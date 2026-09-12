using System.Globalization;
using System.Xml.Linq;
using BillingSaaS.Domain.Entities; // Ajusta al namespace de tus entidades

namespace BillingSaaS.Infrastructure.Services;

public class FacturaXmlGenerator
{
    // Tu RUC como proveedor de software (Requisito Anexo 26)
    private readonly string _rucProveedor = "1790000000001";

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
                    new XElement("razonSocial", factura.RazonSocialComprador),
                    new XElement("ruc", factura.Ruc),
                    new XElement("claveAcceso", factura.ClaveAcceso),
                    new XElement("codDoc", "01"), // Código obligatorio para FACTURA[cite: 1]
                    new XElement("estab", factura.Establecimiento),
                    new XElement("ptoEmi", factura.PuntoEmision),
                    new XElement("secuencial", factura.Secuencial),
                    new XElement("dirMatriz", factura.DireccionMatriz)
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
                    new XElement("moneda", "DOLAR") // Moneda obligatoria por defecto[cite: 1]
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
}
