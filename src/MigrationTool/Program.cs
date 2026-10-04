using System.Diagnostics;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Data.Interceptors;
using BillingSaaS.Infrastructure.Identity;
using BillingSaaS.Infrastructure.Security;
using BillingSaaS.MigrationTool.Models;
using BillingSaaS.MigrationTool.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.MigrationTool;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        MigrationConsoleLogger.PrintBanner();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Cargar configuración
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            // Enlazar opciones de migración
            builder.Services.Configure<MigrationOptions>(builder.Configuration.GetSection("Migration"));

            // Parsear parámetros de línea de comandos sobreescribiendo opciones si se especificaron
            var migrationOptions = builder.Configuration.GetSection("Migration").Get<MigrationOptions>() ?? new MigrationOptions();
            ParseCommandLineArgs(args, migrationOptions);
            builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(migrationOptions));

            // Configurar conexión a la base de datos destino (BillingSaaSDb)
            var connectionString = !string.IsNullOrWhiteSpace(migrationOptions.TargetConnectionString)
                ? migrationOptions.TargetConnectionString
                : (builder.Configuration.GetConnectionString(BillingSaaS.Shared.Services.Database)
                   ?? builder.Configuration.GetConnectionString("Default"));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("CRÍTICO: No se encontró la cadena de conexión 'BillingSaaSDb' en appsettings.json o argumentos CLI.");
                Console.ResetColor();
                return 1;
            }

            var isSqlServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase);

            builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                if (isSqlServer)
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                        sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    });
                }
                else
                {
                    options.UseSqlite(connectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    });
                }
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<IUser, MigrationSystemUser>();
            builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Configurar ASP.NET Core Identity
            builder.Services
                .AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddUserManager<ApplicationUserManager>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

            // Servicio criptográfico (AES-256-GCM)
            builder.Services.AddSingleton<ICertificateEncryptionService, AesGcmCertificateEncryptionService>();

            // Servicios auxiliares de migración
            builder.Services.AddSingleton<IPartnerCsvFilterService, PartnerCsvFilterService>();
            builder.Services.AddSingleton<IXmlFacturaParser, SriXmlFacturaParser>();
            builder.Services.AddSingleton<ICertificateValidator, X509CertificateValidator>();
            builder.Services.AddScoped<JsonFileMigracionSource>();
            builder.Services.AddScoped<SqlLegacyDbSource>();
            builder.Services.AddScoped<IClienteMigrador, ClienteMigrador>();

            var host = builder.Build();

            using var scope = host.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var logger = sp.GetRequiredService<ILogger<Program>>();

            // 1. Asegurar base de datos y catálogo de planes mínimos requeridos
            await AsegurarBaseDeDatosYPlanesAsync(context, logger);

            // 2. Resolver origen de datos (SQL Database o JSON)
            IClienteMigracionSource source = migrationOptions.SourceType.Equals("Json", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<JsonFileMigracionSource>()
                : sp.GetRequiredService<SqlLegacyDbSource>();

            Console.WriteLine($"Origen de datos seleccionado: {migrationOptions.SourceType.ToUpperInvariant()}");
            Console.WriteLine($"Partner ID de asignación:   {migrationOptions.PartnerId}");
            if (!string.IsNullOrWhiteSpace(migrationOptions.FilterCsvPath))
            {
                Console.WriteLine($"Filtro CSV de clientes:     {migrationOptions.FilterCsvPath}");
            }
            Console.WriteLine();

            // 3. Obtener clientes a migrar
            var clientes = await source.ObtenerClientesAsync();
            if (clientes.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No se encontraron clientes para migrar. Verifique el origen de datos o archivo JSON.");
                Console.ResetColor();
                return 0;
            }

            if (migrationOptions.LimiteClientes > 0 && clientes.Count > migrationOptions.LimiteClientes)
            {
                clientes = clientes.Take(migrationOptions.LimiteClientes).ToList();
            }

            // 4. Ejecutar migración
            var migrador = sp.GetRequiredService<IClienteMigrador>();
            var resultado = await migrador.MigrarClientesAsync(clientes);

            stopwatch.Stop();

            // 5. Imprimir reporte final
            MigrationConsoleLogger.PrintSummaryReport(resultado, stopwatch.Elapsed);

            return resultado.ClientesConError > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine($"ERROR FATAL: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            return 1;
        }
    }

    private static void ParseCommandLineArgs(string[] args, MigrationOptions options)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if ((arg.Equals("--partner-id", StringComparison.OrdinalIgnoreCase) || arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                if (Guid.TryParse(args[i + 1], out var pId))
                {
                    options.PartnerId = pId;
                }
                i++;
            }
            else if ((arg.Equals("--source", StringComparison.OrdinalIgnoreCase) || arg.Equals("-s", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.SourceType = args[i + 1];
                i++;
            }
            else if ((arg.Equals("--file", StringComparison.OrdinalIgnoreCase) || arg.Equals("-f", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.JsonFilePath = args[i + 1];
                options.SourceType = "Json";
                i++;
            }
            else if (arg.Equals("--plan", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.PlanCodigoDefault = args[i + 1];
                i++;
            }
            else if (arg.Equals("--limit", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var limit))
                {
                    options.LimiteClientes = limit;
                }
                i++;
            }
            else if ((arg.Equals("--legacy-db", StringComparison.OrdinalIgnoreCase) || arg.Equals("--source-conn", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.LegacyConnectionString = args[i + 1];
                i++;
            }
            else if ((arg.Equals("--target-db", StringComparison.OrdinalIgnoreCase) || arg.Equals("--dest-conn", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.TargetConnectionString = args[i + 1];
                i++;
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
            }
        }
    }

    private static async Task AsegurarBaseDeDatosYPlanesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (context.Database.IsSqlServer())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Asegurar que exista el Plan MIGRACION_SISTEMA si no fue sembrado aún
        var planMigracion = await context.Planes.FirstOrDefaultAsync(p => p.Codigo == "MIGRACION_SISTEMA");
        if (planMigracion == null)
        {
            logger.LogInformation("Sembrando plan base 'MIGRACION_SISTEMA'...");
            planMigracion = Plan.Crear(
                codigo: "MIGRACION_SISTEMA",
                nombre: "Plan Migración (Solo Sistema)",
                descripcion: "Uso del sistema para clientes migrados con firma electrónica .p12 activa.",
                precioMensual: 3.00m,
                precioAnual: 20.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: 100,
                maxEstablecimientos: 1,
                tiposDocumentosPermitidos: "01,04,05",
                esPublico: true
            );
            context.Planes.Add(planMigracion);
            await context.SaveChangesAsync();
        }

        var planFirma = await context.Planes.FirstOrDefaultAsync(p => p.Codigo == "MIGRACION_FIRMA");
        if (planFirma == null)
        {
            planFirma = Plan.Crear(
                codigo: "MIGRACION_FIRMA",
                nombre: "Plan Migración + Firma Digital",
                descripcion: "Uso del sistema e incluye renovación de firma digital .p12.",
                precioMensual: 5.00m,
                precioAnual: 45.00m,
                maxDocumentosMensuales: null,
                maxDocumentosAnuales: 100,
                maxEstablecimientos: 1,
                tiposDocumentosPermitidos: "01,04,05",
                esPublico: true
            );
            context.Planes.Add(planFirma);
            await context.SaveChangesAsync();
        }
    }
}

public class MigrationSystemUser : IUser
{
    public string? Id => "MIGRATION_SYSTEM";
    public List<string>? Roles => ["Administrator"];
    public Guid? TenantId => null;
}
