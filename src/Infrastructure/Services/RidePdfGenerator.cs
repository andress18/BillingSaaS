using System;
using System.IO;
using System.Linq;
using System.Text;
using Barcoder.Code128;
using Barcoder.Renderer.Svg;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BillingSaaS.Infrastructure.Services;

public class RidePdfGenerator : IRidePdfGenerator
{
    static RidePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null)
    {
        return GenerarFacturaRide(factura, emisor: null, logoBase64);
    }

    public byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(factura);

        // 1. Cálculos de subtotales agrupados por tarifa de IVA según especificaciones SRI
        var todosImpuestos = factura.Detalles.SelectMany(d => d.Impuestos).ToList();

        var subtotal15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.BaseImponible);

        var subtotal5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.BaseImponible);

        var subtotal0 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "0" || i.Tarifa == 0m))
            .Sum(i => i.BaseImponible);

        var subtotalNoObjeto = todosImpuestos
            .Where(i => i.Codigo == "2" && i.CodigoPorcentaje == "6")
            .Sum(i => i.BaseImponible);

        var subtotalExento = todosImpuestos
            .Where(i => i.Codigo == "2" && i.CodigoPorcentaje == "7")
            .Sum(i => i.BaseImponible);

        // Si no se desglosaron impuestos explícitos, garantizar coherencia con TotalSinImpuestos
        if (subtotal15 == 0 && subtotal5 == 0 && subtotal0 == 0 && subtotalNoObjeto == 0 && subtotalExento == 0 && factura.TotalSinImpuestos > 0)
        {
            subtotal15 = factura.TotalSinImpuestos;
        }

        var iva15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.Valor);

        var iva5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.Valor);

        if (iva15 == 0 && iva5 == 0 && (factura.ImporteTotal - factura.TotalSinImpuestos) > 0)
        {
            iva15 = factura.ImporteTotal - factura.TotalSinImpuestos;
        }

        // 2. Generación vectorial del código de barras Code128 (Clave de acceso de 49 dígitos)
        string claveAcceso = factura.ClaveAcceso ?? string.Empty;
        string? barcodeSvg = GenerarCodigoBarrasSvg(claveAcceso);

        // 3. Procesamiento de logo opcional
        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(logoBase64))
        {
            try
            {
                var cleanBase64 = logoBase64.Contains(',') ? logoBase64.Split(',')[1] : logoBase64;
                logoBytes = Convert.FromBase64String(cleanBase64);
            }
            catch
            {
                logoBytes = null;
            }
        }

        // 4. Datos del emisor
        string razonSocialEmisor = emisor?.RazonSocial ?? factura.RazonSocial ?? "EMISOR ELECTRÓNICO";
        string? nombreComercialEmisor = emisor?.NombreComercial;
        string rucEmisor = emisor?.Ruc ?? factura.Ruc ?? "9999999999999";
        string direccionMatriz = emisor?.DireccionMatriz ?? factura.DireccionMatriz ?? "S/N";
        string direccionEstablecimiento = emisor?.DireccionEstablecimiento ?? direccionMatriz;
        bool obligadoContabilidad = emisor?.ObligadoContabilidad ?? false;
        string? contribuyenteEspecial = emisor?.ContribuyenteEspecial;
        string? regimenRimpe = emisor?.RegimenRimpe;

        // 5. Creación del Documento QuestPDF
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    // ==========================================
                    // ENCABEZADO (2 COLUMNAS)
                    // ==========================================
                    col.Item().Row(row =>
                    {
                        // Columna Izquierda: Logo y Datos Emisor
                        row.RelativeItem(5.2f).Column(leftCol =>
                        {
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                leftCol.Item().MaxHeight(50).PaddingBottom(5).Image(logoBytes).FitArea();
                            }

                            leftCol.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(emisorBox =>
                            {
                                emisorBox.Item().Text(razonSocialEmisor).Bold().FontSize(9.5f);

                                if (!string.IsNullOrWhiteSpace(nombreComercialEmisor))
                                {
                                    emisorBox.Item().Text(nombreComercialEmisor).Italic().FontSize(8.5f);
                                }

                                emisorBox.Item().PaddingTop(3).Text(t =>
                                {
                                    t.Span("Dir. Matriz: ").Bold();
                                    t.Span(direccionMatriz);
                                });

                                emisorBox.Item().Text(t =>
                                {
                                    t.Span("Dir. Sucursal: ").Bold();
                                    t.Span(direccionEstablecimiento);
                                });

                                if (!string.IsNullOrWhiteSpace(contribuyenteEspecial))
                                {
                                    emisorBox.Item().Text(t =>
                                    {
                                        t.Span("Contribuyente Especial Nro: ").Bold();
                                        t.Span(contribuyenteEspecial);
                                    });
                                }

                                emisorBox.Item().Text(t =>
                                {
                                    t.Span("OBLIGADO A LLEVAR CONTABILIDAD: ").Bold();
                                    t.Span(obligadoContabilidad ? "SI" : "NO");
                                });

                                if (!string.IsNullOrWhiteSpace(regimenRimpe))
                                {
                                    emisorBox.Item().PaddingTop(2).Text(regimenRimpe.ToUpperInvariant()).Bold().FontSize(7.5f);
                                }

                                emisorBox.Item().PaddingTop(2).Text("Agente de Retención Resolución No. 1").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                            });
                        });

                        row.ConstantItem(10); // Espacio entre columnas

                        // Columna Derecha: RUC, Tipo Comprobante, Clave de Acceso y Código de Barras
                        row.RelativeItem(4.8f).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(rightCol =>
                        {
                            rightCol.Item().Text($"R.U.C.: {rucEmisor}").Bold().FontSize(11);
                            rightCol.Item().Text("F A C T U R A").Bold().FontSize(12);
                            rightCol.Item().Text($"No. {factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial}").Bold().FontSize(9.5f);

                            rightCol.Item().PaddingTop(3).Text(t =>
                            {
                                t.Span("NÚMERO DE AUTORIZACIÓN:").Bold().FontSize(7.5f);
                            });
                            rightCol.Item().Text(factura.NumeroAutorizacion ?? (factura.ClaveAcceso ?? "PENDIENTE")).FontSize(7.5f);

                            rightCol.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("FECHA Y HORA DE AUTORIZACIÓN: ").Bold().FontSize(7.5f);
                                t.Span(factura.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss") ?? "PENDIENTE").FontSize(7.5f);
                            });

                            rightCol.Item().Text(t =>
                            {
                                t.Span("AMBIENTE: ").Bold().FontSize(7.5f);
                                t.Span(factura.Ambiente == 2 ? "PRODUCCIÓN" : "PRUEBAS").FontSize(7.5f);
                            });

                            rightCol.Item().Text(t =>
                            {
                                t.Span("EMISIÓN: ").Bold().FontSize(7.5f);
                                t.Span("NORMAL").FontSize(7.5f);
                            });

                            rightCol.Item().PaddingTop(3).Text("CLAVE DE ACCESO:").Bold().FontSize(7.5f);

                            if (!string.IsNullOrWhiteSpace(barcodeSvg))
                            {
                                rightCol.Item().PaddingTop(2).Svg(barcodeSvg);
                            }

                            rightCol.Item().PaddingTop(2).AlignCenter().Text(claveAcceso).FontSize(7f);
                        });
                    });

                    // ==========================================
                    // DATOS DEL ADQUIRENTE / COMPRADOR
                    // ==========================================
                    col.Item().PaddingTop(6).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(compradorCol =>
                    {
                        var cliente = factura.Cliente;
                        string razonSocialComp = cliente?.RazonSocial ?? factura.RazonSocialComprador ?? "CONSUMIDOR FINAL";
                        string identificacionComp = cliente?.Identificacion ?? factura.IdentificacionComprador ?? "9999999999999";
                        string direccionComp = cliente?.Direccion ?? "S/N";

                        compradorCol.Item().Row(r =>
                        {
                            r.RelativeItem(7).Text(t =>
                            {
                                t.Span("Razón Social / Nombres y Apellidos: ").Bold();
                                t.Span(razonSocialComp);
                            });

                            r.RelativeItem(3).Text(t =>
                            {
                                t.Span("Identificación: ").Bold();
                                t.Span(identificacionComp);
                            });
                        });

                        compradorCol.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem(7).Text(t =>
                            {
                                t.Span("Fecha Emisión: ").Bold();
                                t.Span(factura.FechaEmision.ToString("dd/MM/yyyy"));
                            });

                            r.RelativeItem(3).Text(t =>
                            {
                                t.Span("Guía de Remisión: ").Bold();
                                t.Span("-");
                            });
                        });

                        compradorCol.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Dirección: ").Bold();
                                t.Span(direccionComp);
                            });
                        });
                    });

                    // ==========================================
                    // TABLA DE DETALLES (PRODUCTOS / SERVICIOS)
                    // ==========================================
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f); // Cod. Principal
                            columns.RelativeColumn(1.0f); // Cantidad
                            columns.RelativeColumn(4.4f); // Descripción
                            columns.RelativeColumn(1.4f); // Precio Unitario
                            columns.RelativeColumn(1.2f); // Descuento
                            columns.RelativeColumn(1.4f); // Precio Total
                        });

                        // Cabecera de la tabla
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).Text("Cod. Principal").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).AlignRight().Text("Cant.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).Text("Descripción").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).AlignRight().Text("Precio Unit.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).AlignRight().Text("Descuento").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3).AlignRight().Text("Precio Total").Bold();
                        });

                        // Filas de productos
                        foreach (var detalle in factura.Detalles)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(detalle.CodigoPrincipal);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(detalle.Cantidad.ToString("N2"));
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(detalle.Descripcion);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(detalle.PrecioUnitario.ToString("N2"));
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(detalle.Descuento.ToString("N2"));
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(detalle.PrecioTotalSinImpuesto.ToString("N2"));
                        }
                    });

                    // ==========================================
                    // SECCIÓN INFERIOR: INFO ADICIONAL + TOTALES
                    // ==========================================
                    col.Item().PaddingTop(8).Row(bottomRow =>
                    {
                        // Columna Izquierda: Información Adicional y Formas de Pago
                        bottomRow.RelativeItem(5.4f).Column(infoCol =>
                        {
                            infoCol.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(5).Column(adicionalBox =>
                            {
                                adicionalBox.Item().Text("INFORMACIÓN ADICIONAL").Bold().FontSize(8f);

                                string correo = factura.Cliente?.CorreoElectronico ?? "N/A";
                                adicionalBox.Item().PaddingTop(3).Text(t =>
                                {
                                    t.Span("Email: ").Bold();
                                    t.Span(correo);
                                });

                                if (!string.IsNullOrWhiteSpace(factura.Cliente?.Direccion))
                                {
                                    adicionalBox.Item().Text(t =>
                                    {
                                        t.Span("Dirección: ").Bold();
                                        t.Span(factura.Cliente.Direccion);
                                    });
                                }

                                // Leyenda técnica de software / Anexo 26 SRI
                                adicionalBox.Item().PaddingTop(3).Text("Software: BillingSaaS (RUC Proveedor: 0957790108001 - Anexo 26)").FontSize(7f).FontColor(Colors.Grey.Darken2);
                            });

                            // Cuadro de Formas de Pago
                            infoCol.Item().PaddingTop(6).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Table(pagoTable =>
                            {
                                pagoTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(7);
                                    cols.RelativeColumn(3);
                                });

                                pagoTable.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(2).Text("Forma de Pago").Bold().FontSize(7.5f);
                                    h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(2).AlignRight().Text("Total").Bold().FontSize(7.5f);
                                });

                                pagoTable.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("OTROS CON UTILIZACION DEL SISTEMA FINANCIERO").FontSize(7.5f);
                                pagoTable.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"${factura.ImporteTotal:N2}").FontSize(7.5f);
                            });
                        });

                        bottomRow.ConstantItem(10);

                        // Columna Derecha: Cuadro de Impuestos y Totales
                        bottomRow.RelativeItem(4.6f).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Table(totalesTable =>
                        {
                            totalesTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(6.5f);
                                cols.RelativeColumn(3.5f);
                            });

                            void AgregarFilaTotal(string etiqueta, decimal valor, bool bold = false)
                            {
                                var cellEtiqueta = totalesTable.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(etiqueta).FontSize(7.5f);
                                if (bold) cellEtiqueta.Bold();

                                var cellValor = totalesTable.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"${valor:N2}").FontSize(7.5f);
                                if (bold) cellValor.Bold();
                            }

                            AgregarFilaTotal("SUBTOTAL 15%", subtotal15);
                            AgregarFilaTotal("SUBTOTAL 5%", subtotal5);
                            AgregarFilaTotal("SUBTOTAL 0%", subtotal0);
                            AgregarFilaTotal("SUBTOTAL NO OBJETO DE IVA", subtotalNoObjeto);
                            AgregarFilaTotal("SUBTOTAL EXENTO DE IVA", subtotalExento);
                            AgregarFilaTotal("SUBTOTAL SIN IMPUESTOS", factura.TotalSinImpuestos);
                            AgregarFilaTotal("TOTAL DESCUENTO", factura.TotalDescuento);
                            AgregarFilaTotal("IVA 15%", iva15);
                            AgregarFilaTotal("IVA 5%", iva5);
                            AgregarFilaTotal("PROPINA", 0.00m);
                            AgregarFilaTotal("IMPORTE TOTAL", factura.ImporteTotal, bold: true);
                        });
                    });
                });

                // Pie de Página
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string GenerarCodigoBarrasSvg(string claveAcceso)
    {
        if (string.IsNullOrWhiteSpace(claveAcceso))
            return string.Empty;

        try
        {
            var barcode = Code128Encoder.Encode(claveAcceso);
            var renderer = new SvgRenderer();
            using var stream = new MemoryStream();
            renderer.Render(barcode, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }
}
