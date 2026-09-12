using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Data.Interceptors;
using BillingSaaS.Infrastructure.Identity;
using BillingSaaS.Infrastructure.Servicios;
using BillingSaaS.Infrastructure.Servicios.Sri;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BillingSaaS.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlite(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });


        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        // Servicios de Facturación y Firma SRI
        builder.Services.AddTransient<IFacturaXmlGenerator, FacturaXmlGenerator>();
        builder.Services.AddTransient<ISriSignatureService, SriSignatureService>();

        // Clientes SOAP SRI con HttpClient tipado
        builder.Services.AddHttpClient<ISriRecepcionService, SriRecepcionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
        });

        builder.Services.AddHttpClient<ISriAutorizacionService, SriAutorizacionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
        });
    }
}
