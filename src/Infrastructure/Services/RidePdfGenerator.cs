using System;
using System.IO;
using System.Linq;
using System.Text;
using Barcoder.Code128;
using Barcoder.Renderer.Svg;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Common;
using BillingSaaS.Shared.Pdf;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BillingSaaS.Infrastructure.Services;

public class RidePdfGenerator : IRidePdfGenerator
{
    private readonly string _rucProveedor;
    private readonly string _nombreProveedor;

    static RidePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RidePdfGenerator(string rucProveedor = "0957790108001", string nombreProveedor = "Factura Fácil")
    {
        _rucProveedor = string.IsNullOrWhiteSpace(rucProveedor) ? "0957790108001" : rucProveedor;
        _nombreProveedor = string.IsNullOrWhiteSpace(nombreProveedor) ? "Factura Fácil" : nombreProveedor;
    }

    public RidePdfGenerator(IConfiguration configuration)
        : this(configuration?["ProveedorFacturacion:Ruc"] ?? "0957790108001",
               configuration?["ProveedorFacturacion:Nombre"] ?? "Factura Fácil")
    {
    }

    public byte[] GenerarFacturaRide(Factura factura, string? logoBase64 = null)
    {
        return GenerarFacturaRide(factura, emisor: null, logoBase64);
    }

    public byte[] GenerarFacturaRide(Factura factura, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(factura);
        return GenerarFacturaRide(factura.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor));
    }

    public byte[] GenerarFacturaRide(FacturaPdfRequestDto factura)
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
        var logoBase64 = factura.LogoBase64;
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
        string razonSocialEmisor = factura.Emisor?.RazonSocial ?? factura.RazonSocialEmisor ?? "EMISOR ELECTRÓNICO";
        string? nombreComercialEmisor = factura.Emisor?.NombreComercial;
        string rucEmisor = factura.Emisor?.Ruc ?? factura.RucEmisor ?? "9999999999999";
        string direccionMatriz = factura.Emisor?.DireccionMatriz ?? factura.DireccionMatrizEmisor ?? "S/N";
        string direccionEstablecimiento = factura.Emisor?.DireccionEstablecimiento ?? direccionMatriz;
        bool obligadoContabilidad = factura.Emisor?.ObligadoContabilidad == "SI";
        string? contribuyenteEspecial = factura.Emisor?.ContribuyenteEspecial;
        string? regimenRimpe = factura.Emisor?.RegimenRimpe ?? factura.RegimenRimpeEmisor;

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
                                leftCol.Item().MaxHeight(85).PaddingBottom(6).Image(logoBytes).FitArea();
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
                        string razonSocialComp = cliente?.RazonSocial ?? "CONSUMIDOR FINAL";
                        string identificacionComp = cliente?.Identificacion ?? "9999999999999";
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

                                if (!string.IsNullOrWhiteSpace(regimenRimpe))
                                {
                                    adicionalBox.Item().PaddingTop(2).Text(t =>
                                    {
                                        t.Span("Régimen Tributario: ").Bold();
                                        t.Span(regimenRimpe);
                                    });
                                }

                                if (factura.CamposAdicionales != null && factura.CamposAdicionales.Count > 0)
                                {
                                    foreach (var campo in factura.CamposAdicionales)
                                    {
                                        if (campo.Nombre.Equals("Email", StringComparison.OrdinalIgnoreCase) ||
                                            campo.Nombre.Equals("Dirección", StringComparison.OrdinalIgnoreCase) ||
                                            campo.Nombre.Equals("Direccion", StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        adicionalBox.Item().PaddingTop(2).Text(t =>
                                        {
                                            t.Span($"{campo.Nombre}: ").Bold();
                                            t.Span(campo.Valor);
                                        });
                                    }
                                }

                                // Leyenda técnica de software / Anexo 26 SRI
                                adicionalBox.Item().PaddingTop(3).Text($"Software: {_nombreProveedor} (RUC Proveedor: {_rucProveedor} - Anexo 26)").FontSize(7f).FontColor(Colors.Grey.Darken2);
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

                                string descPago = FormasPagoSRI.ObtenerDescripcion(factura.FormaPago);
                                if (factura.Plazo.HasValue && factura.Plazo.Value > 0)
                                {
                                    var unidad = string.IsNullOrWhiteSpace(factura.UnidadTiempo) ? "días" : factura.UnidadTiempo;
                                    descPago += $" (Plazo: {factura.Plazo.Value:0.##} {unidad})";
                                }

                                pagoTable.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(descPago).FontSize(7.5f);
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

    public byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, string? logoBase64 = null)
    {
        return GenerarNotaDebitoRide(notaDebito, emisor: null, logoBase64);
    }

    public byte[] GenerarNotaDebitoRide(NotaDebito notaDebito, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(notaDebito);
        return GenerarNotaDebitoRide(notaDebito.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor));
    }

    public byte[] GenerarNotaDebitoRide(NotaDebitoPdfRequestDto notaDebito)
    {
        ArgumentNullException.ThrowIfNull(notaDebito);

        var todosImpuestos = notaDebito.Impuestos.ToList();

        var subtotal15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.BaseImponible);

        var subtotal5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.BaseImponible);

        var subtotal0 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "0" || i.Tarifa == 0m))
            .Sum(i => i.BaseImponible);

        if (subtotal15 == 0 && subtotal5 == 0 && subtotal0 == 0 && notaDebito.TotalSinImpuestos > 0)
        {
            subtotal15 = notaDebito.TotalSinImpuestos;
        }

        var iva15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.Valor);

        var iva5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.Valor);

        if (iva15 == 0 && iva5 == 0 && (notaDebito.ValorTotal - notaDebito.TotalSinImpuestos) > 0)
        {
            iva15 = notaDebito.ValorTotal - notaDebito.TotalSinImpuestos;
        }

        string claveAcceso = notaDebito.ClaveAcceso ?? string.Empty;
        string? barcodeSvg = GenerarCodigoBarrasSvg(claveAcceso);

        var logoBase64 = notaDebito.LogoBase64;
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

        string razonSocialEmisor = notaDebito.Emisor?.RazonSocial ?? "EMISOR ELECTRÓNICO";
        string? nombreComercialEmisor = notaDebito.Emisor?.NombreComercial;
        string rucEmisor = notaDebito.Emisor?.Ruc ?? "9999999999999";
        string direccionMatriz = notaDebito.Emisor?.DireccionMatriz ?? "S/N";
        string direccionEstablecimiento = notaDebito.Emisor?.DireccionEstablecimiento ?? direccionMatriz;
        bool obligadoContabilidad = notaDebito.Emisor?.ObligadoContabilidad == "SI";
        string? contribuyenteEspecial = notaDebito.Emisor?.ContribuyenteEspecial;
        string? regimenRimpe = notaDebito.Emisor?.RegimenRimpe;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    // ENCABEZADO (2 COLUMNAS)
                    col.Item().Row(row =>
                    {
                        // Columna Izquierda: Logo y Datos Emisor
                        row.RelativeItem(5.2f).Column(leftCol =>
                        {
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                leftCol.Item().MaxHeight(85).PaddingBottom(6).Image(logoBytes).FitArea();
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
                            });
                        });

                        row.ConstantItem(10);

                        // Columna Derecha: RUC, Tipo Comprobante, Clave de Acceso y Código de Barras
                        row.RelativeItem(4.8f).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(rightCol =>
                        {
                            rightCol.Item().Text($"R.U.C.: {rucEmisor}").Bold().FontSize(11);
                            rightCol.Item().Text("NOTA DE DÉBITO").Bold().FontSize(12);
                            rightCol.Item().Text($"No. {notaDebito.Establecimiento}-{notaDebito.PuntoEmision}-{notaDebito.Secuencial}").Bold().FontSize(9.5f);

                            rightCol.Item().PaddingTop(3).Text(t =>
                            {
                                t.Span("NÚMERO DE AUTORIZACIÓN:").Bold().FontSize(7.5f);
                            });
                            rightCol.Item().Text(notaDebito.NumeroAutorizacion ?? (notaDebito.ClaveAcceso ?? "PENDIENTE")).FontSize(7.5f);

                            rightCol.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("FECHA Y HORA DE AUTORIZACIÓN: ").Bold().FontSize(7.5f);
                                t.Span(notaDebito.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss") ?? "PENDIENTE").FontSize(7.5f);
                            });

                            rightCol.Item().Text(t =>
                            {
                                t.Span("AMBIENTE: ").Bold().FontSize(7.5f);
                                t.Span(notaDebito.Ambiente == 2 ? "PRODUCCIÓN" : "PRUEBAS").FontSize(7.5f);
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

                    // DATOS DEL ADQUIRENTE / COMPRADOR
                    col.Item().PaddingTop(6).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(compradorCol =>
                    {
                        var cliente = notaDebito.Cliente;
                        string razonSocialComp = cliente?.RazonSocial ?? "CONSUMIDOR FINAL";
                        string identificacionComp = cliente?.Identificacion ?? "9999999999999";

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
                                t.Span(notaDebito.FechaEmision.ToString("dd/MM/yyyy"));
                            });
                        });
                    });

                    // DOCUMENTO QUE SE MODIFICA (EXCLUSIVO NOTA DE DÉBITO)
                    col.Item().PaddingTop(6).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(docSustentoCol =>
                    {
                        docSustentoCol.Item().Text("COMPROBANTE QUE SE MODIFICA").Bold().FontSize(8.5f);

                        docSustentoCol.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem(4).Text(t =>
                            {
                                t.Span("Comprobante: ").Bold();
                                t.Span(notaDebito.CodDocModificado == "01" ? "FACTURA" : $"TIPO {notaDebito.CodDocModificado}");
                            });

                            r.RelativeItem(4).Text(t =>
                            {
                                t.Span("Número: ").Bold();
                                t.Span(notaDebito.NumDocModificado);
                            });

                            r.RelativeItem(4).Text(t =>
                            {
                                t.Span("Fecha Emisión Doc. Sustento: ").Bold();
                                t.Span(notaDebito.FechaEmisionDocSustento.ToString("dd/MM/yyyy"));
                            });
                        });
                    });

                    // TABLA DE MOTIVOS DEL DÉBITO
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(7.5f);
                            columns.RelativeColumn(2.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("RAZÓN DE LA MODIFICACIÓN").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignRight().Text("VALOR DE LA MODIFICACIÓN").Bold();
                        });

                        foreach (var motivo in notaDebito.Motivos)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(motivo.Razon);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).AlignRight().Text(motivo.Valor.ToString("F2"));
                        }
                    });

                    // TOTALES Y FORMAS DE PAGO
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        // Columna Izquierda: Información Adicional y Pagos
                        row.RelativeItem(5.5f).Column(leftCol =>
                        {
                            leftCol.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(infoAdicional =>
                            {
                                infoAdicional.Item().Text("INFORMACIÓN ADICIONAL").Bold().FontSize(8.5f);

                                if (!string.IsNullOrWhiteSpace(notaDebito.Cliente?.CorreoElectronico))
                                {
                                    infoAdicional.Item().PaddingTop(2).Text(t =>
                                    {
                                        t.Span("Email: ").Bold();
                                        t.Span(notaDebito.Cliente.CorreoElectronico);
                                    });
                                }

                                if (!string.IsNullOrWhiteSpace(notaDebito.Cliente?.Direccion))
                                {
                                    infoAdicional.Item().Text(t =>
                                    {
                                        t.Span("Dirección: ").Bold();
                                        t.Span(notaDebito.Cliente.Direccion);
                                    });
                                }

                                // Leyenda técnica de software / Anexo 26 SRI
                                infoAdicional.Item().PaddingTop(3).Text($"Software: {_nombreProveedor} (RUC Proveedor: {_rucProveedor} - Anexo 26)").FontSize(7f).FontColor(Colors.Grey.Darken2);
                            });

                            if (notaDebito.Pagos.Any())
                            {
                                leftCol.Item().PaddingTop(6).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(pagosCol =>
                                {
                                    pagosCol.Item().Text("FORMAS DE PAGO").Bold().FontSize(8.5f);
                                    foreach (var pago in notaDebito.Pagos)
                                    {
                                        string descPago = pago.FormaPago == "01"
                                            ? "01 - SIN UTILIZACIÓN DEL SISTEMA FINANCIERO"
                                            : $"{pago.FormaPago} - OTROS CON UTILIZACIÓN DEL SISTEMA FINANCIERO";

                                        pagosCol.Item().PaddingTop(2).Row(r =>
                                        {
                                            r.RelativeItem(7).Text(descPago).FontSize(7.5f);
                                            r.RelativeItem(3).AlignRight().Text($"$ {pago.Total:F2}").FontSize(7.5f).Bold();
                                        });
                                    }
                                });
                            }
                        });

                        row.ConstantItem(10);

                        // Columna Derecha: Cuadro de Totales
                        row.RelativeItem(4.5f).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(4).Column(totalesCol =>
                        {
                            void AgregarFilaTotal(string label, decimal valor, bool bold = false)
                            {
                                totalesCol.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Row(r =>
                                {
                                    var text = r.RelativeItem(6).Text(label).FontSize(7.5f);
                                    if (bold) text.Bold();

                                    var valText = r.RelativeItem(4).AlignRight().Text(valor.ToString("F2")).FontSize(7.5f);
                                    if (bold) valText.Bold();
                                });
                            }

                            AgregarFilaTotal("SUBTOTAL 15%", subtotal15);
                            AgregarFilaTotal("SUBTOTAL 5%", subtotal5);
                            AgregarFilaTotal("SUBTOTAL 0%", subtotal0);
                            AgregarFilaTotal("SUBTOTAL SIN IMPUESTOS", notaDebito.TotalSinImpuestos);
                            AgregarFilaTotal("IVA 15%", iva15);
                            AgregarFilaTotal("IVA 5%", iva5);
                            AgregarFilaTotal("VALOR TOTAL", notaDebito.ValorTotal, bold: true);
                        });
                    });
                });

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

    public byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, string? logoBase64 = null)
    {
        return GenerarNotaCreditoRide(notaCredito, emisor: null, logoBase64);
    }

    public byte[] GenerarNotaCreditoRide(NotaCredito notaCredito, Emisor? emisor, string? logoBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(notaCredito);
        return GenerarNotaCreditoRide(notaCredito.ToPdfRequest(emisor, logoBase64, _rucProveedor, _nombreProveedor));
    }

    public byte[] GenerarNotaCreditoRide(NotaCreditoPdfRequestDto notaCredito)
    {
        ArgumentNullException.ThrowIfNull(notaCredito);

        // 1. Cálculos de subtotales agrupados por tarifa de IVA según especificaciones SRI
        var todosImpuestos = notaCredito.Detalles.SelectMany(d => d.Impuestos).ToList();

        var subtotal15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.BaseImponible);

        var subtotal5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.BaseImponible);

        var subtotal0 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "0" || i.Tarifa == 0m))
            .Sum(i => i.BaseImponible);

        if (subtotal15 == 0 && subtotal5 == 0 && subtotal0 == 0 && notaCredito.TotalSinImpuestos > 0)
        {
            subtotal15 = notaCredito.TotalSinImpuestos;
        }

        var iva15 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "4" || i.Tarifa == 15m))
            .Sum(i => i.Valor);

        var iva5 = todosImpuestos
            .Where(i => i.Codigo == "2" && (i.CodigoPorcentaje == "5" || i.Tarifa == 5m))
            .Sum(i => i.Valor);

        if (iva15 == 0 && iva5 == 0 && (notaCredito.ValorModificacion - notaCredito.TotalSinImpuestos) > 0)
        {
            iva15 = notaCredito.ValorModificacion - notaCredito.TotalSinImpuestos;
        }

        // 2. Generación vectorial del código de barras
        string claveAcceso = notaCredito.ClaveAcceso ?? string.Empty;
        string? barcodeSvg = GenerarCodigoBarrasSvg(claveAcceso);

        // 3. Procesamiento de logo opcional
        var logoBase64 = notaCredito.LogoBase64;
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
        string razonSocialEmisor = notaCredito.Emisor?.RazonSocial ?? "EMISOR ELECTRÓNICO";
        string? nombreComercialEmisor = notaCredito.Emisor?.NombreComercial;
        string rucEmisor = notaCredito.Emisor?.Ruc ?? "9999999999999";
        string direccionMatriz = notaCredito.Emisor?.DireccionMatriz ?? "S/N";
        string direccionEstablecimiento = notaCredito.Emisor?.DireccionEstablecimiento ?? direccionMatriz;
        bool obligadoContabilidad = notaCredito.Emisor?.ObligadoContabilidad == "SI";
        string? contribuyenteEspecial = notaCredito.Emisor?.ContribuyenteEspecial;
        string? regimenRimpe = notaCredito.Emisor?.RegimenRimpe;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    // Encabezado
                    col.Item().Row(row =>
                    {
                        // Columna Izquierda: Emisor
                        row.RelativeItem(5).Column(leftCol =>
                        {
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                leftCol.Item().MaxHeight(85).PaddingBottom(6).Image(logoBytes).FitArea();
                            }

                            leftCol.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(emisorBox =>
                            {
                                emisorBox.Item().Text(razonSocialEmisor).Bold().FontSize(10);
                                if (!string.IsNullOrWhiteSpace(nombreComercialEmisor))
                                    emisorBox.Item().Text(nombreComercialEmisor).FontSize(9).FontColor(Colors.Grey.Darken2);

                                emisorBox.Item().PaddingTop(4).Text($"Dirección Matriz: {direccionMatriz}").FontSize(8);
                                if (!string.IsNullOrWhiteSpace(direccionEstablecimiento) && direccionEstablecimiento != direccionMatriz)
                                    emisorBox.Item().Text($"Dirección Sucursal: {direccionEstablecimiento}").FontSize(8);

                                if (!string.IsNullOrWhiteSpace(contribuyenteEspecial))
                                    emisorBox.Item().Text($"Contribuyente Especial Nro: {contribuyenteEspecial}").FontSize(8);

                                emisorBox.Item().Text($"OBLIGADO A LLEVAR CONTABILIDAD: {(obligadoContabilidad ? "SI" : "NO")}").FontSize(8).Bold();

                                if (!string.IsNullOrWhiteSpace(regimenRimpe))
                                    emisorBox.Item().PaddingTop(2).Text(regimenRimpe).FontSize(7.5f).Bold();
                            });
                        });

                        row.ConstantItem(10);

                        // Columna Derecha: Datos Tributarios Nota de Crédito
                        row.RelativeItem(5).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(rightCol =>
                        {
                            rightCol.Item().Text($"R.U.C.: {rucEmisor}").Bold().FontSize(11);
                            rightCol.Item().PaddingVertical(2).Text("NOTA DE CRÉDITO").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                            rightCol.Item().Text($"No. {notaCredito.Establecimiento}-{notaCredito.PuntoEmision}-{notaCredito.Secuencial}").FontSize(9.5f).Bold();
                            rightCol.Item().PaddingTop(3).Text($"NÚMERO DE AUTORIZACIÓN:").FontSize(7.5f);
                            rightCol.Item().Text(notaCredito.NumeroAutorizacion ?? notaCredito.ClaveAcceso ?? "PENDIENTE").FontSize(7.5f).FontColor(Colors.Grey.Darken3);

                            string fechaAut = notaCredito.FechaAutorizacion?.ToString("dd/MM/yyyy HH:mm:ss")
                                              ?? DateTime.UtcNow.AddHours(-5).ToString("dd/MM/yyyy HH:mm:ss");
                            rightCol.Item().PaddingTop(2).Text($"FECHA Y HORA DE AUTORIZACIÓN: {fechaAut}").FontSize(7.5f);
                            rightCol.Item().Text($"AMBIENTE: {(notaCredito.Ambiente == 2 ? "PRODUCCIÓN" : "PRUEBAS")}").FontSize(8);
                            rightCol.Item().Text("EMISIÓN: NORMAL").FontSize(8);

                            rightCol.Item().PaddingTop(4).Text("CLAVE DE ACCESO:").Bold().FontSize(7.5f);
                            if (!string.IsNullOrWhiteSpace(barcodeSvg))
                            {
                                rightCol.Item().Height(35).Svg(barcodeSvg);
                            }
                            rightCol.Item().AlignCenter().Text(claveAcceso).FontSize(7.5f);
                        });
                    });

                    col.Item().PaddingVertical(6);

                    // Información del Comprador y Documento Modificado
                    col.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(infoBox =>
                    {
                        infoBox.Item().Row(r =>
                        {
                            r.RelativeItem(7).Text($"Razón Social / Nombres y Apellidos: {notaCredito.Cliente?.RazonSocial ?? "CONSUMIDOR FINAL"}").Bold().FontSize(8.5f);
                            r.RelativeItem(3).Text($"Identificación: {notaCredito.Cliente?.Identificacion ?? "9999999999999"}").Bold().FontSize(8.5f);
                        });

                        infoBox.Item().PaddingTop(2).Row(r =>
                        {
                            r.RelativeItem(7).Text($"Fecha Emisión: {notaCredito.FechaEmision:dd/MM/yyyy}").FontSize(8);
                            r.RelativeItem(3).Text($"Comprobante Modificado: FACTURA").Bold().FontSize(8);
                        });

                        infoBox.Item().PaddingTop(2).Row(r =>
                        {
                            r.RelativeItem(7).Text($"No. Comprobante Modificado: {notaCredito.NumDocModificado}").Bold().FontSize(8.5f);
                            r.RelativeItem(3).Text($"Fecha Emisión Sustento: {notaCredito.FechaEmisionDocSustento:dd/MM/yyyy}").FontSize(8);
                        });

                        infoBox.Item().PaddingTop(2).Text($"Razón de Modificación: {notaCredito.Motivo}").FontSize(8.5f).Bold();
                    });

                    col.Item().PaddingVertical(6);

                    // Tabla de Detalles
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);    // Cod. Principal
                            columns.RelativeColumn(1.2f); // Cantidad
                            columns.RelativeColumn(4.5f); // Descripción
                            columns.RelativeColumn(1.5f); // Precio Unitario
                            columns.RelativeColumn(1.3f); // Descuento
                            columns.RelativeColumn(1.5f); // Precio Total
                        });

                        // Encabezados
                        table.Header(header =>
                        {
                            void StyleHeader(IContainer cell, string text, bool alignRight = false)
                            {
                                var c = cell.Background(Colors.Grey.Lighten2)
                                            .Border(0.5f)
                                            .BorderColor(Colors.Grey.Medium)
                                            .Padding(3);
                                if (alignRight)
                                    c.AlignRight().Text(text).Bold().FontSize(7.5f);
                                else
                                    c.Text(text).Bold().FontSize(7.5f);
                            }

                            StyleHeader(header.Cell(), "Cod. Principal");
                            StyleHeader(header.Cell(), "Cant", true);
                            StyleHeader(header.Cell(), "Descripción");
                            StyleHeader(header.Cell(), "Precio Unit.", true);
                            StyleHeader(header.Cell(), "Descuento", true);
                            StyleHeader(header.Cell(), "Precio Total", true);
                        });

                        // Filas
                        foreach (var det in notaCredito.Detalles)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(det.CodigoPrincipal).FontSize(7.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(det.Cantidad.ToString("G29")).FontSize(7.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(det.Descripcion).FontSize(7.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(det.PrecioUnitario.ToString("F2")).FontSize(7.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(det.Descuento.ToString("F2")).FontSize(7.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(det.PrecioTotalSinImpuesto.ToString("F2")).FontSize(7.5f);
                        }
                    });

                    col.Item().PaddingVertical(6);

                    // Bloque Inferior: Información Adicional y Cuadro de Totales
                    col.Item().Row(row =>
                    {
                        // Columna Izquierda: Información Adicional
                        row.RelativeItem(5.5f).Column(infoAdicionalCol =>
                        {
                            infoAdicionalCol.Item().Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(6).Column(box =>
                            {
                                box.Item().Text("INFORMACIÓN ADICIONAL").Bold().FontSize(8.5f);
                                if (!string.IsNullOrWhiteSpace(notaCredito.Cliente?.Direccion))
                                    box.Item().PaddingTop(2).Text($"Dirección: {notaCredito.Cliente.Direccion}").FontSize(7.5f);
                                if (!string.IsNullOrWhiteSpace(notaCredito.Cliente?.CorreoElectronico))
                                    box.Item().PaddingTop(1).Text($"Email: {notaCredito.Cliente.CorreoElectronico}").FontSize(7.5f);

                                // Leyenda técnica de software / Anexo 26 SRI
                                box.Item().PaddingTop(3).Text($"Software: {_nombreProveedor} (RUC Proveedor: {_rucProveedor} - Anexo 26)").FontSize(7f).FontColor(Colors.Grey.Darken2);
                            });
                        });

                        row.ConstantItem(10);

                        // Columna Derecha: Cuadro de Totales
                        row.RelativeItem(4.5f).Border(0.8f).BorderColor(Colors.Grey.Medium).CornerRadius(4).Padding(4).Column(totalesCol =>
                        {
                            void AgregarFilaTotal(string label, decimal valor, bool bold = false)
                            {
                                totalesCol.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Row(r =>
                                {
                                    var text = r.RelativeItem(6).Text(label).FontSize(7.5f);
                                    if (bold) text.Bold();

                                    var valText = r.RelativeItem(4).AlignRight().Text(valor.ToString("F2")).FontSize(7.5f);
                                    if (bold) valText.Bold();
                                });
                            }

                            AgregarFilaTotal("SUBTOTAL 15%", subtotal15);
                            AgregarFilaTotal("SUBTOTAL 5%", subtotal5);
                            AgregarFilaTotal("SUBTOTAL 0%", subtotal0);
                            AgregarFilaTotal("SUBTOTAL SIN IMPUESTOS", notaCredito.TotalSinImpuestos);
                            AgregarFilaTotal("TOTAL DESCUENTO", notaCredito.TotalDescuento);
                            AgregarFilaTotal("IVA 15%", iva15);
                            AgregarFilaTotal("IVA 5%", iva5);
                            AgregarFilaTotal("VALOR TOTAL", notaCredito.ValorModificacion, bold: true);
                        });
                    });
                });

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
