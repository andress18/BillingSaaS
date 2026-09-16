using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using System.Security.Cryptography.X509Certificates;

namespace BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;

public record ConfigurarEmisorCommand : IRequest<int>
{
    public Guid TenantId { get; init; }
    public string Ruc { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? NombreComercial { get; init; }
    public string DireccionMatriz { get; init; } = null!;
    public string? DireccionEstablecimiento { get; init; }
    public string CodigoEstablecimiento { get; init; } = "001";
    public string PuntoEmision { get; init; } = "001";
    public int Ambiente { get; init; } = 1; // 1: Pruebas, 2: Produccion
    public bool ObligadoContabilidad { get; init; }
    public string? RegimenRimpe { get; init; }
    public string? ContribuyenteEspecial { get; init; }
    public int SecuencialInicial { get; init; } = 0;

    // Certificado digital .p12 (base64 o hex) y su contraseña
    public string? CertificadoBase64 { get; init; }
    public string? PasswordCertificado { get; init; }
}

public class ConfigurarEmisorCommandHandler : IRequestHandler<ConfigurarEmisorCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICertificateEncryptionService _encryptionService;

    public ConfigurarEmisorCommandHandler(
        IApplicationDbContext context,
        IUser user,
        ICertificateEncryptionService encryptionService)
    {
        _context = context;
        _user = user;
        _encryptionService = encryptionService;
    }

    public async Task<int> Handle(ConfigurarEmisorCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId != Guid.Empty 
            ? request.TenantId 
            : (_user.TenantId ?? throw new UnauthorizedAccessException("Usuario no tiene TenantId asignado."));

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);

        if (emisor == null)
        {
            emisor = Emisor.Crear(
                tenantId: tenantId,
                ruc: request.Ruc,
                razonSocial: request.RazonSocial,
                direccionMatriz: request.DireccionMatriz,
                codigoEstablecimiento: request.CodigoEstablecimiento,
                puntoEmision: request.PuntoEmision,
                ambiente: request.Ambiente,
                obligadoContabilidad: request.ObligadoContabilidad,
                nombreComercial: request.NombreComercial,
                direccionEstablecimiento: request.DireccionEstablecimiento,
                regimenRimpe: request.RegimenRimpe,
                contribuyenteEspecial: request.ContribuyenteEspecial,
                secuencialInicial: request.SecuencialInicial
            );

            _context.Emisores.Add(emisor);
        }
        else
        {
            emisor.ActualizarDatosTributarios(
                razonSocial: request.RazonSocial,
                direccionMatriz: request.DireccionMatriz,
                nombreComercial: request.NombreComercial,
                direccionEstablecimiento: request.DireccionEstablecimiento,
                codigoEstablecimiento: request.CodigoEstablecimiento,
                puntoEmision: request.PuntoEmision,
                ambiente: request.Ambiente,
                obligadoContabilidad: request.ObligadoContabilidad,
                regimenRimpe: request.RegimenRimpe,
                contribuyenteEspecial: request.ContribuyenteEspecial,
                secuencialInicial: request.SecuencialInicial
            );
        }

        if (!string.IsNullOrWhiteSpace(request.CertificadoBase64) && !string.IsNullOrWhiteSpace(request.PasswordCertificado))
        {
            var raw = request.CertificadoBase64.Trim();
            byte[] p12Bytes = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromHexString(raw[2..])
                : Convert.FromBase64String(raw);

            // 1. Validar integridad y vigencia del certificado .p12 en memoria volátil
            using var cert = X509CertificateLoader.LoadPkcs12(p12Bytes, request.PasswordCertificado);
            var fechaCaducidad = cert.NotAfter;
            var subject = cert.Subject;

            // 2. Cifrar en reposo con AES-256-GCM vinculando criptográficamente el TenantId como AAD (LOPDP / OWASP)
            var aad = tenantId.ToByteArray();
            var encryptedCertBytes = _encryptionService.Encrypt(p12Bytes, aad);
            var encryptedPassword = _encryptionService.EncryptString(request.PasswordCertificado, aad);

            // 3. Persistir únicamente los datos cifrados
            emisor.ConfigurarCertificado(encryptedCertBytes, encryptedPassword, fechaCaducidad, subject);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return emisor.Id;
    }
}

