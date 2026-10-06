using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
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
                                /*
                                // [COMENTADO PARA PRODUCCIÓN]: No sobrescribir contraseñas de usuarios existentes
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
                                */

                                var emisorTracked = await _context.Emisores.FirstOrDefaultAsync(e => e.Id == emisorExistente.Id, cancellationToken);
                                if (emisorTracked != null)
                                {
                                    /*
                                    // [COMENTADO PARA PRODUCCIÓN]: No sobrescribir logotipos de emisores existentes
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

                                    // [COMENTADO PARA PRODUCCIÓN]: No sobrescribir firmas digitales de emisores existentes
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
                                    */

                                    // Sincronizar datos históricos (facturas, compradores, productos)
                                    await MigrarDatosHistoricosClienteAsync(emisorTracked.TenantId, emisorTracked, clienteDto, clienteDesc, result, cancellationToken);
                                }

                                if (_options.DryRun)
                                {
                                    await transaction.RollbackAsync(cancellationToken);
                                    _logger.LogInformation("[DRY-RUN] Cliente existente '{Desc}' procesado pero cambios revertidos.", clienteDesc);
                                }
                                else
                                {
                                    await transaction.CommitAsync(cancellationToken);
                                    _logger.LogInformation("Cliente existente '{Desc}' (RUC: {Ruc}) actualizado y sincronizado exitosamente.", clienteDesc, rucNormalizado);
                                }

                                result.ClientesProcesadosExitosamente++;
                                return;
                            }
                        }

                        var emailNormalizado = !string.IsNullOrWhiteSpace(clienteDto.Email) ? clienteDto.Email.Trim() : clienteDto.Username.Trim();
                        var userExistente = await _userManager.FindByEmailAsync(emailNormalizado)
                                            ?? await _userManager.FindByNameAsync(clienteDto.Username.Trim());

                        if (userExistente != null)
                        {
                            /*
                            // [COMENTADO PARA PRODUCCIÓN]: No sobrescribir contraseñas de usuarios existentes
                            if (!string.IsNullOrWhiteSpace(clienteDto.PasswordPlana) && clienteDto.PasswordPlana != "Temporal123*")
                            {
                                userExistente.PasswordHash = _passwordHasher.HashPassword(userExistente, clienteDto.PasswordPlana);
                                var updateRes = await _userManager.UpdateAsync(userExistente);
                                if (updateRes.Succeeded)
                                {
                                    _logger.LogInformation("[PASSWORD ACTUALIZADO] Se actualizó la contraseña para el usuario existente '{User}' con la clave real del CSV.", userExistente.UserName);
                                }
                            }
                            */

                            // Corregir logo y firma también si se encontró por usuario existente
                            var emisorUser = await _context.Emisores.FirstOrDefaultAsync(e => e.TenantId == userExistente.TenantId, cancellationToken);
                            if (emisorUser == null)
                            {
                                emisorUser = CrearEmisor(userExistente.TenantId, clienteDto, clienteDesc, result);
                                _context.Emisores.Add(emisorUser);
                                await _context.SaveChangesAsync(cancellationToken);
                            }
                            /*
                            else
                            {
                                // [COMENTADO PARA PRODUCCIÓN]: No sobrescribir logotipo ni firma digital
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
                            */

                            // Sincronizar datos históricos (facturas, compradores, productos)
                            await MigrarDatosHistoricosClienteAsync(userExistente.TenantId, emisorUser, clienteDto, clienteDesc, result, cancellationToken);

                            if (_options.DryRun)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                _logger.LogInformation("[DRY-RUN] Cliente existente '{Desc}' procesado pero cambios revertidos.", clienteDesc);
                            }
                            else
                            {
                                await transaction.CommitAsync(cancellationToken);
                                _logger.LogInformation("Cliente existente '{Desc}' (User: {User}) actualizado y sincronizado exitosamente.", clienteDesc, userExistente.UserName);
                            }

                            result.ClientesProcesadosExitosamente++;
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
                        var tenant = Tenant.Crear(nombreTenant, partnerId, tenantId, estado: TenantEstados.Activo);

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
                        var emisor = CrearEmisor(tenant.Id, clienteDto, clienteDesc, result);
                        _context.Emisores.Add(emisor);
                        await _context.SaveChangesAsync(cancellationToken);

                        // ==========================================
                        // 6. CATÁLOGO DE CLIENTES, PRODUCTOS Y FACTURAS HISTÓRICAS
                        // ==========================================
                        await MigrarDatosHistoricosClienteAsync(tenant.Id, emisor, clienteDto, clienteDesc, result, cancellationToken);

                        // ==========================================
                        // 7. CONFIRMAR TRANSACCIÓN
                        // ==========================================
                        if (_options.DryRun)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            _logger.LogInformation(
                                "[DRY-RUN] Simulación exitosa para nuevo cliente {Desc}. Cambios no persistidos.",
                                clienteDesc);
                        }
                        else
                        {
                            await transaction.CommitAsync(cancellationToken);
                            _logger.LogInformation(
                                "Cliente {Desc} migrado exitosamente.",
                                clienteDesc);
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

    private Emisor CrearEmisor(
        Guid tenantId,
        ClienteMigracionDto clienteDto,
        string clienteDesc,
        MigrationResult result)
    {
        var ruc = !string.IsNullOrWhiteSpace(clienteDto.Ruc) && clienteDto.Ruc.Length == 13
            ? clienteDto.Ruc.Trim()
            : "9999999999001";

        var razonSocial = !string.IsNullOrWhiteSpace(clienteDto.RazonSocial)
            ? clienteDto.RazonSocial.Trim()
            : (!string.IsNullOrWhiteSpace(clienteDto.NombreOrganizacion) ? clienteDto.NombreOrganizacion.Trim() : clienteDto.Username.Trim());

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
            tenantId: tenantId,
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

        if (!string.IsNullOrWhiteSpace(clienteDto.CertificadoBase64))
        {
            var certResult = _certValidator.ValidarCertificado(clienteDto.CertificadoBase64, clienteDto.PasswordCertificado);
            if (certResult.EsValido)
            {
                var tenantAad = tenantId.ToByteArray();
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
                _logger.LogInformation("Cliente {Desc} configurado sin certificado digital (.p12): {Motivo}", clienteDesc, certResult.ErrorMensaje);
            }
        }
        else
        {
            result.FirmasSinCertificado++;
            _logger.LogInformation("Cliente {Desc} sin certificado digital registrado en BD antigua.", clienteDesc);
        }

        return emisor;
    }

    private async Task MigrarDatosHistoricosClienteAsync(
        Guid tenantId,
        Emisor emisor,
        ClienteMigracionDto clienteDto,
        string clienteDesc,
        MigrationResult result,
        CancellationToken cancellationToken)
    {
        // 1. COMPRADORES (CatalogoCliente)
        var compradoresExistentes = await _context.CatalogoClientes
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var compradoresUnicos = compradoresExistentes
            .GroupBy(c => c.Identificacion.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        bool hubieronCompradoresNuevos = false;

        // Combinar catálogo de compradores directo con compradores de facturas históricas
        var todosCompradores = clienteDto.Compradores
            .Concat(clienteDto.Facturas.Select(f => f.Comprador))
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Identificacion));

        foreach (var compDto in todosCompradores)
        {
            var idLimpia = compDto.Identificacion.Trim();
            if (string.IsNullOrWhiteSpace(idLimpia)) continue;

            if (!compradoresUnicos.TryGetValue(idLimpia, out var catExistente))
            {
                var tipoId = !string.IsNullOrWhiteSpace(compDto.TipoIdentificacion) ? compDto.TipoIdentificacion.Trim() : "07";
                var razonSoc = !string.IsNullOrWhiteSpace(compDto.RazonSocial) ? compDto.RazonSocial.Trim() : "CONSUMIDOR FINAL";

                var catCliente = CatalogoCliente.Crear(
                    tenantId: tenantId,
                    tipoIdentificacion: tipoId.Length <= 2 ? tipoId : "07",
                    identificacion: idLimpia.Length <= 20 ? idLimpia : idLimpia[..20],
                    razonSocial: razonSoc.Length <= 300 ? razonSoc : razonSoc[..300],
                    direccion: compDto.Direccion != null && compDto.Direccion.Length > 300 ? compDto.Direccion[..300] : compDto.Direccion,
                    correoElectronico: compDto.CorreoElectronico != null && compDto.CorreoElectronico.Length > 300 ? compDto.CorreoElectronico[..300] : compDto.CorreoElectronico);

                compradoresUnicos[idLimpia] = catCliente;
                _context.CatalogoClientes.Add(catCliente);
                result.TotalClientesCatalogoCreados++;
                hubieronCompradoresNuevos = true;
            }
            /*
            else
            {
                // [COMENTADO PARA PRODUCCIÓN]: No modificar compradores existentes
                bool modificado = false;
                var emailActual = catExistente.CorreoElectronico;
                var dirActual = catExistente.Direccion;
                var razonActual = catExistente.RazonSocial;

                if (string.IsNullOrWhiteSpace(emailActual) && !string.IsNullOrWhiteSpace(compDto.CorreoElectronico))
                {
                    emailActual = compDto.CorreoElectronico.Trim();
                    if (emailActual.Length > 300) emailActual = emailActual[..300];
                    modificado = true;
                }

                if (string.IsNullOrWhiteSpace(dirActual) && !string.IsNullOrWhiteSpace(compDto.Direccion))
                {
                    dirActual = compDto.Direccion.Trim();
                    if (dirActual.Length > 300) dirActual = dirActual[..300];
                    modificado = true;
                }

                if (razonActual == "CONSUMIDOR FINAL" && !string.IsNullOrWhiteSpace(compDto.RazonSocial) && compDto.RazonSocial.Trim() != "CONSUMIDOR FINAL")
                {
                    razonActual = compDto.RazonSocial.Trim();
                    if (razonActual.Length > 300) razonActual = razonActual[..300];
                    modificado = true;
                }

                if (modificado)
                {
                    catExistente.ActualizarContacto(razonActual, dirActual, emailActual);
                    hubieronCompradoresNuevos = true;
                }
            }
            */
        }

        if (hubieronCompradoresNuevos)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 2. PRODUCTOS (CatalogoProducto)
        var productosExistentes = await _context.CatalogoProductos
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var productosUnicos = productosExistentes
            .GroupBy(p => p.CodigoPrincipal.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        bool hubieronProductosNuevos = false;

        // Combinar catálogo de productos directo con detalles de facturas históricas
        var todosProductos = clienteDto.Productos
            .Concat(clienteDto.Facturas.SelectMany(f => f.Detalles))
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.CodigoPrincipal));

        foreach (var detDto in todosProductos)
        {
            var codPrincipal = !string.IsNullOrWhiteSpace(detDto.CodigoPrincipal) ? detDto.CodigoPrincipal.Trim() : "";
            if (string.IsNullOrWhiteSpace(codPrincipal)) continue;
            if (codPrincipal.Length > 25) codPrincipal = codPrincipal[..25];

            if (!productosUnicos.TryGetValue(codPrincipal, out var prodExistente))
            {
                var desc = !string.IsNullOrWhiteSpace(detDto.Descripcion) ? detDto.Descripcion.Trim() : "Item de Factura";
                if (desc.Length > 300) desc = desc[..300];

                var precio = detDto.PrecioUnitario >= 0 ? detDto.PrecioUnitario : 0m;
                var primerImp = detDto.Impuestos.FirstOrDefault();
                var codImp = !string.IsNullOrWhiteSpace(primerImp?.Codigo) ? primerImp.Codigo.Trim() : "2";
                var codPorc = !string.IsNullOrWhiteSpace(primerImp?.CodigoPorcentaje) ? primerImp.CodigoPorcentaje.Trim() : "4";
                var tarifa = primerImp != null && primerImp.Tarifa >= 0 ? primerImp.Tarifa : 15.00m;

                try
                {
                    var catProd = CatalogoProducto.Crear(
                        tenantId: tenantId,
                        codigoPrincipal: codPrincipal,
                        descripcion: desc,
                        precioUnitario: precio,
                        codigoImpuesto: codImp.Length <= 10 ? codImp : codImp[..10],
                        codigoPorcentaje: codPorc.Length <= 10 ? codPorc : codPorc[..10],
                        tarifa: tarifa);

                    productosUnicos[codPrincipal] = catProd;
                    _context.CatalogoProductos.Add(catProd);
                    result.TotalProductosCatalogoCreados++;
                    hubieronProductosNuevos = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("No se pudo agregar producto '{Cod}' al catálogo para {Desc}: {Msg}", codPrincipal, clienteDesc, ex.Message);
                }
            }
            /*
            else
            {
                // [COMENTADO PARA PRODUCCIÓN]: No modificar productos existentes
                bool modificado = false;
                var descActual = prodExistente.Descripcion;
                var precioActual = prodExistente.PrecioUnitario;

                if ((descActual == "Item de Factura" || descActual == "ITEM") && !string.IsNullOrWhiteSpace(detDto.Descripcion) && detDto.Descripcion != "Item de Factura")
                {
                    descActual = detDto.Descripcion.Trim();
                    if (descActual.Length > 300) descActual = descActual[..300];
                    modificado = true;
                }

                if (precioActual == 0m && detDto.PrecioUnitario > 0m)
                {
                    precioActual = detDto.PrecioUnitario;
                    modificado = true;
                }

                if (modificado)
                {
                    prodExistente.ActualizarDatos(
                        descActual,
                        precioActual,
                        prodExistente.CodigoImpuesto,
                        prodExistente.CodigoPorcentaje,
                        prodExistente.Tarifa);
                    hubieronProductosNuevos = true;
                }
            }
            */
        }

        if (hubieronProductosNuevos)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        /*
        // =========================================================================
        // [COMENTADO PARA PRODUCCIÓN]:
        // No procesar facturas históricas ni actualizar correlativo del emisor.
        // La migración se ejecuta exclusivamente para insertar compradores y productos faltantes.
        // =========================================================================

        // 3. FACTURAS HISTÓRICAS
        var facturasExistentesClaves = new HashSet<string>(
            await _context.Facturas
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId && f.ClaveAcceso != null)
                .Select(f => f.ClaveAcceso!)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var facturasExistentesNumeracion = new HashSet<string>(
            await _context.Facturas
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .Select(f => $"{f.Establecimiento}-{f.PuntoEmision}-{f.Secuencial}")
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        int facturasMigradasCliente = 0;
        foreach (var fDto in clienteDto.Facturas)
        {
            try
            {
                var secFactura = !string.IsNullOrWhiteSpace(fDto.Secuencial) ? fDto.Secuencial.Trim() : "000000001";
                if (secFactura.Length < 9 && int.TryParse(secFactura, out var secNum))
                {
                    secFactura = secNum.ToString("D9");
                }

                var estabFactura = !string.IsNullOrWhiteSpace(fDto.Establecimiento) ? fDto.Establecimiento.Trim() : emisor.CodigoEstablecimiento;
                var ptoFactura = !string.IsNullOrWhiteSpace(fDto.PuntoEmision) ? fDto.PuntoEmision.Trim() : emisor.PuntoEmision;
                var numCompuesto = $"{estabFactura}-{ptoFactura}-{secFactura}";

                // Evitar duplicados por clave de acceso o por numeración
                if (!string.IsNullOrWhiteSpace(fDto.ClaveAcceso) && facturasExistentesClaves.Contains(fDto.ClaveAcceso.Trim()))
                {
                    continue;
                }

                if (facturasExistentesNumeracion.Contains(numCompuesto))
                {
                    continue;
                }

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

                var fechaEmisionFactura = fDto.FechaEmision > DateTime.MinValue ? fDto.FechaEmision : DateTime.UtcNow;

                var factura = Factura.Crear(
                    tenantId: tenantId,
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
                    facturasExistentesClaves.Add(fDto.ClaveAcceso.Trim());
                }

                facturasExistentesNumeracion.Add(numCompuesto);

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

        // 4. Ajustar correlativo de factura en el emisor si es menor al histórico
        int maxSecuencialHist = clienteDto.UltimoSecuencialFactura;
        foreach (var f in clienteDto.Facturas)
        {
            if (int.TryParse(f.Secuencial, out var s) && s > maxSecuencialHist)
            {
                maxSecuencialHist = s;
            }
        }

        if (maxSecuencialHist >= emisor.SecuencialFactura)
        {
            emisor.ActualizarDatosTributarios(
                ruc: emisor.Ruc,
                razonSocial: emisor.RazonSocial,
                direccionMatriz: emisor.DireccionMatriz,
                nombreComercial: emisor.NombreComercial,
                direccionEstablecimiento: emisor.DireccionEstablecimiento,
                codigoEstablecimiento: emisor.CodigoEstablecimiento,
                puntoEmision: emisor.PuntoEmision,
                ambiente: emisor.Ambiente,
                obligadoContabilidad: emisor.ObligadoContabilidad,
                regimenRimpe: emisor.RegimenRimpe,
                contribuyenteEspecial: emisor.ContribuyenteEspecial,
                secuencialInicial: maxSecuencialHist + 1);

            await _context.SaveChangesAsync(cancellationToken);
        }
        */
    }
}
