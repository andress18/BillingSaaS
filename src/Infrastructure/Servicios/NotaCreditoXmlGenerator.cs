using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Infrastructure.Servicios;

public class NotaCreditoXmlGenerator : INotaCreditoXmlGenerator
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly XmlWriterSettings IndentedSettings = new()
    {
        Encoding = Utf8NoBom,
        Indent = true,
        OmitXmlDeclaration = false
    };

    private readonly string _rucProveedor;
    private readonly string _nombreProveedor;

    public NotaCreditoXmlGenerator(string rucProveedor, string nombreProveedor)
    {
        _rucProveedor = rucProveedor;
        _nombreProveedor = nombreProveedor;
    }

    public XDocument GenerarXml(NotaCredito notaCredito)
    {
        ArgumentNullException.ThrowIfNull(notaCredito);

        var format = CultureInfo.InvariantCulture;

        // Agrupación de impuestos por código y porcentaje a nivel de cabecera
        var impuestosAgrupados = notaCredito.Detalles
            .SelectMany(d => d.Impuestos)
            .GroupBy(i => new { i.Codigo, i.CodigoPorcentaje })
            .Select(g => new
            {
                g.Key.Codigo,
                g.Key.CodigoPorcentaje,
                BaseImponible = g.Sum(i => i.BaseImponible),
                Valor = g.Sum(i => i.Valor)
            })
            .ToList();

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("notaCredito",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.1.0"),

                // 1. INFORMACIÓN TRIBUTARIA
                new XElement("infoTributaria",
                    new XElement("ambiente", notaCredito.Ambiente),
                    new XElement("tipoEmision", notaCredito.TipoEmision),
                    new XElement("razonSocial", notaCredito.RazonSocial),
                    new XElement("ruc", notaCredito.Ruc),
                    new XElement("claveAcceso", notaCredito.ClaveAcceso),
                    new XElement("codDoc", "04"),
                    new XElement("estab", notaCredito.Establecimiento),
                    new XElement("ptoEmi", notaCredito.PuntoEmision),
                    new XElement("secuencial", notaCredito.Secuencial),
                    new XElement("dirMatriz", notaCredito.DireccionMatriz),
                    !string.IsNullOrWhiteSpace(notaCredito.ContribuyenteRimpe)
                        ? new XElement("contribuyenteRimpe", notaCredito.ContribuyenteRimpe)
                        : null
                ),

                // 2. INFORMACIÓN DE LA NOTA DE CRÉDITO
                new XElement("infoNotaCredito",
                    new XElement("fechaEmision", notaCredito.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("tipoIdentificacionComprador", notaCredito.TipoIdentificacionComprador),
                    new XElement("razonSocialComprador", notaCredito.RazonSocialComprador),
                    new XElement("identificacionComprador", notaCredito.IdentificacionComprador),
                    new XElement("obligadoContabilidad", "NO"),
                    new XElement("codDocModificado", notaCredito.CodDocModificado),
                    new XElement("numDocModificado", notaCredito.NumDocModificado),
                    new XElement("fechaEmisionDocSustento", notaCredito.FechaEmisionDocSustento.ToString("dd/MM/yyyy")),
                    new XElement("totalSinImpuestos", notaCredito.TotalSinImpuestos.ToString("0.00", format)),
                    new XElement("valorModificacion", notaCredito.ValorModificacion.ToString("0.00", format)),
                    new XElement("moneda", "DOLAR"),

                    // Bloque totalConImpuestos
                    new XElement("totalConImpuestos",
                        impuestosAgrupados.Select(imp =>
                            new XElement("totalImpuesto",
                                new XElement("codigo", imp.Codigo),
                                new XElement("codigoPorcentaje", imp.CodigoPorcentaje),
                                new XElement("baseImponible", imp.BaseImponible.ToString("0.00", format)),
                                new XElement("valor", imp.Valor.ToString("0.00", format))
                            )
                        )
                    ),

                    new XElement("motivo", notaCredito.Motivo)
                ),

                // 3. DETALLES
                new XElement("detalles",
                    notaCredito.Detalles.Select(d =>
                        new XElement("detalle",
                            new XElement("codigoInterno", d.CodigoPrincipal),
                            new XElement("descripcion", d.Descripcion),
                            new XElement("cantidad", d.Cantidad.ToString("0.000000", format)),
                            new XElement("precioUnitario", d.PrecioUnitario.ToString("0.000000", format)),
                            new XElement("descuento", d.Descuento.ToString("0.00", format)),
                            new XElement("precioTotalSinImpuesto", d.PrecioTotalSinImpuesto.ToString("0.00", format)),
                            new XElement("impuestos",
                                d.Impuestos.Select(i =>
                                    new XElement("impuesto",
                                        new XElement("codigo", i.Codigo),
                                        new XElement("codigoPorcentaje", i.CodigoPorcentaje),
                                        new XElement("tarifa", i.Tarifa.ToString("0.00", format)),
                                        new XElement("baseImponible", i.BaseImponible.ToString("0.00", format)),
                                        new XElement("valor", i.Valor.ToString("0.00", format))
                                    )
                                )
                            )
                        )
                    )
                ),

                // 4. INFORMACIÓN ADICIONAL
                new XElement("infoAdicional",
                    !string.IsNullOrWhiteSpace(notaCredito.Cliente?.CorreoElectronico)
                        ? new XElement("campoAdicional",
                            new XAttribute("nombre", "Email"),
                            notaCredito.Cliente.CorreoElectronico)
                        : null,
                    !string.IsNullOrWhiteSpace(notaCredito.Cliente?.Direccion)
                        ? new XElement("campoAdicional",
                            new XAttribute("nombre", "Dirección"),
                            notaCredito.Cliente.Direccion)
                        : null,
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "Software"),
                        _nombreProveedor),
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "RUC Proveedor Software"),
                        _rucProveedor)
                )
            )
        );

        return xml;
    }

    public byte[] GenerarXmlBytes(NotaCredito notaCredito)
    {
        var doc = GenerarXml(notaCredito);
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, IndentedSettings))
        {
            doc.Save(writer);
        }
        return ms.ToArray();
    }

    public byte[] GenerarXmlAutorizadoBytes(NotaCredito notaCredito, string? xmlComprobanteFirmado = null)
    {
        ArgumentNullException.ThrowIfNull(notaCredito);

        string rawComprobanteXml;

        if (!string.IsNullOrWhiteSpace(xmlComprobanteFirmado))
        {
            rawComprobanteXml = xmlComprobanteFirmado;
        }
        else if (!string.IsNullOrWhiteSpace(notaCredito.XmlFirmado))
        {
            rawComprobanteXml = notaCredito.XmlFirmado;
        }
        else
        {
            var doc = GenerarXml(notaCredito);
            var sb = new StringBuilder();
            using (var stringWriter = new StringWriter(sb))
            using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Encoding = Utf8NoBom,
                Indent = false,
                OmitXmlDeclaration = false
            }))
            {
                doc.Save(xmlWriter);
            }
            rawComprobanteXml = sb.ToString();
        }

        // Si está autorizado, empaquetar en el formato estándar oficial SRI <autorizacion>
        if ((string.Equals(notaCredito.Estado, "AUTORIZADO", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(notaCredito.Estado, "AUTORIZADA", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(notaCredito.NumeroAutorizacion))
        {
            var fechaAutorizacionStr = notaCredito.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss")
                                       ?? DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy HH:mm:ss");

            var autorizacionDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("autorizacion",
                    new XElement("estado", "AUTORIZADO"),
                    new XElement("numeroAutorizacion", notaCredito.NumeroAutorizacion),
                    new XElement("fechaAutorizacion",
                        new XAttribute("class", "fechaAutorizacion"),
                        fechaAutorizacionStr),
                    new XElement("ambiente", notaCredito.Ambiente == 2 ? "PRODUCCION" : "PRUEBAS"),
                    new XElement("comprobante", new XCData(rawComprobanteXml)),
                    new XElement("mensajes")
                )
            );

            using var ms = new MemoryStream();
            using (var writer = XmlWriter.Create(ms, IndentedSettings))
            {
                autorizacionDoc.Save(writer);
            }
            return ms.ToArray();
        }

        return Utf8NoBom.GetBytes(rawComprobanteXml);
    }
}
