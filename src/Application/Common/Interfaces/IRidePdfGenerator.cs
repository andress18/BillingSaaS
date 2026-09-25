using BillingSaaS.Domain.Entities;
using BillingSaaS.Shared.Pdf;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IRidePdfGenerator
{
    byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null);
    byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null);
    byte[] GenerarFacturaRide(FacturaPdfRequestDto request);

    byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, string? logoBase64 = null);
    byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, Emisor? emisor, string? logoBase64 = null);
    byte[] GenerarNotaDebitoRide(NotaDebitoPdfRequestDto request);

    byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, string? logoBase64 = null);
    byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, Emisor? emisor, string? logoBase64 = null);
    byte[] GenerarNotaCreditoRide(NotaCreditoPdfRequestDto request);
}
