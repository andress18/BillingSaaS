using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Identity;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
            }
            else
            {
                await _context.Database.EnsureCreatedAsync();

                if (_context.Database.IsSqlite())
                {
                    try
                    {
                        var connection = _context.Database.GetDbConnection();
                        bool wasClosed = connection.State == System.Data.ConnectionState.Closed;
                        if (wasClosed) await connection.OpenAsync();

                        try
                        {
                            using var cmd = connection.CreateCommand();
                            cmd.CommandText = "PRAGMA table_info(Facturas);";
                            var columnasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    columnasExistentes.Add(reader.GetString(1));
                                }
                            }

                            if (!columnasExistentes.Contains("XmlFirmado"))
                            {
                                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Facturas ADD COLUMN XmlFirmado TEXT;");
                            }

                            if (!columnasExistentes.Contains("ContribuyenteRimpe"))
                            {
                                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Facturas ADD COLUMN ContribuyenteRimpe TEXT;");
                            }
                        }
                        finally
                        {
                            if (wasClosed) await connection.CloseAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudieron verificar/migrar columnas adicionales en Facturas.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // 1. Roles por defecto
        var administratorRole = new IdentityRole(Roles.Administrator);
        if (_roleManager.Roles.All(r => r.Name != administratorRole.Name))
        {
            await _roleManager.CreateAsync(administratorRole);
        }

        var partnerRole = new IdentityRole(Roles.Partner);
        if (_roleManager.Roles.All(r => r.Name != partnerRole.Name))
        {
            await _roleManager.CreateAsync(partnerRole);
        }

        // IDs canónicos de Tenants
        var adminTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var partnerTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var demoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // 2. Tenants Oficiales
        // 2.1 Administrador Central
        var adminTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == adminTenantId);
        if (adminTenant == null)
        {
            adminTenant = Tenant.Crear(
                nombre: "ADMINISTRACION SAAS CENTRAL",
                partnerId: null,
                id: adminTenantId
            );
            _context.Tenants.Add(adminTenant);
        }

        // 2.2 Partner Oficial: "Gorky"
        var partnerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == partnerTenantId);
        if (partnerTenant == null)
        {
            partnerTenant = Tenant.Crear(
                nombre: "Gorky",
                partnerId: null,
                id: partnerTenantId
            );
            _context.Tenants.Add(partnerTenant);
        }

        // 2.3 Cliente Demo (Solo en ambiente local SQLite para pruebas)
        if (_context.Database.IsSqlite())
        {
            var demoTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == demoTenantId);
            if (demoTenant == null)
            {
                demoTenant = Tenant.Crear(
                    nombre: "PRUEBAS SAAS FACTURACION",
                    partnerId: partnerTenantId,
                    id: demoTenantId
                );
                _context.Tenants.Add(demoTenant);
            }
        }

        await _context.SaveChangesAsync();

        // 3. Usuarios Oficiales
        // 3.1 Super Administrador
        var existingAdmin = await _userManager.FindByNameAsync("administrator@localhost");
        if (existingAdmin == null)
        {
            var administrator = new ApplicationUser
            {
                UserName = "administrator@localhost",
                Email = "administrator@localhost",
                TenantId = adminTenantId
            };
            await _userManager.CreateAsync(administrator, "Administrator1!");
            await _userManager.AddToRoleAsync(administrator, Roles.Administrator);
        }
        else if (existingAdmin.TenantId != adminTenantId)
        {
            existingAdmin.TenantId = adminTenantId;
            await _userManager.UpdateAsync(existingAdmin);
        }

        // 3.2 Partner Oficial (Usuario: Serconkpo)
        var oldPartner = await _userManager.FindByNameAsync("partner@localhost");
        if (oldPartner != null)
        {
            await _userManager.DeleteAsync(oldPartner);
        }

        var existingPartner = await _userManager.FindByNameAsync("Serconkpo");
        if (existingPartner == null)
        {
            var partnerUser = new ApplicationUser
            {
                UserName = "Serconkpo",
                Email = "serconkpo@localhost",
                TenantId = partnerTenantId
            };
            await _userManager.CreateAsync(partnerUser, "Partner123!");
            await _userManager.AddToRoleAsync(partnerUser, Roles.Partner);
        }
        else if (existingPartner.TenantId != partnerTenantId)
        {
            existingPartner.TenantId = partnerTenantId;
            await _userManager.UpdateAsync(existingPartner);
        }

        // 3.3 Cliente Demo (Solo en ambiente local SQLite)
        if (_context.Database.IsSqlite())
        {
            var existingCliente = await _userManager.FindByNameAsync("cliente@localhost");
            if (existingCliente == null)
            {
                var clienteUser = new ApplicationUser
                {
                    UserName = "cliente@localhost",
                    Email = "cliente@localhost",
                    TenantId = demoTenantId
                };
                await _userManager.CreateAsync(clienteUser, "Cliente123!");
            }
            else if (existingCliente.TenantId != demoTenantId)
            {
                existingCliente.TenantId = demoTenantId;
                await _userManager.UpdateAsync(existingCliente);
            }
        }

        // 4. Catálogo de Planes Oficiales
        var planMigracionSistema = await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == "MIGRACION_SISTEMA");
        if (planMigracionSistema == null)
        {
            planMigracionSistema = Plan.Crear(
                codigo: "MIGRACION_SISTEMA",
                nombre: "Plan Migración (Solo Sistema)",
                descripcion: "Uso del sistema para clientes que ya disponen de su firma electrónica (.p12) activa.",
                precioMensual: 3.00m,
                precioAnual: 20.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: null,
                maxEstablecimientos: 1,
                tiposDocumentosPermitidos: "01,04",
                esPublico: true
            );
            _context.Planes.Add(planMigracionSistema);
        }
        else
        {
            planMigracionSistema.ActualizarPrecios(3.00m, 20.00m);
        }

        var planMigracionFirma = await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == "MIGRACION_FIRMA");
        if (planMigracionFirma == null)
        {
            planMigracionFirma = Plan.Crear(
                codigo: "MIGRACION_FIRMA",
                nombre: "Plan Migración + Firma Digital",
                descripcion: "Uso del sistema e incluye trámite y emisión de firma digital .p12 ($15 sistema + $30 firma anual).",
                precioMensual: 5.00m,
                precioAnual: 45.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: null,
                maxEstablecimientos: 1,
                tiposDocumentosPermitidos: "01,04",
                esPublico: true
            );
            _context.Planes.Add(planMigracionFirma);
        }
        else
        {
            planMigracionFirma.ActualizarPrecios(5.00m, 45.00m);
        }

        var planCortesiaPartner = await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == "PLAN_CORTESIA_PARTNER");
        if (planCortesiaPartner == null)
        {
            planCortesiaPartner = Plan.Crear(
                codigo: "PLAN_CORTESIA_PARTNER",
                nombre: "Plan Partner Cortesía",
                descripcion: "Plan de cortesía para agencias y socios estratégicos que gestionan clientes.",
                precioMensual: 0.00m,
                precioAnual: 0.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: null,
                maxEstablecimientos: 5,
                tiposDocumentosPermitidos: "01,04",
                esPublico: false
            );
            _context.Planes.Add(planCortesiaPartner);
        }

        var planAdminSistema = await _context.Planes.FirstOrDefaultAsync(p => p.Codigo == "PLAN_ADMIN_SISTEMA");
        if (planAdminSistema == null)
        {
            planAdminSistema = Plan.Crear(
                codigo: "PLAN_ADMIN_SISTEMA",
                nombre: "Plan Administración Central",
                descripcion: "Facturación interna y cobros de licencias del SaaS.",
                precioMensual: 0.00m,
                precioAnual: 0.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: null,
                maxEstablecimientos: 10,
                tiposDocumentosPermitidos: "01,04",
                esPublico: false
            );
            _context.Planes.Add(planAdminSistema);
        }

        // Desactivar cualquier otro plan obsoleto
        var codigosValidos = new[] { "MIGRACION_SISTEMA", "MIGRACION_FIRMA", "PLAN_CORTESIA_PARTNER", "PLAN_ADMIN_SISTEMA" };
        var otrosPlanes = await _context.Planes
            .Where(p => !codigosValidos.Contains(p.Codigo) && p.Activo)
            .ToListAsync();
        foreach (var p in otrosPlanes)
        {
            p.Desactivar();
        }

        await _context.SaveChangesAsync();

        // 5. Emisores Oficiales (Sin certificado digital por defecto: cada cliente/partner sube su .p12 vía UI)
        // 5.1 Emisor Administrador Central
        var emisorAdmin = await _context.Emisores.FirstOrDefaultAsync(e => e.TenantId == adminTenantId);
        if (emisorAdmin == null)
        {
            emisorAdmin = Emisor.Crear(
                tenantId: adminTenantId,
                ruc: "1790000000001",
                razonSocial: "ADMINISTRACION SAAS CENTRAL S.A.S.",
                direccionMatriz: "Quito - Ecuador",
                codigoEstablecimiento: "001",
                puntoEmision: "001",
                ambiente: 1,
                obligadoContabilidad: true,
                nombreComercial: "BILLING SAAS ECUADOR",
                regimenRimpe: "CONTRIBUYENTE RÉGIMEN GENERAL",
                secuencialInicial: 0
            );
            _context.Emisores.Add(emisorAdmin);
        }

        // 5.2 Emisor Partner Oficial: "Gorky"
        var emisorPartner = await _context.Emisores.FirstOrDefaultAsync(e => e.TenantId == partnerTenantId);
        if (emisorPartner == null)
        {
            emisorPartner = Emisor.Crear(
                tenantId: partnerTenantId,
                ruc: "1792222222001",
                razonSocial: "Gorky",
                direccionMatriz: "Quito - Ecuador",
                codigoEstablecimiento: "001",
                puntoEmision: "001",
                ambiente: 1,
                obligadoContabilidad: true,
                nombreComercial: "Gorky",
                regimenRimpe: "CONTRIBUYENTE RÉGIMEN GENERAL",
                secuencialInicial: 0
            );
            _context.Emisores.Add(emisorPartner);
        }
        else
        {
            emisorPartner.ActualizarDatosTributarios(
                ruc: "0957790108001",
                razonSocial: "Gorky",
                direccionMatriz: emisorPartner.DireccionMatriz,
                nombreComercial: "Gorky",
                direccionEstablecimiento: emisorPartner.DireccionEstablecimiento,
                codigoEstablecimiento: emisorPartner.CodigoEstablecimiento,
                puntoEmision: emisorPartner.PuntoEmision,
                ambiente: emisorPartner.Ambiente,
                obligadoContabilidad: emisorPartner.ObligadoContabilidad,
                regimenRimpe: emisorPartner.RegimenRimpe,
                contribuyenteEspecial: emisorPartner.ContribuyenteEspecial
            );
        }

        // 5.3 Emisor Demo (Solo en SQLite, garantizando certificado en blanco)
        if (_context.Database.IsSqlite())
        {
            var emisorDemo = await _context.Emisores.FirstOrDefaultAsync(e => e.TenantId == demoTenantId);
            if (emisorDemo == null)
            {
                emisorDemo = Emisor.Crear(
                    tenantId: demoTenantId,
                    ruc: "0957790108001",
                    razonSocial: "PRUEBAS SAAS FACTURACION",
                    direccionMatriz: "Quito - Ecuador",
                    codigoEstablecimiento: "001",
                    puntoEmision: "001",
                    ambiente: 1,
                    obligadoContabilidad: false,
                    nombreComercial: "FACTURACION SAAS DEMO",
                    regimenRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE",
                    secuencialInicial: 0
                );
                _context.Emisores.Add(emisorDemo);
            }
            else if (emisorDemo.TieneCertificadoValido())
            {
                // Purga cualquier certificado de prueba residual
                emisorDemo.EliminarCertificado();
            }
        }

        // 6. Suscripciones Activas
        // 6.1 Administrador Central
        if (!await _context.Suscripciones.AnyAsync(s => s.TenantId == adminTenantId))
        {
            var suscripcionAdmin = TenantSubscription.Crear(
                tenantId: adminTenantId,
                planId: planAdminSistema.Id,
                fechaInicio: DateTime.UtcNow.AddMonths(-1),
                fechaVencimiento: DateTime.UtcNow.AddYears(10),
                frecuencia: "ANUAL",
                diasGracia: 3
            );
            _context.Suscripciones.Add(suscripcionAdmin);
        }

        // 6.2 Partner Oficial ("Gorky")
        if (!await _context.Suscripciones.AnyAsync(s => s.TenantId == partnerTenantId))
        {
            var suscripcionPartner = TenantSubscription.Crear(
                tenantId: partnerTenantId,
                planId: planCortesiaPartner.Id,
                fechaInicio: DateTime.UtcNow.AddMonths(-1),
                fechaVencimiento: DateTime.UtcNow.AddYears(5),
                frecuencia: "ANUAL",
                diasGracia: 3
            );
            _context.Suscripciones.Add(suscripcionPartner);
        }

        // 6.3 Cliente Demo (Solo en SQLite)
        if (_context.Database.IsSqlite() && !await _context.Suscripciones.AnyAsync(s => s.TenantId == demoTenantId))
        {
            var suscripcionDemo = TenantSubscription.Crear(
                tenantId: demoTenantId,
                planId: planMigracionSistema.Id,
                fechaInicio: DateTime.UtcNow.AddMonths(-1),
                fechaVencimiento: DateTime.UtcNow.AddMonths(11),
                frecuencia: "ANUAL",
                diasGracia: 3
            );
            _context.Suscripciones.Add(suscripcionDemo);
        }

        await _context.SaveChangesAsync();
    }
}
