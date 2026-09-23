using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Servicios.Sri;

public class SriConsultaRucService : ISriConsultaRucService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SriConsultaRucService> _logger;

    public SriConsultaRucService(HttpClient httpClient, ILogger<SriConsultaRucService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SriContribuyenteDto?> ConsultarPorIdentificacionAsync(string identificacion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identificacion))
            return null;

        identificacion = identificacion.Trim();
        var tipoIdentificacion = identificacion.Length == 13 ? "04" : "05";

        // Intento 1: Catastro Consolidado de Contribuyentes (RUC)
        try
        {
            var rucToQuery = identificacion.Length == 10 ? $"{identificacion}001" : identificacion;
            var url = $"https://srienlinea.sri.gob.ec/sri-catastro-sujeto-servicio-internet/rest/ConsolidadoContribuyente/existePorNumeroRuc?numeroRuc={rucToQuery}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var result = JsonSerializer.Deserialize<ConsolidadoSriResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null && !string.IsNullOrWhiteSpace(result.RazonSocial))
                    {
                        return new SriContribuyenteDto(
                            Identificacion: identificacion,
                            RazonSocial: result.RazonSocial.Trim(),
                            TipoIdentificacion: tipoIdentificacion,
                            Estado: result.Estado,
                            RegimenRimpe: null
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar catastro consolidado SRI para identificación {Identificacion}", identificacion);
        }

        // Intento 2: Servicio Móvil SRI deudas/identificación
        try
        {
            var url = $"https://srienlinea.sri.gob.ec/movil-servicios/api/v1.0/deudas/porIdentificacion/{identificacion}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var result = JsonSerializer.Deserialize<DeudasSriResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var razon = result?.Contribuyente?.RazonSocial ?? result?.Contribuyente?.NombreComercial;
                    if (!string.IsNullOrWhiteSpace(razon))
                    {
                        return new SriContribuyenteDto(
                            Identificacion: identificacion,
                            RazonSocial: razon.Trim(),
                            TipoIdentificacion: tipoIdentificacion,
                            Estado: "ACTIVO",
                            RegimenRimpe: null
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar servicio móvil SRI para identificación {Identificacion}", identificacion);
        }

        return null;
    }

    private class ConsolidadoSriResponse
    {
        [JsonPropertyName("numeroRuc")]
        public string? NumeroRuc { get; set; }

        [JsonPropertyName("razonSocial")]
        public string? RazonSocial { get; set; }

        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("claseContribuyente")]
        public string? ClaseContribuyente { get; set; }
    }

    private class DeudasSriResponse
    {
        [JsonPropertyName("contribuyente")]
        public ContribuyenteInfo? Contribuyente { get; set; }

        public class ContribuyenteInfo
        {
            [JsonPropertyName("identificacion")]
            public string? Identificacion { get; set; }

            [JsonPropertyName("razonSocial")]
            public string? RazonSocial { get; set; }

            [JsonPropertyName("nombreComercial")]
            public string? NombreComercial { get; set; }
        }
    }
}

