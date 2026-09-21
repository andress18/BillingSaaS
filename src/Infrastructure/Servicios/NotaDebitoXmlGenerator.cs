using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Infrastructure.Servicios;

public class NotaDebitoXmlGenerator : INotaDebitoXmlGenerator
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly XmlWriterSettings IndentedSettings = new()
    {
        Encoding = Utf8NoBom,
        Indent = true,
        OmitXmlDeclaration = false
    };

    private readonly string _rucProveedor;

    public NotaDebitoXmlGenerator(string rucProveedor = "1790000000001")
    {
        _rucProveedor = string.IsNullOrWhiteSpace(rucProveedor) ? "1790000000001" : rucProveedor;
    }

    public XDocument GenerarXml(NotaDebito notaDebito)
    {
        ArgumentNullException.ThrowIfNull(notaDebito);

        var format = CultureInfo.InvariantCulture;

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("notaDebito",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),

                // 1. INFORMACIÓN TRIBUTARIA
                new XElement("infoTributaria",
                    new XElement("ambiente", notaDebito.Ambiente),
                    new XElement("tipoEmision", notaDebito.TipoEmision),
                    new XElement("razonSocial", notaDebito.RazonSocial),
                    new XElement("ruc", notaDebito.Ruc),
                    new XElement("claveAcceso", notaDebito.ClaveAcceso),
                    new XElement("codDoc", "05"), // Código obligatorio para NOTA DE DÉBITO
                    new XElement("estab", notaDebito.Establecimiento),
                    new XElement("ptoEmi", notaDebito.PuntoEmision),
                    new XElement("secuencial", notaDebito.Secuencial),
                    new XElement("dirMatriz", notaDebito.DireccionMatriz),
                    !string.IsNullOrWhiteSpace(notaDebito.ContribuyenteRimpe)
                        ? new XElement("contribuyenteRimpe", notaDebito.ContribuyenteRimpe)
                        : null
                ),

                // 2. INFORMACIÓN DE LA NOTA DE DÉBITO
                new XElement("infoNotaDebito",
                    new XElement("fechaEmision", notaDebito.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("tipoIdentificacionComprador", notaDebito.TipoIdentificacionComprador),
                    new XElement("razonSocialComprador", notaDebito.RazonSocialComprador),
                    new XElement("identificacionComprador", notaDebito.IdentificacionComprador),
                    new XElement("obligadoContabilidad", "NO"),
                    new XElement("codDocModificado", notaDebito.CodDocModificado),
                    new XElement("numDocModificado", notaDebito.NumDocModificado),
                    new XElement("fechaEmisionDocSustento", notaDebito.FechaEmisionDocSustento.ToString("dd/MM/yyyy")),
                    new XElement("totalSinImpuestos", notaDebito.TotalSinImpuestos.ToString("0.00", format)),

                    // Impuestos aplicables a los motivos de la nota de débito
                    new XElement("impuestos",
                        notaDebito.Impuestos.Select(imp =>
                            new XElement("impuesto",
                                new XElement("codigo", imp.Codigo),
                                new XElement("codigoPorcentaje", imp.CodigoPorcentaje),
                                new XElement("tarifa", imp.Tarifa.ToString("0.00", format)),
                                new XElement("baseImponible", imp.BaseImponible.ToString("0.00", format)),
                                new XElement("valor", imp.Valor.ToString("0.00", format))
                            )
                        )
                    ),

                    new XElement("valorTotal", notaDebito.ValorTotal.ToString("0.00", format)),

                    // Bloque PAGOS obligatorio por el SRI
                    new XElement("pagos",
                        notaDebito.Pagos.Select(pago =>
                            new XElement("pago",
                                new XElement("formaPago", pago.FormaPago),
                                new XElement("total", pago.Total.ToString("0.00", format)),
                                pago.Plazo.HasValue ? new XElement("plazo", pago.Plazo.Value.ToString("0.00", format)) : null,
                                !string.IsNullOrWhiteSpace(pago.UnidadTiempo) ? new XElement("unidadTiempo", pago.UnidadTiempo) : null
                            )
                        )
                    )
                ),

                // 3. MOTIVOS DEL DÉBITO
                new XElement("motivos",
                    notaDebito.Motivos.Select(m =>
                        new XElement("motivo",
                            new XElement("razon", m.Razon),
                            new XElement("valor", m.Valor.ToString("0.00", format))
                        )
                    )
                ),

                // 4. INFORMACIÓN ADICIONAL
                new XElement("infoAdicional",
                    !string.IsNullOrWhiteSpace(notaDebito.Cliente?.CorreoElectronico)
                        ? new XElement("campoAdicional",
                            new XAttribute("nombre", "Email"),
                            notaDebito.Cliente.CorreoElectronico)
                        : null,
                    !string.IsNullOrWhiteSpace(notaDebito.Cliente?.Direccion)
                        ? new XElement("campoAdicional",
                            new XAttribute("nombre", "Dirección"),
                            notaDebito.Cliente.Direccion)
                        : null,
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "Software"),
                        "Factura Fácil Ecuador"),
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "RUC Proveedor Software"),
                        _rucProveedor)
                )
            )
        );

        return xml;
    }

    public byte[] GenerarXmlBytes(NotaDebito notaDebito)
    {
        var doc = GenerarXml(notaDebito);
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, IndentedSettings))
        {
            doc.Save(writer);
        }
        return ms.ToArray();
    }

    public byte[] GenerarXmlAutorizadoBytes(NotaDebito notaDebito, string? xmlComprobanteFirmado = null)
    {
        ArgumentNullException.ThrowIfNull(notaDebito);

        string rawComprobanteXml;

        if (!string.IsNullOrWhiteSpace(xmlComprobanteFirmado))
        {
            rawComprobanteXml = xmlComprobanteFirmado;
        }
        else if (!string.IsNullOrWhiteSpace(notaDebito.XmlFirmado))
        {
            rawComprobanteXml = notaDebito.XmlFirmado;
        }
        else
        {
            var doc = GenerarXml(notaDebito);
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
        if (string.Equals(notaDebito.Estado, "AUTORIZADO", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(notaDebito.NumeroAutorizacion))
        {
            var fechaAutorizacionStr = notaDebito.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss")
                                       ?? DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy HH:mm:ss");

            var autorizacionDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("autorizacion",
                    new XElement("estado", "AUTORIZADO"),
                    new XElement("numeroAutorizacion", notaDebito.NumeroAutorizacion),
                    new XElement("fechaAutorizacion",
                        new XAttribute("class", "fechaAutorizacion"),
                        fechaAutorizacionStr),
                    new XElement("ambiente", notaDebito.Ambiente == 2 ? "PRODUCCION" : "PRUEBAS"),
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

