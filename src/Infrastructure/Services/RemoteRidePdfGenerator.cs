using System.Net.Http.Json;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Common;
using BillingSaaS.Shared.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Services;

public class RemoteRidePdfGenerator : IRidePdfGenerator
{
    private readonly HttpClient _httpClient;
    private readonly RidePdfGenerator _localGenerator;
    private readonly ILogger<RemoteRidePdfGenerator> _logger;
    private readonly string? _pdfServiceUrl;
    private readonly string? _functionKey;
    private readonly string _rucProveedor;
    private readonly string _nombreProveedor;

    public RemoteRidePdfGenerator(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RemoteRidePdfGenerator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rucProveedor = configuration["ProveedorFacturacion:Ruc"] ?? "0957790108001";
        _nombreProveedor = configuration["ProveedorFacturacion:Nombre"] ?? "Factura Fácil";
        _localGenerator = new RidePdfGenerator(_rucProveedor, _nombreProveedor);

        var url = configuration["AzureFunctions:PdfServiceUrl"]?.TrimEnd('/');
        _pdfServiceUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        _functionKey = configuration["AzureFunctions:FunctionKey"]?.Trim();
    }

    public byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null)
    {
        return GenerarFacturaRide(factura, emisor: null, logoBase64);
    }

    public byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(factura);
        var request = factura.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor);
        return GenerarFacturaRide(request);
    }

    public byte[] GenerarFacturaRide(FacturaPdfRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(_pdfServiceUrl))
        {
            return _localGenerator.GenerarFacturaRide(request);
        }

        try
        {
            var url = $"{_pdfServiceUrl}/api/pdf/factura";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(_functionKey))
            {
                httpRequest.Headers.Add("x-functions-key", _functionKey);
            }

            var response = _httpClient.Send(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                using var stream = response.Content.ReadAsStream();
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }

            _logger.LogWarning("Azure Function retornó status {StatusCode} al generar RIDE Factura. Ejecutando fallback local.", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo de comunicación con Azure Function PDF ({Url}). Ejecutando fallback local.", _pdfServiceUrl);
        }

        return _localGenerator.GenerarFacturaRide(request);
    }

    public byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, string? logoBase64 = null)
    {
        return GenerarNotaDebitoRide(notaDebito, emisor: null, logoBase64);
    }

    public byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(notaDebito);
        var request = notaDebito.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor);
        return GenerarNotaDebitoRide(request);
    }

    public byte[] GenerarNotaDebitoRide(NotaDebitoPdfRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(_pdfServiceUrl))
        {
            return _localGenerator.GenerarNotaDebitoRide(request);
        }

        try
        {
            var url = $"{_pdfServiceUrl}/api/pdf/notadebito";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(_functionKey))
            {
                httpRequest.Headers.Add("x-functions-key", _functionKey);
            }

            var response = _httpClient.Send(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                using var stream = response.Content.ReadAsStream();
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }

            _logger.LogWarning("Azure Function retornó status {StatusCode} al generar RIDE Nota de Débito. Ejecutando fallback local.", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo de comunicación con Azure Function PDF ({Url}). Ejecutando fallback local.", _pdfServiceUrl);
        }

        return _localGenerator.GenerarNotaDebitoRide(request);
    }

    public byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, string? logoBase64 = null)
    {
        return GenerarNotaCreditoRide(notaCredito, emisor: null, logoBase64);
    }

    public byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(notaCredito);
        var request = notaCredito.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor);
        return GenerarNotaCreditoRide(request);
    }

    public byte[] GenerarNotaCreditoRide(NotaCreditoPdfRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(_pdfServiceUrl))
        {
            return _localGenerator.GenerarNotaCreditoRide(request);
        }

        try
        {
            var url = $"{_pdfServiceUrl}/api/pdf/notacredito";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(_functionKey))
            {
                httpRequest.Headers.Add("x-functions-key", _functionKey);
            }

            var response = _httpClient.Send(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                using var stream = response.Content.ReadAsStream();
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }

            _logger.LogWarning("Azure Function retornó status {StatusCode} al generar RIDE Nota de Crédito. Ejecutando fallback local.", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo de comunicación con Azure Function PDF ({Url}). Ejecutando fallback local.", _pdfServiceUrl);
        }

        return _localGenerator.GenerarNotaCreditoRide(request);
    }
}
