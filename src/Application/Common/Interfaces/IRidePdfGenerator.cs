using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IRidePdfGenerator
{
    byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null);
    byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null);
    byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, string? logoBase64 = null);
    byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, Emisor? emisor, string? logoBase64 = null);
    byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, string? logoBase64 = null);
    byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, Emisor? emisor, string? logoBase64 = null);
}

