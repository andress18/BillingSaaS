using System.Text.Json;
using BillingSaaS.MigrationTool.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillingSaaS.MigrationTool.Services;

public class JsonFileMigracionSource : IClienteMigracionSource
{
    private readonly MigrationOptions _options;
    private readonly IXmlFacturaParser _xmlParser;
    private readonly ILogger<JsonFileMigracionSource> _logger;

    public JsonFileMigracionSource(
        IOptions<MigrationOptions> options,
        IXmlFacturaParser xmlParser,
        ILogger<JsonFileMigracionSource> logger)
    {
        _options = options.Value;
        _xmlParser = xmlParser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClienteMigracionDto>> ObtenerClientesAsync(CancellationToken cancellationToken = default)
    {
        var filePath = !string.IsNullOrWhiteSpace(_options.JsonFilePath)
            ? _options.JsonFilePath
            : "clientes_ejemplo.json";

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("El archivo JSON de migración '{Path}' no existe.", filePath);
            return Array.Empty<ClienteMigracionDto>();
        }

        _logger.LogInformation("Cargando clientes desde archivo JSON: '{Path}'", filePath);

        var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var clientes = JsonSerializer.Deserialize<List<ClienteMigracionDto>>(jsonContent, jsonOptions) ?? new();

        _logger.LogInformation("Se leyeron {Count} clientes desde el archivo JSON.", clientes.Count);

        // Procesar y enriquecer cada cliente
        foreach (var cliente in clientes)
        {
            if (cliente.PartnerId == Guid.Empty)
            {
                cliente.PartnerId = _options.PartnerId;
            }

            // Parsear XMLs de facturas si tienen XmlOriginal/XmlFirmado pero no detalles
            for (int i = 0; i < cliente.Facturas.Count; i++)
            {
                var f = cliente.Facturas[i];
                var xml = !string.IsNullOrWhiteSpace(f.XmlFirmado) ? f.XmlFirmado : f.XmlOriginal;
                if (!string.IsNullOrWhiteSpace(xml) && (f.Detalles.Count == 0 || string.IsNullOrWhiteSpace(f.ClaveAcceso)))
                {
                    try
                    {
                        var parsed = _xmlParser.ParsearFacturaXml(xml, f.Estado);
                        // Conservar valores previos si ya estaban definidos
                        parsed.Estado = !string.IsNullOrWhiteSpace(f.Estado) ? f.Estado : parsed.Estado;
                        parsed.NumeroAutorizacion = !string.IsNullOrWhiteSpace(f.NumeroAutorizacion) ? f.NumeroAutorizacion : parsed.NumeroAutorizacion;
                        parsed.FechaAutorizacion = f.FechaAutorizacion ?? parsed.FechaAutorizacion;
                        parsed.MensajeErrorSri = f.MensajeErrorSri ?? parsed.MensajeErrorSri;
                        cliente.Facturas[i] = parsed;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al auto-parsear XML de factura para cliente {Email}", cliente.Email);
                    }
                }
            }

            // Enriquecer datos del emisor desde sus facturas si no vinieron explícitos
            _xmlParser.EnriquecerEmisorDesdeFacturas(cliente);
        }

        return clientes;
    }
}
