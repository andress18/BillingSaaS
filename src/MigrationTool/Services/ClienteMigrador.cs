using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Identity;
using BillingSaaS.MigrationTool.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillingSaaS.MigrationTool.Services;

public class ClienteMigrador : IClienteMigrador
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ICertificateEncryptionService _encryptionService;
    private readonly ICertificateValidator _certValidator;
    private readonly MigrationOptions _options;
    private readonly ILogger<ClienteMigrador> _logger;

    public ClienteMigrador(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ICertificateEncryptionService encryptionService,
        ICertificateValidator certValidator,
        IOptions<MigrationOptions> options,
        ILogger<ClienteMigrador> logger)
    {
        _context = context;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _encryptionService = encryptionService;
        _certValidator = certValidator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrarClientesAsync(
        IReadOnlyList<ClienteMigracionDto> clientes,
        CancellationToken cancellationToken = default)
    {
        var result = new MigrationResult
        {
            TotalClientesDetectados = clientes.Count
        };

        _logger.LogInformation("Iniciando proceso de migración para {Count} clientes...", clientes.Count);

        var strategy = _context.Database.CreateExecutionStrategy();
        int index = 0;
        foreach (var clienteDto in clientes)
        {
            index++;
            var clienteDesc = !string.IsNullOrWhiteSpace(clienteDto.Ruc) ? clienteDto.Ruc : clienteDto.Email;

            _logger.LogInformation(
                "[{Index}/{Total}] Procesando cliente: {Desc} ({Nombre})",
                index, clientes.Count, clienteDesc, clienteDto.NombreOrganizacion);

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    _context.ChangeTracker.Clear();
                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        // ==========================================
                        // 1. REGLA DE IDEMPOTENCIA
                        // ==========================================
                        var rucNormalizado = clienteDto.Ruc?.Trim();
                        if (!string.IsNullOrWhiteSpace(rucNormalizado))
                        {
                            var emisorExistente = await _context.Emisores
                                .AsNoTracking()
                                .FirstOrDefaultAsync(e => e.Ruc == rucNormalizado, cancellationToken);

                            if (emisorExistente != null)
                            {
                                // Si el cliente ya existe pero tenemos su contraseña real del CSV, actualizarla de inmediato
                                if (!string.IsNullOrWhiteSpace(clienteDto.PasswordPlana) && clienteDto.PasswordPlana != "Temporal123*")
                                {
                                    var uExistente = await _userManager.FindByEmailAsync(clienteDto.Email?.Trim() ?? "")
                                                     ?? await _userManager.FindByNameAsync(clienteDto.Username.Trim());

                                    if (uExistente != null)
                                    {
                                        uExistente.PasswordHash = _passwordHasher.HashPassword(uExistente, clienteDto.PasswordPlana);
                                        var updateRes = await _userManager.UpdateAsync(uExistente);
                                        if (updateRes.Succeeded)
                                        {
                                            _logger.LogInformation("[PASSWORD ACTUALIZADO] Se actualizó la contraseña para el usuario existente '{User}' (RUC: {Ruc}) con la clave real del CSV.", uExistente.UserName, rucNormalizado);
                                        }
                                    }
                                }

                                 // Si el emisor no tenía logo o su logo actual no tiene formato Data URI
                                var emisorTracked = await _context.Emisores.FirstOrDefaultAsync(e => e.Id == emisorExistente.Id, cancellationToken);
                                if (emisorTracked != null)
                                {
                                    var logoParaActualizar = !string.IsNullOrWhiteSpace(clienteDto.Logo)
                                        ? clienteDto.Logo
                                        : emisorTracked.Logo;

                                    if (!string.IsNullOrWhiteSpace(logoParaActualizar))
                                    {
                                        var logoNormalizado = Emisor.NormalizarLogo(logoParaActualizar);
                                        if (emisorTracked.Logo != logoNormalizado)
                                        {
                                            emisorTracked.ActualizarLogo(logoNormalizado);
                                            await _context.SaveChangesAsync(cancellationToken);
                                            _logger.LogInformation("[LOGO ACTUALIZADO/NORMALIZADO] Se corrigió el formato Data URI del logotipo para el emisor existente (RUC: {Ruc}).", rucNormalizado);
                                        }
                                    }

                                    // Configurar/actualizar firma digital si viene en la BD antigua y es válida
                                    if (!string.IsNullOrWhiteSpace(clienteDto.CertificadoBase64))
                                    {
                                        var certExistenteResult = _certValidator.ValidarCertificado(clienteDto.CertificadoBase64, clienteDto.PasswordCertificado);
                                        if (certExistenteResult.EsValido)
                                        {
                                            var tenantAad = emisorTracked.TenantId.ToByteArray();
                                            var certificadoCifradoBytes = _encryptionService.Encrypt(certExistenteResult.CertificadoBytes, tenantAad);
                                            var passwordCifrada = _encryptionService.EncryptString(certExistenteResult.PasswordPlana, tenantAad);

                                            emisorTracked.ConfigurarCertificado(
                                                certificadoBytes: certificadoCifradoBytes,
                                                passwordCifrado: passwordCifrada,
                                                fechaCaducidad: certExistenteResult.FechaCaducidad ?? DateTime.UtcNow.AddYears(1),
                                                subject: certExistenteResult.Subject ?? "CN=Firma Electronica Migrada");

                                            await _context.SaveChangesAsync(cancellationToken);
                                            result.FirmasValidas++;
                                            _logger.LogInformation("[FIRMA ACTUALIZADA] Se configuró y cifró la firma digital desde la base de datos antigua para el emisor existente (RUC: {Ruc}).", rucNormalizado);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("[FIRMA NO CONFIGURADA] Firma en BD antigua no válida para '{Ruc}'. Error: {Error}", rucNormalizado, certExistenteResult.ErrorMensaje);
                                        }
                                    }
                                }

                                var motivo = $"Emisor con RUC '{rucNormalizado}' ya existe en el sistema (TenantId: {emisorExistente.TenantId}).";
                                _logger.LogInformation("[EXISTENTE] {Motivo}. Se aseguraron credenciales, firma y formato de logotipo.", motivo);
                                result.ClientesOmitidos++;
                                result.OmitidosDetalle.Add($"{clienteDesc}: {motivo}");
                                await transaction.CommitAsync(cancellationToken);
                                return;
                            }
                        }

                        var emailNormalizado = !string.IsNullOrWhiteSpace(clienteDto.Email) ? clienteDto.Email.Trim() : clienteDto.Username.Trim();
                        var userExistente = await _userManager.FindByEmailAsync(emailNormalizado)
                                            ?? await _userManager.FindByNameAsync(clienteDto.Username.Trim());

                        if (userExistente != null)
                        {
                            if (!string.IsNullOrWhiteSpace(clienteDto.PasswordPlana) && clienteDto.PasswordPlana != "Temporal123*")
                            {
                                userExistente.PasswordHash = _passwordHasher.HashPassword(userExistente, clienteDto.PasswordPlana);
                                var updateRes = await _userManager.UpdateAsync(userExistente);
                                if (updateRes.Succeeded)
                                {
                                    _logger.LogInformation("[PASSWORD ACTUALIZADO] Se actualizó la contraseña para el usuario existente '{User}' con la clave real del CSV.", userExistente.UserName);
                                }
                            }

                            // Corregir logo y firma también si se encontró por usuario existente
                            var emisorUser = await _context.Emisores.FirstOrDefaultAsync(e => e.TenantId == userExistente.TenantId, cancellationToken);
                            if (emisorUser != null)
                            {
                                var logoAUsar = !string.IsNullOrWhiteSpace(clienteDto.Logo) ? clienteDto.Logo : emisorUser.Logo;
                                if (!string.IsNullOrWhiteSpace(logoAUsar))
                                {
                                    var logoNorm = Emisor.NormalizarLogo(logoAUsar);
                                    if (emisorUser.Logo != logoNorm)
                                    {
                                        emisorUser.ActualizarLogo(logoNorm);
                                        await _context.SaveChangesAsync(cancellationToken);
                                        _logger.LogInformation("[LOGO ACTUALIZADO/NORMALIZADO] Se corrigió el formato Data URI del logotipo para el emisor del usuario '{User}'.", userExistente.UserName);
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(clienteDto.CertificadoBase64))
                                {
                                    var certUserResult = _certValidator.ValidarCertificado(clienteDto.CertificadoBase64, clienteDto.PasswordCertificado);
                                    if (certUserResult.EsValido)
                                    {
                                        var tenantAad = emisorUser.TenantId.ToByteArray();
                                        var certificadoCifradoBytes = _encryptionService.Encrypt(certUserResult.CertificadoBytes, tenantAad);
                                        var passwordCifrada = _encryptionService.EncryptString(certUserResult.PasswordPlana, tenantAad);

                                        emisorUser.ConfigurarCertificado(
                                            certificadoBytes: certificadoCifradoBytes,
                                            passwordCifrado: passwordCifrada,
                                            fechaCaducidad: certUserResult.FechaCaducidad ?? DateTime.UtcNow.AddYears(1),
                                            subject: certUserResult.Subject ?? "CN=Firma Electronica Migrada");

                                        await _context.SaveChangesAsync(cancellationToken);
                                        result.FirmasValidas++;
                                        _logger.LogInformation("[FIRMA ACTUALIZADA] Se configuró y cifró la firma digital desde la base de datos antigua para el usuario '{User}'.", userExistente.UserName);
                                    }
                                }
                            }

                            var motivo = $"Usuario con Email '{emailNormalizado}' o UserName '{clienteDto.Username}' ya existe en Identity.";
                            _logger.LogInformation("[EXISTENTE] {Motivo}. Se aseguraron credenciales, firma y formato de logotipo.", motivo);
                            result.ClientesOmitidos++;
                            result.OmitidosDetalle.Add($"{clienteDesc}: {motivo}");
                            await transaction.CommitAsync(cancellationToken);
                            return;
                        }

                // ==========================================
                // 2. CREACIÓN DEL TENANT
                // ==========================================
                var tenantId = Guid.NewGuid();
                var nombreTenant = !string.IsNullOrWhiteSpace(clienteDto.NombreOrganizacion)
                    ? clienteDto.NombreOrganizacion
                    : (!string.IsNullOrWhiteSpace(clienteDto.RazonSocial) ? clienteDto.RazonSocial : clienteDto.Username);

                var partnerId = clienteDto.PartnerId != Guid.Empty ? clienteDto.PartnerId : _options.PartnerId;
                var tenant = Tenant.Crear(nombreTenant, partnerId, tenantId);

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync(cancellationToken);

                // ==========================================
                // 3. CREACIÓN DEL USUARIO IDENTITY
                // ==========================================
                var userName = !string.IsNullOrWhiteSpace(clienteDto.Username) ? clienteDto.Username.Trim() : emailNormalizado;
                var user = new ApplicationUser
                {
                    UserName = userName,
                    Email = emailNormalizado,
                    TenantId = tenant.Id,
                    EmailConfirmed = true
                };

                // Hasheo directo con IPasswordHasher para soportar cualquier contraseña heredada sin que falle la validación de complejidad
                var passwordPlana = !string.IsNullOrWhiteSpace(clienteDto.PasswordPlana) ? clienteDto.PasswordPlana : "Temporal123*";
                user.PasswordHash = _passwordHasher.HashPassword(user, passwordPlana);
                user.SecurityStamp = Guid.NewGuid().ToString();

                var userResult = await _userManager.CreateAsync(user);
                if (!userResult.Succeeded)
                {
                    var errorDesc = string.Join("; ", userResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Error al crear usuario Identity '{userName}': {errorDesc}");
                }

                // ==========================================
                // 4. SUSCRIPCIÓN Y PLAN
                // ==========================================
                var codigoPlan = !string.IsNullOrWhiteSpace(clienteDto.PlanCodigo) ? clienteDto.PlanCodigo.Trim() : _options.PlanCodigoDefault;
                var plan = await _context.Planes
                               .FirstOrDefaultAsync(p => p.Codigo == codigoPlan && p.Activo, cancellationToken)
                           ?? await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == "MIGRACION_SISTEMA" && p.Activo, cancellationToken)
                           ?? await _context.Planes.FirstOrDefaultAsync(p => p.Activo, cancellationToken);

                if (plan == null)
                {
                    throw new InvalidOperationException($"No se encontró ningún plan activo en la base de datos para asignar al cliente.");
                }

                var fechaFin = clienteDto.FechaFinPlan > DateTime.MinValue ? clienteDto.FechaFinPlan : DateTime.UtcNow.AddYears(1);
                var fechaInicio = clienteDto.FechaInicioPlan > DateTime.MinValue ? clienteDto.FechaInicioPlan : DateTime.UtcNow;
                if (fechaFin < fechaInicio)
                {
                    fechaInicio = fechaFin.AddMonths(-1);
                }

                var suscripcion = TenantSubscription.Crear(
                    tenantId: tenant.Id,
                    planId: plan.Id,
                    fechaInicio: fechaInicio,
                    fechaVencimiento: fechaFin,
                    frecuencia: string.IsNullOrWhiteSpace(clienteDto.Frecuencia) ? "ANUAL" : clienteDto.Frecuencia,
                    diasGracia: clienteDto.DiasGracia > 0 ? clienteDto.DiasGracia : 3);

                if (fechaFin < DateTime.UtcNow)
                {
                    suscripcion.MarcarComoVencido();
                }

                _context.Suscripciones.Add(suscripcion);
                await _context.SaveChangesAsync(cancellationToken);

                // ==========================================
                // 5. EMISOR Y CERTIFICADO DIGITAL (.p12)
                // ==========================================
                var ruc = !string.IsNullOrWhiteSpace(clienteDto.Ruc) && clienteDto.Ruc.Length == 13
                    ? clienteDto.Ruc.Trim()
                    : "9999999999001";

                var razonSocial = !string.IsNullOrWhiteSpace(clienteDto.RazonSocial)
                    ? clienteDto.RazonSocial.Trim()
                    : nombreTenant;

                var direccionMatriz = !string.IsNullOrWhiteSpace(clienteDto.DireccionMatriz)
                    ? clienteDto.DireccionMatriz.Trim()
                    : "Quito - Ecuador";

                var codigoEstab = !string.IsNullOrWhiteSpace(clienteDto.CodigoEstablecimiento) && clienteDto.CodigoEstablecimiento.Length == 3
                    ? clienteDto.CodigoEstablecimiento.Trim()
                    : "001";

                var puntoEmi = !string.IsNullOrWhiteSpace(clienteDto.PuntoEmision) && clienteDto.PuntoEmision.Length == 3
                    ? clienteDto.PuntoEmision.Trim()
                    : "001";

                var ambiente = clienteDto.Ambiente is 1 or 2 ? clienteDto.Ambiente : 2;

                // Calcular secuencial inicial a partir del MAX histórico + 1
                int maxSecuencialHist = clienteDto.UltimoSecuencialFactura;
                foreach (var f in clienteDto.Facturas)
                {
                    if (int.TryParse(f.Secuencial, out var s) && s > maxSecuencialHist)
                    {
                        maxSecuencialHist = s;
                    }
                }
                int secuencialInicial = Math.Max(maxSecuencialHist + 1, 1);

                var emisor = Emisor.Crear(
                    tenantId: tenant.Id,
                    ruc: ruc,
                    razonSocial: razonSocial,
                    direccionMatriz: direccionMatriz,
                    codigoEstablecimiento: codigoEstab,
                    puntoEmision: puntoEmi,
                    ambiente: ambiente,
                    obligadoContabilidad: clienteDto.ObligadoContabilidad,
                    nombreComercial: clienteDto.NombreComercial,
                    direccionEstablecimiento: clienteDto.DireccionEstablecimiento,
                    regimenRimpe: clienteDto.RegimenRimpe,
                    contribuyenteEspecial: clienteDto.ContribuyenteEspecial,
                    secuencialInicial: secuencialInicial,
                    logo: clienteDto.Logo);

                // Validar y cifrar certificado digital
                var certResult = _certValidator.ValidarCertificado(clienteDto.CertificadoBase64, clienteDto.PasswordCertificado);
                if (certResult.EsValido)
                {
                    // AAD OBLIGATORIO: Tenant.Id.ToByteArray()
                    var tenantAad = tenant.Id.ToByteArray();
                    var certificadoCifradoBytes = _encryptionService.Encrypt(certResult.CertificadoBytes, tenantAad);
                    var passwordCifrada = _encryptionService.EncryptString(certResult.PasswordPlana, tenantAad);

                    emisor.ConfigurarCertificado(
                        certificadoBytes: certificadoCifradoBytes,
                        passwordCifrado: passwordCifrada,
                        fechaCaducidad: certResult.FechaCaducidad ?? DateTime.UtcNow.AddYears(1),
                        subject: certResult.Subject ?? "CN=Firma Electronica Migrada");

                    if (certResult.EstaCaducado)
                    {
                        result.FirmasCaducadas++;
                        _logger.LogWarning("Firma del cliente {Desc} está CADUCADA (Venció: {Fecha:yyyy-MM-dd}).", clienteDesc, certResult.FechaCaducidad);
                    }
                    else
                    {
                        result.FirmasValidas++;
                        _logger.LogInformation("Firma del cliente {Desc} válida hasta {Fecha:yyyy-MM-dd}.", clienteDesc, certResult.FechaCaducidad);
                    }
                }
                else
                {
                    result.FirmasSinCertificado++;
                    _logger.LogInformation("Cliente {Desc} migrado sin certificado digital (.p12): {Motivo}", clienteDesc, certResult.ErrorMensaje);
                }

                _context.Emisores.Add(emisor);
                await _context.SaveChangesAsync(cancellationToken);

                // ==========================================
                // 6. CATÁLOGO DE CLIENTES Y FACTURAS HISTÓRICAS
                // ==========================================
                var compradoresUnicos = new Dictionary<string, CatalogoCliente>(StringComparer.OrdinalIgnoreCase);

                // 6.1 Identificar compradores únicos para poblar CatalogoCliente
                foreach (var fDto in clienteDto.Facturas)
                {
                    var compDto = fDto.Comprador;
                    if (compDto == null || string.IsNullOrWhiteSpace(compDto.Identificacion)) continue;

                    var idLimpia = compDto.Identificacion.Trim();
                    if (!compradoresUnicos.ContainsKey(idLimpia))
                    {
                        var tipoId = !string.IsNullOrWhiteSpace(compDto.TipoIdentificacion) ? compDto.TipoIdentificacion.Trim() : "07";
                        var razonSoc = !string.IsNullOrWhiteSpace(compDto.RazonSocial) ? compDto.RazonSocial.Trim() : "CONSUMIDOR FINAL";
                        
                        var catCliente = CatalogoCliente.Crear(
                            tenantId: tenant.Id,
                            tipoIdentificacion: tipoId.Length <= 2 ? tipoId : "07",
                            identificacion: idLimpia.Length <= 20 ? idLimpia : idLimpia[..20],
                            razonSocial: razonSoc.Length <= 300 ? razonSoc : razonSoc[..300],
                            direccion: compDto.Direccion,
                            correoElectronico: compDto.CorreoElectronico);

                        compradoresUnicos[idLimpia] = catCliente;
                        _context.CatalogoClientes.Add(catCliente);
                        result.TotalClientesCatalogoCreados++;
                    }
                }

                if (compradoresUnicos.Count > 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 6.2 Insertar facturas históricas
                int facturasMigradasCliente = 0;
                foreach (var fDto in clienteDto.Facturas)
                {
                    try
                    {
                        var compDto = fDto.Comprador ?? new CompradorMigracionDto();
                        var tipoId = !string.IsNullOrWhiteSpace(compDto.TipoIdentificacion) ? compDto.TipoIdentificacion.Trim() : "07";
                        var idComprador = !string.IsNullOrWhiteSpace(compDto.Identificacion) ? compDto.Identificacion.Trim() : "9999999999999";
                        var nomComprador = !string.IsNullOrWhiteSpace(compDto.RazonSocial) ? compDto.RazonSocial.Trim() : "CONSUMIDOR FINAL";

                        var comprador = Comprador.Crear(
                            tipoIdentificacion: tipoId.Length <= 2 ? tipoId : "07",
                            identificacion: idComprador.Length <= 20 ? idComprador : idComprador[..20],
                            razonSocial: nomComprador.Length <= 300 ? nomComprador : nomComprador[..300],
                            direccion: compDto.Direccion,
                            correoElectronico: compDto.CorreoElectronico);

                        // Mapear detalles
                        var detalles = new List<DetalleFactura>();
                        foreach (var detDto in fDto.Detalles)
                        {
                            var impuestos = detDto.Impuestos.Select(imp =>
                                Impuesto.Crear(
                                    codigo: imp.Codigo,
                                    codigoPorcentaje: imp.CodigoPorcentaje,
                                    tarifa: imp.Tarifa,
                                    baseImponible: imp.BaseImponible
                                )).ToList();

                            if (impuestos.Count == 0)
                            {
                                impuestos.Add(Impuesto.Crear("2", "2", 15.00m, detDto.Cantidad * detDto.PrecioUnitario));
                            }

                            var codPrincipal = !string.IsNullOrWhiteSpace(detDto.CodigoPrincipal) ? detDto.CodigoPrincipal : "ITEM";
                            var desc = !string.IsNullOrWhiteSpace(detDto.Descripcion) ? detDto.Descripcion : "Item de Factura";

                            var detalle = DetalleFactura.Crear(
                                codigoPrincipal: codPrincipal.Length <= 25 ? codPrincipal : codPrincipal[..25],
                                descripcion: desc.Length <= 300 ? desc : desc[..300],
                                cantidad: detDto.Cantidad > 0 ? detDto.Cantidad : 1,
                                precioUnitario: detDto.PrecioUnitario,
                                descuento: detDto.Descuento,
                                impuestos: impuestos);

                            detalles.Add(detalle);
                        }

                        // Si no había detalles pero hay total
                        if (detalles.Count == 0)
                        {
                            var impDefault = new List<Impuesto>
                            {
                                Impuesto.Crear("2", "2", 15.00m, fDto.TotalSinImpuestos > 0 ? fDto.TotalSinImpuestos : fDto.ImporteTotal)
                            };

                            detalles.Add(DetalleFactura.Crear(
                                codigoPrincipal: "MIG-HIST",
                                descripcion: "Servicios Facturados (Migración)",
                                cantidad: 1,
                                precioUnitario: fDto.TotalSinImpuestos > 0 ? fDto.TotalSinImpuestos : fDto.ImporteTotal,
                                descuento: fDto.TotalDescuento,
                                impuestos: impDefault));
                        }

                        Guid? catClienteId = null;
                        if (compradoresUnicos.TryGetValue(idComprador, out var catFound))
                        {
                            catClienteId = catFound.Id;
                        }

                        var secFactura = !string.IsNullOrWhiteSpace(fDto.Secuencial) ? fDto.Secuencial.Trim() : "000000001";
                        if (secFactura.Length < 9 && int.TryParse(secFactura, out var secNum))
                        {
                            secFactura = secNum.ToString("D9");
                        }

                        var estabFactura = !string.IsNullOrWhiteSpace(fDto.Establecimiento) ? fDto.Establecimiento.Trim() : codigoEstab;
                        var ptoFactura = !string.IsNullOrWhiteSpace(fDto.PuntoEmision) ? fDto.PuntoEmision.Trim() : puntoEmi;
                        var fechaEmisionFactura = fDto.FechaEmision > DateTime.MinValue ? fDto.FechaEmision : DateTime.UtcNow;

                        // Omitir validación de consumidor final moderno para facturas históricas ya autorizadas
                        var factura = Factura.Crear(
                            tenantId: tenant.Id,
                            ambiente: emisor.Ambiente,
                            razonSocial: emisor.RazonSocial,
                            rucEmisor: emisor.Ruc,
                            establecimiento: estabFactura,
                            puntoEmision: ptoFactura,
                            secuencial: secFactura,
                            direccionMatriz: emisor.DireccionMatriz,
                            fechaEmision: fechaEmisionFactura,
                            cliente: comprador,
                            detalles: detalles,
                            emisorId: emisor.Id,
                            contribuyenteRimpe: emisor.RegimenRimpe,
                            catalogoClienteId: catClienteId,
                            formaPago: !string.IsNullOrWhiteSpace(fDto.FormaPago) ? fDto.FormaPago : "01",
                            validarInvariantes: false);

                        if (!string.IsNullOrWhiteSpace(fDto.ClaveAcceso))
                        {
                            factura.AsignarClaveAcceso(fDto.ClaveAcceso);
                        }

                        factura.EstablecerEstadoHistorico(
                            estado: fDto.Estado,
                            numeroAutorizacion: fDto.NumeroAutorizacion,
                            fechaAutorizacion: fDto.FechaAutorizacion,
                            mensajeError: fDto.MensajeErrorSri,
                            xmlFirmado: fDto.XmlFirmado);

                        _context.Facturas.Add(factura);
                        facturasMigradasCliente++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al migrar factura secuencial '{Sec}' del cliente {Email}", fDto.Secuencial, clienteDesc);
                        if (!_options.OmitirFacturasConError)
                        {
                            throw;
                        }
                    }
                }

                if (facturasMigradasCliente > 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    result.TotalFacturasMigradas += facturasMigradasCliente;
                }

                // ==========================================
                // 7. CONFIRMAR TRANSACCIÓN
                // ==========================================
                if (_options.DryRun)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogInformation(
                        "[DRY-RUN] Simulación exitosa para cliente {Desc} ({Facturas} facturas, {Compradores} compradores). Cambios no persistidos.",
                        clienteDesc, facturasMigradasCliente, compradoresUnicos.Count);
                }
                else
                {
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation(
                        "Cliente {Desc} migrado exitosamente con {Facturas} facturas y {Compradores} clientes de catálogo.",
                        clienteDesc, facturasMigradasCliente, compradoresUnicos.Count);
                }

                        result.ClientesProcesadosExitosamente++;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _context.ChangeTracker.Clear();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                result.ClientesConError++;
                result.Errores.Add(new MigrationErrorDetail
                {
                    IdentificadorCliente = clienteDesc,
                    Ruc = clienteDto.Ruc ?? string.Empty,
                    Mensaje = ex.Message,
                    StackTrace = ex.StackTrace
                });

                _logger.LogError(ex, "ERROR al procesar cliente {Desc}. Se realizó ROLLBACK de todas sus operaciones.", clienteDesc);
            }
        }

        return result;
    }
}
