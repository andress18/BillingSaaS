using System.Linq;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Shared.Pdf;

namespace BillingSaaS.Infrastructure.Common;

public static class PdfMappingExtensions
{
    public static EmisorPdfDto ToPdfDto(this Emisor emisor)
    {
        return new EmisorPdfDto
        {
            RazonSocial = emisor.RazonSocial,
            NombreComercial = emisor.NombreComercial,
            Ruc = emisor.Ruc,
            DireccionMatriz = emisor.DireccionMatriz,
            DireccionEstablecimiento = emisor.DireccionEstablecimiento,
            ContribuyenteEspecial = emisor.ContribuyenteEspecial,
            ObligadoContabilidad = emisor.ObligadoContabilidad ? "SI" : "NO",
            RegimenRimpe = emisor.RegimenRimpe
        };
    }

    public static CompradorPdfDto ToPdfDto(this Comprador comprador)
    {
        return new CompradorPdfDto
        {
            RazonSocial = comprador.RazonSocial,
            Identificacion = comprador.Identificacion,
            Direccion = comprador.Direccion,
            CorreoElectronico = comprador.CorreoElectronico
        };
    }

    public static FacturaPdfRequestDto ToPdfRequest(
        this Factura factura,
        Emisor? emisor = null,
        string? logoBase64 = null,
        string? rucProveedor = null,
        string? nombreProveedor = null)
    {
        return new FacturaPdfRequestDto
        {
            Establecimiento = factura.Establecimiento,
            PuntoEmision = factura.PuntoEmision,
            Secuencial = factura.Secuencial,
            ClaveAcceso = factura.ClaveAcceso ?? string.Empty,
            NumeroAutorizacion = factura.NumeroAutorizacion,
            FechaAutorizacion = factura.FechaAutorizacion,
            Ambiente = factura.Ambiente,
            FechaEmision = factura.FechaEmision,
            RazonSocialEmisor = emisor?.RazonSocial ?? factura.RazonSocial,
            RucEmisor = emisor?.Ruc ?? factura.Ruc,
            DireccionMatrizEmisor = emisor?.DireccionMatriz ?? factura.DireccionMatriz,
            RegimenRimpeEmisor = emisor?.RegimenRimpe ?? factura.ContribuyenteRimpe,
            TotalSinImpuestos = factura.TotalSinImpuestos,
            TotalDescuento = factura.TotalDescuento,
            ImporteTotal = factura.ImporteTotal,
            Cliente = factura.Cliente != null ? factura.Cliente.ToPdfDto() : (factura.RazonSocialComprador != null ? new CompradorPdfDto
            {
                RazonSocial = factura.RazonSocialComprador,
                Identificacion = factura.IdentificacionComprador
            } : null),
            Emisor = emisor?.ToPdfDto(),
            Detalles = factura.Detalles.Select(d => new DetallePdfDto
            {
                CodigoPrincipal = d.CodigoPrincipal,
                Descripcion = d.Descripcion,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Descuento = d.Descuento,
                PrecioTotalSinImpuesto = d.PrecioTotalSinImpuesto,
                Impuestos = d.Impuestos.Select(i => new ImpuestoPdfDto
                {
                    Codigo = i.Codigo,
                    CodigoPorcentaje = i.CodigoPorcentaje,
                    Tarifa = i.Tarifa,
                    BaseImponible = i.BaseImponible,
                    Valor = i.Valor
                }).ToList()
            }).ToList(),
            CamposAdicionales = factura.CamposAdicionales?
                .Select(c => new CampoAdicionalPdfDto(c.Nombre, c.Valor))
                .ToList() ?? [],
            FormaPago = factura.FormaPago ?? "01",
            Plazo = factura.Plazo,
            UnidadTiempo = factura.UnidadTiempo,
            LogoBase64 = logoBase64,
            RucProveedor = rucProveedor,
            NombreProveedor = nombreProveedor
        };
    }

    public static NotaCreditoPdfRequestDto ToPdfRequest(
        this NotaCredito notaCredito,
        Emisor? emisor = null,
        string? logoBase64 = null,
        string? rucProveedor = null,
        string? nombreProveedor = null)
    {
        return new NotaCreditoPdfRequestDto
        {
            Establecimiento = notaCredito.Establecimiento,
            PuntoEmision = notaCredito.PuntoEmision,
            Secuencial = notaCredito.Secuencial,
            ClaveAcceso = notaCredito.ClaveAcceso ?? string.Empty,
            NumeroAutorizacion = notaCredito.NumeroAutorizacion,
            FechaAutorizacion = notaCredito.FechaAutorizacion,
            Ambiente = notaCredito.Ambiente,
            FechaEmision = notaCredito.FechaEmision,
            CodDocModificado = notaCredito.CodDocModificado,
            NumDocModificado = notaCredito.NumDocModificado,
            FechaEmisionDocSustento = notaCredito.FechaEmisionDocSustento,
            Motivo = notaCredito.Motivo,
            TotalSinImpuestos = notaCredito.TotalSinImpuestos,
            TotalDescuento = notaCredito.TotalDescuento,
            ValorModificacion = notaCredito.ValorModificacion,
            Cliente = notaCredito.Cliente != null ? notaCredito.Cliente.ToPdfDto() : null,
            Emisor = emisor?.ToPdfDto(),
            Detalles = notaCredito.Detalles.Select(d => new DetallePdfDto
            {
                CodigoPrincipal = d.CodigoPrincipal,
                Descripcion = d.Descripcion,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Descuento = d.Descuento,
                PrecioTotalSinImpuesto = d.PrecioTotalSinImpuesto,
                Impuestos = d.Impuestos.Select(i => new ImpuestoPdfDto
                {
                    Codigo = i.Codigo,
                    CodigoPorcentaje = i.CodigoPorcentaje,
                    Tarifa = i.Tarifa,
                    BaseImponible = i.BaseImponible,
                    Valor = i.Valor
                }).ToList()
            }).ToList(),
            CamposAdicionales = [],
            LogoBase64 = logoBase64,
            RucProveedor = rucProveedor,
            NombreProveedor = nombreProveedor
        };
    }

    public static NotaDebitoPdfRequestDto ToPdfRequest(
        this NotaDebito notaDebito,
        Emisor? emisor = null,
        string? logoBase64 = null,
        string? rucProveedor = null,
        string? nombreProveedor = null)
    {
        return new NotaDebitoPdfRequestDto
        {
            Establecimiento = notaDebito.Establecimiento,
            PuntoEmision = notaDebito.PuntoEmision,
            Secuencial = notaDebito.Secuencial,
            ClaveAcceso = notaDebito.ClaveAcceso ?? string.Empty,
            NumeroAutorizacion = notaDebito.NumeroAutorizacion,
            FechaAutorizacion = notaDebito.FechaAutorizacion,
            Ambiente = notaDebito.Ambiente,
            FechaEmision = notaDebito.FechaEmision,
            CodDocModificado = notaDebito.CodDocModificado,
            NumDocModificado = notaDebito.NumDocModificado,
            FechaEmisionDocSustento = notaDebito.FechaEmisionDocSustento,
            TotalSinImpuestos = notaDebito.TotalSinImpuestos,
            ValorTotal = notaDebito.ValorTotal,
            Cliente = notaDebito.Cliente != null ? notaDebito.Cliente.ToPdfDto() : null,
            Emisor = emisor?.ToPdfDto(),
            Motivos = notaDebito.Motivos.Select(m => new MotivoDebitoPdfDto(m.Razon, m.Valor)).ToList(),
            Impuestos = notaDebito.Impuestos.Select(i => new ImpuestoPdfDto
            {
                Codigo = i.Codigo,
                CodigoPorcentaje = i.CodigoPorcentaje,
                Tarifa = i.Tarifa,
                BaseImponible = i.BaseImponible,
                Valor = i.Valor
            }).ToList(),
            Pagos = notaDebito.Pagos.Select(p => new PagoDebitoPdfDto(p.FormaPago, p.Total, p.Plazo, p.UnidadTiempo)).ToList(),
            CamposAdicionales = [],
            LogoBase64 = logoBase64,
            RucProveedor = rucProveedor,
            NombreProveedor = nombreProveedor
        };
    }
}
