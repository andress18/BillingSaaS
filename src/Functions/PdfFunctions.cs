using BillingSaaS.Infrastructure.Services;
using BillingSaaS.Shared.Pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Functions;

public class PdfFunctions
{
    private readonly ILogger<PdfFunctions> _logger;
    private readonly RidePdfGenerator _pdfGenerator;

    public PdfFunctions(ILogger<PdfFunctions> logger)
    {
        _logger = logger;
        _pdfGenerator = new RidePdfGenerator();
    }

    [Function("GenerarFacturaPdf")]
    public async Task<IActionResult> GenerarFacturaPdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pdf/factura")] HttpRequest req)
    {
        _logger.LogInformation("Procesando generación de Factura RIDE en Azure Function.");
        var request = await req.ReadFromJsonAsync<FacturaPdfRequestDto>();
        if (request == null)
        {
            return new BadRequestObjectResult("El payload de la factura es requerido.");
        }

        var pdfBytes = _pdfGenerator.GenerarFacturaRide(request);
        var fileName = $"FACTURA_{request.Establecimiento}_{request.PuntoEmision}_{request.Secuencial}.pdf";
        return new FileContentResult(pdfBytes, "application/pdf")
        {
            FileDownloadName = fileName
        };
    }

    [Function("GenerarNotaCreditoPdf")]
    public async Task<IActionResult> GenerarNotaCreditoPdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pdf/notacredito")] HttpRequest req)
    {
        _logger.LogInformation("Procesando generación de Nota de Crédito RIDE en Azure Function.");
        var request = await req.ReadFromJsonAsync<NotaCreditoPdfRequestDto>();
        if (request == null)
        {
            return new BadRequestObjectResult("El payload de la nota de crédito es requerido.");
        }

        var pdfBytes = _pdfGenerator.GenerarNotaCreditoRide(request);
        var fileName = $"NC_{request.Establecimiento}_{request.PuntoEmision}_{request.Secuencial}.pdf";
        return new FileContentResult(pdfBytes, "application/pdf")
        {
            FileDownloadName = fileName
        };
    }

    [Function("GenerarNotaDebitoPdf")]
    public async Task<IActionResult> GenerarNotaDebitoPdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pdf/notadebito")] HttpRequest req)
    {
        _logger.LogInformation("Procesando generación de Nota de Débito RIDE en Azure Function.");
        var request = await req.ReadFromJsonAsync<NotaDebitoPdfRequestDto>();
        if (request == null)
        {
            return new BadRequestObjectResult("El payload de la nota de débito es requerido.");
        }

        var pdfBytes = _pdfGenerator.GenerarNotaDebitoRide(request);
        var fileName = $"ND_{request.Establecimiento}_{request.PuntoEmision}_{request.Secuencial}.pdf";
        return new FileContentResult(pdfBytes, "application/pdf")
        {
            FileDownloadName = fileName
        };
    }

    [Function("PdfHealthCheck")]
    public IActionResult HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pdf/health")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "Healthy",
            service = "BillingSaaS.Functions.PdfGenerator",
            timestamp = DateTime.UtcNow
        });
    }
}
