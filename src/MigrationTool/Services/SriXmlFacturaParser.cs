using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BillingSaaS.MigrationTool.Models;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.MigrationTool.Services;

public class SriXmlFacturaParser : IXmlFacturaParser
{
    private readonly ILogger<SriXmlFacturaParser> _logger;

    public SriXmlFacturaParser(ILogger<SriXmlFacturaParser> logger)
    {
        _logger = logger;
    }

    public FacturaMigracionDto ParsearFacturaXml(string xmlContent, string? estadoPorDefecto = null)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("El contenido XML no puede estar vacío.", nameof(xmlContent));

        var dto = new FacturaMigracionDto
        {
            XmlOriginal = xmlContent,
            Estado = estadoPorDefecto ?? "AUTORIZADO"
        };

        try
        {
            var doc = XDocument.Parse(xmlContent);

            // 1. Verificar si viene dentro de un sobre de autorización del SRI (<autorizacion>)
            var nodoAutorizacion = FindElement(doc.Root, "autorizacion");
            if (nodoAutorizacion != null)
            {
                var estadoNodo = FindElement(nodoAutorizacion, "estado");
                if (estadoNodo != null && !string.IsNullOrWhiteSpace(estadoNodo.Value))
                {
                    dto.Estado = estadoNodo.Value.Trim().ToUpperInvariant();
                }

                var numAutNodo = FindElement(nodoAutorizacion, "numeroAutorizacion");
                if (numAutNodo != null)
                {
                    dto.NumeroAutorizacion = numAutNodo.Value.Trim();
                }

                var fechaAutNodo = FindElement(nodoAutorizacion, "fechaAutorizacion");
                if (fechaAutNodo != null && ParseDateTime(fechaAutNodo.Value, out var fechaAut))
                {
                    dto.FechaAutorizacion = fechaAut;
                }

                var comprobanteNodo = FindElement(nodoAutorizacion, "comprobante");
                if (comprobanteNodo != null && !string.IsNullOrWhiteSpace(comprobanteNodo.Value))
                {
                    dto.XmlFirmado = comprobanteNodo.Value;
                    // El comprobante interno contiene el XML de la factura
                    try
                    {
                        var innerDoc = XDocument.Parse(comprobanteNodo.Value);
                        ParsearFacturaInterna(innerDoc.Root, dto);
                        return dto;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo parsear el XML interno en <comprobante>, intentando parsear estructura directa.");
                    }
                }
            }

            // 2. Si no es sobre o el comprobante vino directo en la raíz
            var nodoFactura = FindElement(doc.Root, "factura") ?? (doc.Root?.Name.LocalName.Equals("factura", StringComparison.OrdinalIgnoreCase) == true ? doc.Root : null);
            if (nodoFactura != null)
            {
                if (doc.Descendants().Any(d => d.Name.LocalName.Equals("Signature", StringComparison.OrdinalIgnoreCase)))
                {
                    dto.XmlFirmado = xmlContent;
                }

                ParsearFacturaInterna(nodoFactura, dto);
            }
            else
            {
                _logger.LogWarning("No se encontró el nodo <factura> en el XML provisto.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al parsear factura XML.");
            throw;
        }

        return dto;
    }

    private void ParsearFacturaInterna(XElement? facturaElem, FacturaMigracionDto dto)
    {
        if (facturaElem == null) return;

        // 1. infoTributaria
        var infoTrib = FindElement(facturaElem, "infoTributaria");
        if (infoTrib != null)
        {
            dto.ClaveAcceso = GetString(infoTrib, "claveAcceso") ?? dto.ClaveAcceso;
            dto.Establecimiento = GetString(infoTrib, "estab") ?? dto.Establecimiento;
            dto.PuntoEmision = GetString(infoTrib, "ptoEmi") ?? dto.PuntoEmision;
            dto.Secuencial = GetString(infoTrib, "secuencial") ?? dto.Secuencial;

            if (string.IsNullOrWhiteSpace(dto.NumeroAutorizacion) && !string.IsNullOrWhiteSpace(dto.ClaveAcceso))
            {
                // En el SRI offline, el número de autorización coincide frecuentemente con la clave de acceso
                dto.NumeroAutorizacion = dto.ClaveAcceso;
            }
        }

        // 2. infoFactura
        var infoFact = FindElement(facturaElem, "infoFactura");
        if (infoFact != null)
        {
            var fechaStr = GetString(infoFact, "fechaEmision");
            if (!string.IsNullOrWhiteSpace(fechaStr) && ParseDateTime(fechaStr, out var fechaEmision))
            {
                dto.FechaEmision = fechaEmision;
                if (dto.FechaAutorizacion == null)
                {
                    dto.FechaAutorizacion = fechaEmision;
                }
            }

            dto.TotalSinImpuestos = GetDecimal(infoFact, "totalSinImpuestos");
            dto.TotalDescuento = GetDecimal(infoFact, "totalDescuento");
            dto.ImporteTotal = GetDecimal(infoFact, "importeTotal");

            // Forma de Pago
            var pagos = FindElement(infoFact, "pagos");
            var pago = pagos != null ? FindElement(pagos, "pago") : FindElement(infoFact, "pago");
            if (pago != null)
            {
                var fp = GetString(pago, "formaPago");
                if (!string.IsNullOrWhiteSpace(fp))
                {
                    dto.FormaPago = fp.Trim();
                }
            }

            // Comprador
            dto.Comprador = new CompradorMigracionDto
            {
                TipoIdentificacion = GetString(infoFact, "tipoIdentificacionComprador") ?? "07",
                Identificacion = GetString(infoFact, "identificacionComprador") ?? "9999999999999",
                RazonSocial = GetString(infoFact, "razonSocialComprador") ?? "CONSUMIDOR FINAL",
                Direccion = GetString(infoFact, "direccionComprador")
            };
        }

        // 3. infoAdicional (para capturar dirección o email del comprador si no vinieron en infoFactura)
        var infoAdic = FindElement(facturaElem, "infoAdicional");
        if (infoAdic != null)
        {
            foreach (var campo in infoAdic.Elements().Where(e => e.Name.LocalName.Equals("campoAdicional", StringComparison.OrdinalIgnoreCase)))
            {
                var nombreAttr = campo.Attribute("nombre")?.Value ?? string.Empty;
                var valor = campo.Value.Trim();

                if (string.IsNullOrWhiteSpace(dto.Comprador.CorreoElectronico) &&
                    (nombreAttr.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                     nombreAttr.Contains("correo", StringComparison.OrdinalIgnoreCase)))
                {
                    dto.Comprador.CorreoElectronico = valor;
                }
                else if (string.IsNullOrWhiteSpace(dto.Comprador.Direccion) &&
                         nombreAttr.Contains("direccion", StringComparison.OrdinalIgnoreCase))
                {
                    dto.Comprador.Direccion = valor;
                }
            }
        }

        // 4. detalles
        var detallesNodo = FindElement(facturaElem, "detalles");
        if (detallesNodo != null)
        {
            dto.Detalles.Clear();
            foreach (var detElem in detallesNodo.Elements().Where(e => e.Name.LocalName.Equals("detalle", StringComparison.OrdinalIgnoreCase)))
            {
                var detalle = new DetalleFacturaMigracionDto
                {
                    CodigoPrincipal = GetString(detElem, "codigoPrincipal") ?? "ITEM",
                    Descripcion = GetString(detElem, "descripcion") ?? "Servicio o Producto",
                    Cantidad = GetDecimal(detElem, "cantidad"),
                    PrecioUnitario = GetDecimal(detElem, "precioUnitario"),
                    Descuento = GetDecimal(detElem, "descuento")
                };

                // Impuestos del detalle
                var impuestosNodo = FindElement(detElem, "impuestos");
                if (impuestosNodo != null)
                {
                    foreach (var impElem in impuestosNodo.Elements().Where(e => e.Name.LocalName.Equals("impuesto", StringComparison.OrdinalIgnoreCase)))
                    {
                        var impuesto = new ImpuestoMigracionDto
                        {
                            Codigo = GetString(impElem, "codigo") ?? "2",
                            CodigoPorcentaje = GetString(impElem, "codigoPorcentaje") ?? "2",
                            Tarifa = GetDecimal(impElem, "tarifa"),
                            BaseImponible = GetDecimal(impElem, "baseImponible"),
                            Valor = GetDecimal(impElem, "valor")
                        };
                        detalle.Impuestos.Add(impuesto);
                    }
                }

                // Asegurar al menos un impuesto si vino vacío
                if (detalle.Impuestos.Count == 0)
                {
                    var baseImp = detalle.Cantidad * detalle.PrecioUnitario - detalle.Descuento;
                    detalle.Impuestos.Add(new ImpuestoMigracionDto
                    {
                        Codigo = "2",
                        CodigoPorcentaje = "2",
                        Tarifa = 15.00m,
                        BaseImponible = Math.Max(0m, baseImp),
                        Valor = Math.Round(Math.Max(0m, baseImp) * 0.15m, 2)
                    });
                }

                dto.Detalles.Add(detalle);
            }
        }

        // Si los detalles vinieron vacíos en el XML, generar una línea resumen para consistencia de entidad
        if (dto.Detalles.Count == 0 && dto.ImporteTotal > 0)
        {
            var subtotal = dto.TotalSinImpuestos > 0 ? dto.TotalSinImpuestos : dto.ImporteTotal;
            var iva = dto.ImporteTotal - subtotal;
            dto.Detalles.Add(new DetalleFacturaMigracionDto
            {
                CodigoPrincipal = "SERV-MIG",
                Descripcion = "Servicio facturado (Migración histórica)",
                Cantidad = 1,
                PrecioUnitario = subtotal,
                Descuento = dto.TotalDescuento,
                Impuestos =
                [
                    new ImpuestoMigracionDto
                    {
                        Codigo = "2",
                        CodigoPorcentaje = iva > 0 ? "2" : "0",
                        Tarifa = iva > 0 ? 15.00m : 0.00m,
                        BaseImponible = subtotal,
                        Valor = Math.Max(0m, iva)
                    }
                ]
            });
        }
    }

    public void EnriquecerEmisorDesdeFacturas(ClienteMigracionDto cliente)
    {
        if (cliente == null) return;

        int maxSecuencial = cliente.UltimoSecuencialFactura;

        // Iterar sobre las facturas para extraer el max secuencial y datos del emisor
        foreach (var fact in cliente.Facturas)
        {
            if (int.TryParse(fact.Secuencial, out var sec) && sec > maxSecuencial)
            {
                maxSecuencial = sec;
            }

            var xmlAExaminar = !string.IsNullOrWhiteSpace(fact.XmlFirmado) ? fact.XmlFirmado : fact.XmlOriginal;
            if (string.IsNullOrWhiteSpace(xmlAExaminar)) continue;

            try
            {
                var doc = XDocument.Parse(xmlAExaminar);
                var nodoFactura = FindElement(doc.Root, "factura") ??
                    (doc.Root?.Name.LocalName.Equals("factura", StringComparison.OrdinalIgnoreCase) == true ? doc.Root : null);

                if (nodoFactura == null)
                {
                    var comp = FindElement(doc.Root, "comprobante");
                    if (comp != null && !string.IsNullOrWhiteSpace(comp.Value))
                    {
                        var inner = XDocument.Parse(comp.Value);
                        nodoFactura = FindElement(inner.Root, "factura") ?? inner.Root;
                    }
                }

                if (nodoFactura == null) continue;

                var infoTrib = FindElement(nodoFactura, "infoTributaria");
                if (infoTrib != null)
                {
                    if (string.IsNullOrWhiteSpace(cliente.Ruc))
                        cliente.Ruc = GetString(infoTrib, "ruc") ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(cliente.RazonSocial))
                        cliente.RazonSocial = GetString(infoTrib, "razonSocial") ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(cliente.NombreComercial))
                        cliente.NombreComercial = GetString(infoTrib, "nombreComercial");

                    if (string.IsNullOrWhiteSpace(cliente.DireccionMatriz))
                        cliente.DireccionMatriz = GetString(infoTrib, "dirMatriz") ?? "Ecuador";

                    if (string.IsNullOrWhiteSpace(cliente.CodigoEstablecimiento) || cliente.CodigoEstablecimiento == "001")
                    {
                        var estab = GetString(infoTrib, "estab");
                        if (!string.IsNullOrWhiteSpace(estab)) cliente.CodigoEstablecimiento = estab;
                    }

                    if (string.IsNullOrWhiteSpace(cliente.PuntoEmision) || cliente.PuntoEmision == "001")
                    {
                        var pto = GetString(infoTrib, "ptoEmi");
                        if (!string.IsNullOrWhiteSpace(pto)) cliente.PuntoEmision = pto;
                    }

                    var ambStr = GetString(infoTrib, "ambiente");
                    if (int.TryParse(ambStr, out var amb) && (amb == 1 || amb == 2))
                    {
                        cliente.Ambiente = amb;
                    }

                    if (string.IsNullOrWhiteSpace(cliente.RegimenRimpe))
                        cliente.RegimenRimpe = GetString(infoTrib, "contribuyenteRimpe");

                    if (string.IsNullOrWhiteSpace(cliente.ContribuyenteEspecial))
                        cliente.ContribuyenteEspecial = GetString(infoTrib, "contribuyenteEspecial");
                }

                var infoFact = FindElement(nodoFactura, "infoFactura");
                if (infoFact != null)
                {
                    if (string.IsNullOrWhiteSpace(cliente.DireccionEstablecimiento))
                        cliente.DireccionEstablecimiento = GetString(infoFact, "dirEstablecimiento");

                    var oblig = GetString(infoFact, "obligadoContabilidad");
                    if (!string.IsNullOrWhiteSpace(oblig))
                    {
                        cliente.ObligadoContabilidad = oblig.Equals("SI", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "XML de factura omitido en extracción preliminar de emisor.");
            }
        }

        cliente.UltimoSecuencialFactura = maxSecuencial;

        // Fallbacks por defecto si no se encontraron en XML
        if (string.IsNullOrWhiteSpace(cliente.RazonSocial))
            cliente.RazonSocial = !string.IsNullOrWhiteSpace(cliente.NombreOrganizacion) ? cliente.NombreOrganizacion : "Razón Social No Especificada";

        if (string.IsNullOrWhiteSpace(cliente.DireccionMatriz))
            cliente.DireccionMatriz = "Quito - Ecuador";
    }

    private static XElement? FindElement(XElement? parent, string localName)
    {
        if (parent == null) return null;
        if (parent.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            return parent;

        return parent.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(XElement parent, string localName)
    {
        var elem = FindElement(parent, localName);
        return elem?.Value?.Trim();
    }

    private static decimal GetDecimal(XElement parent, string localName)
    {
        var str = GetString(parent, localName);
        if (string.IsNullOrWhiteSpace(str)) return 0m;

        str = str.Replace(',', '.');
        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }

    private static bool ParseDateTime(string rawDate, out DateTime result)
    {
        rawDate = rawDate.Trim();

        string[] formats =
        [
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        ];

        if (DateTime.TryParseExact(rawDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            return true;
        }

        result = DateTime.UtcNow;
        return false;
    }
}
