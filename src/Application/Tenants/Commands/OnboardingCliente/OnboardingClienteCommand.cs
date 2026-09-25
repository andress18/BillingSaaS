using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Tenants.Commands.OnboardingCliente;

public record OnboardingClienteCommand : IRequest<OnboardingClienteResponseDto>
{
    // Datos obligatorios de la cuenta (al menos Email o Username)
    public string NombreOrganizacion { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Username { get; init; }
    public string? PasswordInicial { get; init; }
    public string PlanCodigo { get; init; } = "MIGRACION_SISTEMA"; // "MIGRACION_SISTEMA" ($20) o "MIGRACION_FIRMA" ($45)
    public string Frecuencia { get; init; } = "ANUAL";             // "ANUAL" o "MENSUAL"
    public Guid? PartnerId { get; init; }                          // Solo configurable por el Administrador

    // Datos tributarios opcionales del Emisor
    public string? Ruc { get; init; }
    public string? RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public string? DireccionMatriz { get; init; }
    public string? CodigoEstablecimiento { get; init; } = "001";
    public string? PuntoEmision { get; init; } = "001";
    public bool ObligadoContabilidad { get; init; } = false;
    public string? RegimenRimpe { get; init; }

    // Certificado digital .p12 opcional
    public string? CertificadoP12Base64 { get; init; }
    public string? PasswordCertificado { get; init; }
}

public record OnboardingClienteResponseDto
{
    public Guid TenantId { get; init; }
    public string NombreOrganizacion { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Username { get; init; }
    public string PasswordTemporal { get; init; } = string.Empty;
    public Guid? PartnerId { get; init; }
    public string PlanNombre { get; init; } = string.Empty;
    public string PlanCodigo { get; init; } = string.Empty;
    public string Frecuencia { get; init; } = string.Empty;
    public DateTime FechaInicio { get; init; }
    public DateTime FechaVencimiento { get; init; }
    public bool EmisorConfigurado { get; init; }
    public bool CertificadoDigitalConfigurado { get; init; }
    public DateTime? FechaCaducidadFirma { get; init; }
    public string MensajeParaCliente { get; init; } = string.Empty;
}

public class OnboardingClienteCommandHandler : IRequestHandler<OnboardingClienteCommand, OnboardingClienteResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;
    private readonly ICertificateEncryptionService _encryptionService;
    private readonly TimeProvider _timeProvider;

    public OnboardingClienteCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IUser user,
        ICertificateEncryptionService encryptionService,
        TimeProvider timeProvider)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
        _encryptionService = encryptionService;
        _timeProvider = timeProvider;
    }

    public async Task<OnboardingClienteResponseDto> Handle(OnboardingClienteCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _user.Id;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        bool isAdmin = await _identityService.IsInRoleAsync(currentUserId, Roles.Administrator);
        bool isPartner = await _identityService.IsInRoleAsync(currentUserId, Roles.Partner);

        if (!isAdmin && !isPartner)
        {
            throw new ForbiddenAccessException();
        }

        // 1. Determinar PartnerId responsable
        Guid? partnerId = null;
        if (isPartner && !isAdmin)
        {
            // El partner siempre asigna clientes a su propia cartera
            partnerId = _user.TenantId;
        }
        else if (isAdmin)
        {
            // El administrador puede especificar el partner o dejarlo directo
            partnerId = request.PartnerId;
        }

        // 2. Validar Plan
        var planCodigo = string.IsNullOrWhiteSpace(request.PlanCodigo) ? "MIGRACION_SISTEMA" : request.PlanCodigo.Trim().ToUpperInvariant();
        var plan = await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == planCodigo && p.Activo, cancellationToken);
        Guard.Against.NotFound(planCodigo, plan);

        // 3. Frecuencia y Fechas
        var frecuencia = request.Frecuencia.Trim().ToUpperInvariant();
        if (frecuencia is not ("ANUAL" or "MENSUAL"))
        {
            frecuencia = "ANUAL";
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var fechaFin = frecuencia == "ANUAL" ? ahoraUtc.AddYears(1) : ahoraUtc.AddMonths(1);

        // 4. Crear Tenant
        var tenant = Tenant.Crear(request.NombreOrganizacion, partnerId);
        _context.Tenants.Add(tenant);

        // 5. Crear Usuario Cliente
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var username = string.IsNullOrWhiteSpace(request.Username)
            ? (email ?? throw new InvalidOperationException("Debe proporcionar un email o nombre de usuario."))
            : request.Username.Trim();

        var password = string.IsNullOrWhiteSpace(request.PasswordInicial)
            ? $"Temp#{Guid.NewGuid().ToString()[..6]}!"
            : request.PasswordInicial.Trim();

        var (createResult, userId) = await _identityService.CreateUserWithTenantAsync(username, email, password, tenant.Id);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"No se pudo crear el usuario del cliente: {string.Join(", ", createResult.Errors)}");
        }

        // 6. Crear Suscripción Activa
        var suscripcion = TenantSubscription.Crear(
            tenant.Id,
            plan.Id,
            ahoraUtc,
            fechaFin,
            frecuencia,
            diasGracia: 3
        );
        _context.Suscripciones.Add(suscripcion);

        // 7. Configuración opcional de Emisor y Certificado Digital
        bool emisorConfigurado = false;
        bool certificadoConfigurado = false;
        DateTime? caducidadFirma = null;

        if (!string.IsNullOrWhiteSpace(request.Ruc))
        {
            var emisor = Emisor.Crear(
                tenantId: tenant.Id,
                ruc: request.Ruc.Trim(),
                razonSocial: request.RazonSocial?.Trim() ?? request.NombreOrganizacion.Trim(),
                direccionMatriz: request.DireccionMatriz?.Trim() ?? "Ecuador",
                codigoEstablecimiento: request.CodigoEstablecimiento?.Trim() ?? "001",
                puntoEmision: request.PuntoEmision?.Trim() ?? "001",
                ambiente: 1, // 1: Pruebas por defecto
                obligadoContabilidad: request.ObligadoContabilidad,
                nombreComercial: request.NombreComercial?.Trim() ?? request.NombreOrganizacion.Trim(),
                regimenRimpe: request.RegimenRimpe
            );

            // Cargar y cifrar certificado digital si fue adjuntado
            if (!string.IsNullOrWhiteSpace(request.CertificadoP12Base64) && !string.IsNullOrWhiteSpace(request.PasswordCertificado))
            {
                byte[] p12Bytes = Convert.FromBase64String(request.CertificadoP12Base64.Trim());
                var certPassword = request.PasswordCertificado.Trim();

                using var cert = X509CertificateLoader.LoadPkcs12(p12Bytes, certPassword);

                if (cert.NotAfter <= ahoraUtc)
                {
                    throw new InvalidOperationException($"El certificado digital adjunto se encuentra caducado (fecha de expiración: {cert.NotAfter:yyyy-MM-dd}).");
                }

                var aad = tenant.Id.ToByteArray();
                var encryptedCert = _encryptionService.Encrypt(p12Bytes, aad);
                var encryptedPass = _encryptionService.EncryptString(certPassword, aad);

                emisor.ConfigurarCertificado(encryptedCert, encryptedPass, cert.NotAfter, cert.Subject);
                certificadoConfigurado = true;
                caducidadFirma = cert.NotAfter;
            }

            _context.Emisores.Add(emisor);
            emisorConfigurado = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var loginIdentifier = !string.IsNullOrWhiteSpace(username) ? username : email;
        var mensaje = $"Hola {request.NombreOrganizacion}, tu cuenta de facturación electrónica ha sido activada con el {plan.Nombre}. Puedes ingresar con tu usuario: {loginIdentifier} y tu contraseña temporal: {password}";

        return new OnboardingClienteResponseDto
        {
            TenantId = tenant.Id,
            NombreOrganizacion = tenant.Nombre,
            Email = email,
            Username = username,
            PasswordTemporal = password,
            PartnerId = tenant.PartnerId,
            PlanNombre = plan.Nombre,
            PlanCodigo = plan.Codigo,
            Frecuencia = frecuencia,
            FechaInicio = ahoraUtc,
            FechaVencimiento = fechaFin,
            EmisorConfigurado = emisorConfigurado,
            CertificadoDigitalConfigurado = certificadoConfigurado,
            FechaCaducidadFirma = caducidadFirma,
            MensajeParaCliente = mensaje
        };
    }
}

