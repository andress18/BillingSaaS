using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Infrastructure.Servicios;

public class FacturaXmlGenerator : IFacturaXmlGenerator
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly XmlWriterSettings IndentedSettings = new()
    {
        Encoding = Utf8NoBom,
        Indent = true,
        OmitXmlDeclaration = false
    };

    // Tu RUC como proveedor de software (Requisito Anexo 26)
    private readonly string _rucProveedor;

    public FacturaXmlGenerator(string rucProveedor = "1790000000001")
    {
        _rucProveedor = string.IsNullOrWhiteSpace(rucProveedor) ? "1790000000001" : rucProveedor;
    }

    public XDocument GenerarXml(Factura factura)
    {
        // Para asegurar que los decimales usen punto (ej. 12.50) y no coma, exigido por el XML
        var format = CultureInfo.InvariantCulture;

        // Agrupación de impuestos requerida para la sección <totalConImpuestos>
        var impuestosAgrupados = factura.Detalles
            .SelectMany(d => d.Impuestos)
            .GroupBy(i => new { i.Codigo, i.CodigoPorcentaje })
            .Select(g => new
            {
                Codigo = g.Key.Codigo,
                CodigoPorcentaje = g.Key.CodigoPorcentaje,
                BaseImponible = g.Sum(i => i.BaseImponible),
                Valor = g.Sum(i => i.Valor)
            }).ToList();

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("factura",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.1.0"), // Usamos la versión 1.1.0

                // 1. INFORMACIÓN TRIBUTARIA
                new XElement("infoTributaria",
                    new XElement("ambiente", factura.Ambiente),
                    new XElement("tipoEmision", factura.TipoEmision),
                    new XElement("razonSocial", factura.RazonSocial),
                    new XElement("ruc", factura.Ruc),
                    new XElement("claveAcceso", factura.ClaveAcceso),
                    new XElement("codDoc", "01"), // Código obligatorio para FACTURA[cite: 1]
                    new XElement("estab", factura.Establecimiento),
                    new XElement("ptoEmi", factura.PuntoEmision),
                    new XElement("secuencial", factura.Secuencial),
                    new XElement("dirMatriz", factura.DireccionMatriz),
                    !string.IsNullOrWhiteSpace(factura.ContribuyenteRimpe)
                        ? new XElement("contribuyenteRimpe", factura.ContribuyenteRimpe)
                        : null
                ),

                // 2. INFORMACIÓN DE LA FACTURA
                new XElement("infoFactura",
                    new XElement("fechaEmision", factura.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("tipoIdentificacionComprador", factura.Cliente.TipoIdentificacion),
                    new XElement("razonSocialComprador", factura.Cliente.RazonSocial),
                    new XElement("identificacionComprador", factura.Cliente.Identificacion),
                    new XElement("totalSinImpuestos", factura.TotalSinImpuestos.ToString("0.00", format)),
                    new XElement("totalDescuento", factura.TotalDescuento.ToString("0.00", format)),
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
                    new XElement("importeTotal", factura.ImporteTotal.ToString("0.00", format)),
                    new XElement("moneda", "DOLAR"), // Moneda obligatoria por defecto
                    // 2.1 Bloque PAGOS obligatorio por el SRI v1.1.0
                    new XElement("pagos",
                        new XElement("pago",
                            new XElement("formaPago", "01"), // 01: Sin utilización del sistema financiero
                            new XElement("total", factura.ImporteTotal.ToString("0.00", format))
                        )
                    )
                ),

                // 3. DETALLES DE PRODUCTOS
                new XElement("detalles",
                    factura.Detalles.Select(d =>
                        new XElement("detalle",
                            new XElement("codigoPrincipal", d.CodigoPrincipal),
                            new XElement("descripcion", d.Descripcion),
                            new XElement("cantidad",
                                d.Cantidad.ToString("0.000000",
                                    format)), // Hasta 6 decimales permitidos en v1.1.0[cite: 1]
                            new XElement("precioUnitario",
                                d.PrecioUnitario.ToString("0.000000",
                                    format)), // Hasta 6 decimales permitidos en v1.1.0[cite: 1]
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

                // 4. INFORMACIÓN ADICIONAL (Cumplimiento Legal Anexo 26)
                new XElement("infoAdicional",
                    // Requisito obligatorio: RUC de Proveedor de Sistemas Informáticos[cite: 1]
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "RUC Proveedor"),
                        _rucProveedor
                    ),
                    !string.IsNullOrWhiteSpace(factura.ContribuyenteRimpe)
                        ? new XElement("campoAdicional", new XAttribute("nombre", "Regimen"),
                            factura.ContribuyenteRimpe)
                        : null,
                    // Opcionales del cliente (Si tiene dirección o correo configurado)
                    !string.IsNullOrWhiteSpace(factura.Cliente.Direccion)
                        ? new XElement("campoAdicional", new XAttribute("nombre", "Direccion"),
                            factura.Cliente.Direccion)
                        : null,
                    !string.IsNullOrWhiteSpace(factura.Cliente.CorreoElectronico)
                        ? new XElement("campoAdicional", new XAttribute("nombre", "Email"),
                            factura.Cliente.CorreoElectronico)
                        : null
                )
            )
        );

        // Se elimina cualquier nodo nulo que haya quedado por atributos opcionales vacíos
        xml.Descendants().Where(e => e.IsEmpty && !e.HasAttributes).Remove();

        return xml;
    }

    public byte[] GenerarXmlBytes(Factura factura)
    {
        var doc = GenerarXml(factura);
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, IndentedSettings))
        {
            doc.Save(writer);
        }
        return ms.ToArray();
    }

    public byte[] GenerarXmlAutorizadoBytes(Factura factura, string? xmlComprobanteFirmado = null)
    {
        // 1. Obtener el XML del comprobante (prioriza el firmado digitalmente en memoria/BD)
        string rawComprobanteXml;
        if (!string.IsNullOrWhiteSpace(xmlComprobanteFirmado))
        {
            rawComprobanteXml = xmlComprobanteFirmado;
        }
        else
        {
            var facturaDoc = GenerarXml(factura);
            var sb = new StringBuilder();
            using (var stringWriter = new StringWriter(sb))
            using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Encoding = Utf8NoBom,
                Indent = false,
                OmitXmlDeclaration = false
            }))
            {
                facturaDoc.Save(xmlWriter);
            }
            rawComprobanteXml = sb.ToString();
        }

        // 2. Si está autorizado, empaquetar en el formato estándar oficial SRI <autorizacion>
        if (string.Equals(factura.Estado, "AUTORIZADO", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(factura.NumeroAutorizacion))
        {
            var fechaAutorizacionStr = factura.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss")
                                       ?? DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy HH:mm:ss");

            var autorizacionDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("autorizacion",
                    new XElement("estado", "AUTORIZADO"),
                    new XElement("numeroAutorizacion", factura.NumeroAutorizacion),
                    new XElement("fechaAutorizacion",
                        new XAttribute("class", "fechaAutorizacion"),
                        fechaAutorizacionStr),
                    new XElement("ambiente", factura.Ambiente == 2 ? "PRODUCCION" : "PRUEBAS"),
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

        // Si no está autorizado, retornar el XML de la factura directamente
        return Utf8NoBom.GetBytes(rawComprobanteXml);
    }
}
