using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Data.Interceptors;
using BillingSaaS.Infrastructure.Identity;
using BillingSaaS.Infrastructure.Security;
using BillingSaaS.Infrastructure.Services;
using BillingSaaS.Infrastructure.Servicios;
using BillingSaaS.Infrastructure.Servicios.Sri;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;

namespace BillingSaaS.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(BillingSaaS.Shared.Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{BillingSaaS.Shared.Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider");
        var isSqlServer = !string.IsNullOrEmpty(databaseProvider)
            ? databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            : (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase));

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            if (isSqlServer)
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
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
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });


        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
            options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            options.DefaultScheme = IdentityConstants.BearerScheme;
        })
        .AddBearerToken(IdentityConstants.BearerScheme);

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        // Servicios de Facturación y Firma SRI
        builder.Services.AddTransient<IFacturaXmlGenerator, FacturaXmlGenerator>();
        builder.Services.AddTransient<INotaDebitoXmlGenerator, NotaDebitoXmlGenerator>();
        builder.Services.AddTransient<ISriSignatureService, SriSignatureService>();

        // Generador de Representación Impresa (RIDE) en PDF (QuestPDF)
        QuestPDF.Settings.License = LicenseType.Community;
        builder.Services.AddTransient<IRidePdfGenerator, RidePdfGenerator>();

        // Servicio de Control de Planes y Suscripciones SaaS
        builder.Services.AddScoped<ISubscriptionValidationService, SubscriptionValidationService>();

        // Servicio criptográfico de custodia y cifrado en reposo para certificados digitales (LOPDP / OWASP / NIST SP 800-38D)
        builder.Services.AddSingleton<ICertificateEncryptionService, AesGcmCertificateEncryptionService>();

        // Servicio de Envío de Comprobantes por Correo (Brevo / SMTP con MailKit)
        builder.Services.AddTransient<IEmailService, MailKitEmailService>();

        // Clientes SOAP SRI con HttpClient tipado y protocolo TLS 1.2 explícito para servidores estatales
        builder.Services.AddHttpClient<ISriRecepcionService, SriRecepcionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = false
        });

        builder.Services.AddHttpClient<ISriAutorizacionService, SriAutorizacionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = false
        });

        // Servicio de Consulta Pública de Contribuyentes al SRI
        builder.Services.AddHttpClient<ISriConsultaRucService, SriConsultaRucService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        });
    }
}
