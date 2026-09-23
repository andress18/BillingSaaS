namespace BillingSaaS.Application.Common.Interfaces;

/// <summary>
/// Servicio para el envío transaccional de comprobantes electrónicos a los compradores/clientes.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un correo electrónico con el RIDE (PDF) y el XML firmado y autorizado adjuntos.
    /// </summary>
    Task SendFacturaEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroFactura,
        string razonSocialEmisor,
        string claveAcceso,
        decimal importeTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default);

    Task SendNotaDebitoEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroNotaDebito,
        string razonSocialEmisor,
        string claveAcceso,
        decimal valorTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default);

    Task SendNotaCreditoEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroNotaCredito,
        string razonSocialEmisor,
        string claveAcceso,
        decimal valorTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default);
}

