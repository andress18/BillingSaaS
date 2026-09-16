using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IRidePdfGenerator
{
    byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null);
    byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null);
}

