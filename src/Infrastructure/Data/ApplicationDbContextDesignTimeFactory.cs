using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BillingSaaS.Infrastructure.Data;

public class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string? connectionString = null;

        // 1. Argumentos de línea de comandos (ej: dotnet ef database update --connection "...")
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--connection" || args[i] == "-c") && i + 1 < args.Length)
            {
                connectionString = args[i + 1];
                break;
            }
        }

        // 2. Variable de entorno
        connectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__BillingSaaSDb");

        // 3. Archivos appsettings.Production.json / appsettings.json
        if (string.IsNullOrEmpty(connectionString))
        {
            var basePath = Directory.GetCurrentDirectory();
            var webPath = Path.Combine(basePath, "src", "Web");
            if (!Directory.Exists(webPath))
            {
                webPath = Path.Combine(basePath, "..", "Web");
            }

            var dir = Directory.Exists(webPath) ? webPath : basePath;

            var config = new ConfigurationBuilder()
                .SetBasePath(dir)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var prodConn = config.GetConnectionString("BillingSaaSDb");
            if (!string.IsNullOrEmpty(prodConn) && (prodConn.Contains("Server=") || prodConn.Contains("Initial Catalog=")))
            {
                connectionString = prodConn;
            }
        }

        // 4. Default fallback para creación de migraciones en caso de no haber config
        connectionString ??= "Server=127.0.0.1;Database=BillingSaaSDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString,
            sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
