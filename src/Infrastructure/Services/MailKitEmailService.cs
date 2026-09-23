using System.Net.Mime;
using System.Text;
using BillingSaaS.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BillingSaaS.Infrastructure.Services;

public class MailKitEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(IConfiguration configuration, ILogger<MailKitEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendFacturaEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroFactura,
        string razonSocialEmisor,
        string claveAcceso,
        decimal importeTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Envío de correo omitido para Factura {Numero}: No se proporcionó correo del comprador.", numeroFactura);
            return;
        }

        var host = _configuration["Smtp:Host"] ?? "smtp-relay.brevo.com";
        var portStr = _configuration["Smtp:Port"] ?? "587";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 587;

        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var senderEmail = _configuration["Smtp:SenderEmail"] ?? "facturacion@facturafacil.ec";
        var senderName = _configuration["Smtp:SenderName"] ?? (!string.IsNullOrWhiteSpace(razonSocialEmisor) ? razonSocialEmisor : "Facturación Electrónica");

        var enabledStr = _configuration["Smtp:Enabled"];
        bool isExplicitlyDisabled = string.Equals(enabledStr, "false", StringComparison.OrdinalIgnoreCase);

        // Si no hay credenciales o está explícitamente deshabilitado, omitimos el envío
        if (isExplicitlyDisabled || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Envío de correo omitido para {Email} (Factura {Numero}): Credenciales SMTP no configuradas o servicio deshabilitado.", toEmail, numeroFactura);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(razonSocialComprador, toEmail.Trim()));
            message.Subject = $"Comprobante Electrónico Factura No. {numeroFactura} - {razonSocialEmisor}";

            var builder = new BodyBuilder
            {
                HtmlBody = GenerarHtmlPlantillaFactura(
                    razonSocialComprador,
                    numeroFactura,
                    razonSocialEmisor,
                    claveAcceso,
                    importeTotal)
            };

            // 1. Adjuntar RIDE PDF
            if (pdfRide != null && pdfRide.Length > 0)
            {
                builder.Attachments.Add(
                    $"Factura_{numeroFactura.Replace("-", "")}.pdf",
                    pdfRide,
                    new MimeKit.ContentType("application", "pdf"));
            }

            // 2. Adjuntar XML firmado
            if (xmlFirmado != null && xmlFirmado.Length > 0)
            {
                builder.Attachments.Add(
                    $"Factura_{numeroFactura.Replace("-", "")}.xml",
                    xmlFirmado,
                    new MimeKit.ContentType("application", "xml"));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000; // 15 segundos timeout

            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await client.AuthenticateAsync(userName, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Correo de factura {Numero} enviado exitosamente a {Email}.", numeroFactura, toEmail);
        }
        catch (Exception ex)
        {
            // El fallo de correo se captura para no revertir la factura ya autorizada en el SRI
            _logger.LogError(ex, "No se pudo enviar el correo de la factura {Numero} a {Email}.", numeroFactura, toEmail);
        }
    }

    private static string GenerarHtmlPlantillaFactura(
        string comprador,
        string numeroFactura,
        string emisor,
        string claveAcceso,
        decimal total)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f6f9;
            margin: 0;
            padding: 20px;
            color: #333333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 12px rgba(0,0,0,0.08);
            border: 1px solid #e1e8ed;
        }}
        .header {{
            background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%);
            color: #ffffff;
            padding: 24px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0 0 6px 0;
            font-size: 22px;
            letter-spacing: 0.5px;
        }}
        .header p {{
            margin: 0;
            font-size: 14px;
            opacity: 0.9;
        }}
        .content {{
            padding: 28px;
        }}
        .greeting {{
            font-size: 16px;
            font-weight: 600;
            margin-bottom: 16px;
            color: #1e293b;
        }}
        .invoice-card {{
            background-color: #f8fafc;
            border-left: 4px solid #3b82f6;
            border-radius: 4px;
            padding: 16px 20px;
            margin: 20px 0;
        }}
        .invoice-field {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 8px;
            font-size: 14px;
        }}
        .invoice-field.total {{
            border-top: 1px dashed #cbd5e1;
            padding-top: 10px;
            margin-top: 10px;
            font-size: 17px;
            font-weight: bold;
            color: #0f172a;
        }}
        .clave-acceso {{
            font-family: Consolas, Monaco, monospace;
            background-color: #f1f5f9;
            padding: 10px;
            border-radius: 4px;
            font-size: 12px;
            word-break: break-all;
            color: #475569;
            margin-top: 6px;
        }}
        .attachments-notice {{
            background-color: #eff6ff;
            border: 1px solid #bfdbfe;
            border-radius: 6px;
            padding: 14px 18px;
            margin: 20px 0;
            font-size: 13px;
            color: #1e40af;
            display: flex;
            align-items: center;
        }}
        .footer {{
            background-color: #f8fafc;
            padding: 16px 24px;
            text-align: center;
            font-size: 12px;
            color: #64748b;
            border-top: 1px solid #e2e8f0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>COMPROBANTE ELECTRÓNICO</h1>
            <p>{emisor}</p>
        </div>
        <div class=""content"">
            <div class=""greeting"">Estimado(a) {comprador},</div>
            <p style=""font-size: 14px; line-height: 1.6; margin: 0 0 16px 0;"">
                Le informamos que se ha emitido y autorizado con éxito un comprobante electrónico a su nombre ante el Servicio de Rentas Internas (SRI).
            </p>
            <div class=""invoice-card"">
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Tipo de Comprobante:</span>
                    <strong>FACTURA</strong>
                </div>
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Número:</span>
                    <strong>{numeroFactura}</strong>
                </div>
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Emisor:</span>
                    <span>{emisor}</span>
                </div>
                <div class=""invoice-field total"">
                    <span>Importe Total:</span>
                    <span style=""color: #16a34a;"">${total:F2} USD</span>
                </div>
            </div>
            <div style=""margin: 20px 0 10px 0;"">
                <span style=""font-size: 12px; font-weight: 600; color: #475569; text-transform: uppercase;"">Clave de Acceso SRI:</span>
                <div class=""clave-acceso"">{claveAcceso}</div>
            </div>
            <div class=""attachments-notice"">
                <span>📎 <strong>Archivos Adjuntos:</strong> En este correo encontrará adjunto su comprobante en formato <strong>PDF (RIDE)</strong> y <strong>XML firmado</strong> válido ante el SRI.</span>
            </div>
        </div>
        <div class=""footer"">
            Este es un mensaje automático generado por el sistema de Facturación Electrónica. Por favor, no responda a este correo.
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendNotaDebitoEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroNotaDebito,
        string razonSocialEmisor,
        string claveAcceso,
        decimal valorTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Envío de correo omitido para Nota de Débito {Numero}: No se proporcionó correo del comprador.", numeroNotaDebito);
            return;
        }

        var host = _configuration["Smtp:Host"] ?? "smtp-relay.brevo.com";
        var portStr = _configuration["Smtp:Port"] ?? "587";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 587;

        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var senderEmail = _configuration["Smtp:SenderEmail"] ?? "facturacion@facturafacil.ec";
        var senderName = _configuration["Smtp:SenderName"] ?? (!string.IsNullOrWhiteSpace(razonSocialEmisor) ? razonSocialEmisor : "Facturación Electrónica");

        var enabledStr = _configuration["Smtp:Enabled"];
        bool isExplicitlyDisabled = string.Equals(enabledStr, "false", StringComparison.OrdinalIgnoreCase);

        if (isExplicitlyDisabled || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Envío de correo omitido para {Email} (Nota de Débito {Numero}): Credenciales SMTP no configuradas o servicio deshabilitado.", toEmail, numeroNotaDebito);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(razonSocialComprador, toEmail.Trim()));
            message.Subject = $"Comprobante Electrónico Nota de Débito No. {numeroNotaDebito} - {razonSocialEmisor}";

            var builder = new BodyBuilder
            {
                HtmlBody = GenerarHtmlPlantillaNotaDebito(
                    razonSocialComprador,
                    numeroNotaDebito,
                    razonSocialEmisor,
                    claveAcceso,
                    valorTotal)
            };

            if (pdfRide != null && pdfRide.Length > 0)
            {
                builder.Attachments.Add(
                    $"NotaDebito_{numeroNotaDebito.Replace("-", "")}.pdf",
                    pdfRide,
                    new MimeKit.ContentType("application", "pdf"));
            }

            if (xmlFirmado != null && xmlFirmado.Length > 0)
            {
                builder.Attachments.Add(
                    $"NotaDebito_{numeroNotaDebito.Replace("-", "")}.xml",
                    xmlFirmado,
                    new MimeKit.ContentType("application", "xml"));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000;

            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await client.AuthenticateAsync(userName, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Correo de Nota de Débito {Numero} enviado exitosamente a {Email}.", numeroNotaDebito, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo de la Nota de Débito {Numero} a {Email}.", numeroNotaDebito, toEmail);
        }
    }

    private static string GenerarHtmlPlantillaNotaDebito(
        string comprador,
        string numeroNotaDebito,
        string emisor,
        string claveAcceso,
        decimal total)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f6f9;
            margin: 0;
            padding: 20px;
            color: #333333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%);
            color: #ffffff;
            padding: 30px 24px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 700;
        }}
        .header p {{
            margin: 8px 0 0 0;
            font-size: 14px;
            opacity: 0.9;
        }}
        .content {{
            padding: 24px;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
        }}
        .invoice-card {{
            background-color: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 16px;
            margin-bottom: 20px;
        }}
        .invoice-field {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            font-size: 14px;
        }}
        .invoice-field:last-child {{
            margin-bottom: 0;
        }}
        .invoice-field.total {{
            border-top: 1px dashed #cbd5e1;
            padding-top: 10px;
            margin-top: 10px;
            font-size: 16px;
            font-weight: 700;
        }}
        .clave-acceso {{
            word-break: break-all;
            background: #f1f5f9;
            padding: 10px;
            border-radius: 4px;
            font-family: monospace;
            font-size: 11px;
            color: #475569;
            margin-top: 6px;
            border: 1px solid #e2e8f0;
        }}
        .attachments-notice {{
            background-color: #eff6ff;
            border-left: 4px solid #3b82f6;
            padding: 12px 16px;
            border-radius: 0 4px 4px 0;
            font-size: 13px;
            color: #1e40af;
            margin: 20px 0;
        }}
        .footer {{
            background-color: #f8fafc;
            padding: 16px 24px;
            text-align: center;
            font-size: 12px;
            color: #94a3b8;
            border-top: 1px solid #f1f5f9;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Nota de Débito Electrónica</h1>
            <p>{emisor}</p>
        </div>
        <div class=""content"">
            <div class=""greeting"">
                Estimado(a) <strong>{comprador}</strong>,
            </div>
            <p style=""font-size: 14px; line-height: 1.5; color: #4b5563;"">
                Le informamos que ha sido emitida y autorizada por el SRI una <strong>Nota de Débito</strong> a su nombre con los siguientes datos:
            </p>
            <div class=""invoice-card"">
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Nro. Comprobante:</span>
                    <strong>{numeroNotaDebito}</strong>
                </div>
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Emisor:</span>
                    <span>{emisor}</span>
                </div>
                <div class=""invoice-field total"">
                    <span>Valor Total:</span>
                    <span style=""color: #16a34a;"">${total:F2} USD</span>
                </div>
            </div>
            <div style=""margin: 20px 0 10px 0;"">
                <span style=""font-size: 12px; font-weight: 600; color: #475569; text-transform: uppercase;"">Clave de Acceso SRI:</span>
                <div class=""clave-acceso"">{claveAcceso}</div>
            </div>
            <div class=""attachments-notice"">
                <span>📎 <strong>Archivos Adjuntos:</strong> En este correo encontrará adjunto su comprobante en formato <strong>PDF (RIDE)</strong> y <strong>XML firmado</strong> válido ante el SRI.</span>
            </div>
        </div>
        <div class=""footer"">
            Este es un mensaje automático generado por el sistema de Facturación Electrónica. Por favor, no responda a este correo.
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendNotaCreditoEmailAsync(
        string toEmail,
        string razonSocialComprador,
        string numeroNotaCredito,
        string razonSocialEmisor,
        string claveAcceso,
        decimal valorTotal,
        byte[] pdfRide,
        byte[] xmlFirmado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Envío de correo omitido para Nota de Crédito {Numero}: No se proporcionó correo del comprador.", numeroNotaCredito);
            return;
        }

        var host = _configuration["Smtp:Host"] ?? "smtp-relay.brevo.com";
        var portStr = _configuration["Smtp:Port"] ?? "587";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 587;

        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var senderEmail = _configuration["Smtp:SenderEmail"] ?? "facturacion@facturafacil.ec";
        var senderName = _configuration["Smtp:SenderName"] ?? (!string.IsNullOrWhiteSpace(razonSocialEmisor) ? razonSocialEmisor : "Facturación Electrónica");

        var enabledStr = _configuration["Smtp:Enabled"];
        bool isExplicitlyDisabled = string.Equals(enabledStr, "false", StringComparison.OrdinalIgnoreCase);

        if (isExplicitlyDisabled || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Envío de correo omitido para {Email} (Nota de Crédito {Numero}): Credenciales SMTP no configuradas o servicio deshabilitado.", toEmail, numeroNotaCredito);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(razonSocialComprador, toEmail));
            message.Subject = $"{senderName} - Nota de Crédito Electrónica {numeroNotaCredito}";

            var builder = new BodyBuilder
            {
                HtmlBody = GenerarHtmlPlantillaNotaCredito(
                    comprador: razonSocialComprador,
                    numeroNotaCredito: numeroNotaCredito,
                    emisor: razonSocialEmisor,
                    claveAcceso: claveAcceso,
                    total: valorTotal)
            };

            if (pdfRide != null && pdfRide.Length > 0)
            {
                builder.Attachments.Add(
                    $"NotaCredito_{numeroNotaCredito.Replace("-", "")}.pdf",
                    pdfRide,
                    new MimeKit.ContentType("application", "pdf"));
            }

            if (xmlFirmado != null && xmlFirmado.Length > 0)
            {
                builder.Attachments.Add(
                    $"NotaCredito_{numeroNotaCredito.Replace("-", "")}.xml",
                    xmlFirmado,
                    new MimeKit.ContentType("application", "xml"));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000;

            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await client.AuthenticateAsync(userName, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Correo de Nota de Crédito {Numero} enviado exitosamente a {Email}.", numeroNotaCredito, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo de la Nota de Crédito {Numero} a {Email}.", numeroNotaCredito, toEmail);
        }
    }

    private static string GenerarHtmlPlantillaNotaCredito(
        string comprador,
        string numeroNotaCredito,
        string emisor,
        string claveAcceso,
        decimal total)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f6f9;
            margin: 0;
            padding: 20px;
            color: #333333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%);
            color: #ffffff;
            padding: 30px 24px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 700;
        }}
        .header p {{
            margin: 8px 0 0 0;
            font-size: 14px;
            opacity: 0.9;
        }}
        .content {{
            padding: 24px;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
        }}
        .invoice-card {{
            background-color: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 16px;
            margin-bottom: 20px;
        }}
        .invoice-field {{
            display: flex;
            justify-content: space-between;
            padding: 6px 0;
            border-bottom: 1px solid #edf2f7;
            font-size: 14px;
        }}
        .invoice-field:last-child {{
            border-bottom: none;
        }}
        .invoice-field.total {{
            font-size: 16px;
            font-weight: bold;
            padding-top: 10px;
            border-top: 2px solid #cbd5e1;
        }}
        .clave-acceso {{
            font-family: Consolas, Monaco, monospace;
            background-color: #f8fafc;
            border: 1px dashed #cbd5e1;
            padding: 10px;
            border-radius: 4px;
            font-size: 12px;
            word-break: break-all;
            color: #475569;
            margin-top: 6px;
        }}
        .attachments-notice {{
            background-color: #f0fdf4;
            border: 1px solid #bbf7d0;
            border-radius: 6px;
            padding: 14px 18px;
            margin: 20px 0;
            font-size: 13px;
            color: #166534;
        }}
        .footer {{
            background-color: #f8fafc;
            padding: 16px 24px;
            text-align: center;
            font-size: 12px;
            color: #94a3b8;
            border-top: 1px solid #f1f5f9;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Nota de Crédito Electrónica</h1>
            <p>{emisor}</p>
        </div>
        <div class=""content"">
            <div class=""greeting"">
                Estimado(a) <strong>{comprador}</strong>,
            </div>
            <p style=""font-size: 14px; line-height: 1.5; color: #4b5563;"">
                Le informamos que ha sido emitida y autorizada por el SRI una <strong>Nota de Crédito</strong> a su nombre con los siguientes datos:
            </p>
            <div class=""invoice-card"">
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Nro. Comprobante:</span>
                    <strong>{numeroNotaCredito}</strong>
                </div>
                <div class=""invoice-field"">
                    <span style=""color: #64748b;"">Emisor:</span>
                    <span>{emisor}</span>
                </div>
                <div class=""invoice-field total"">
                    <span>Valor Modificación:</span>
                    <span style=""color: #16a34a;"">${total:F2} USD</span>
                </div>
            </div>
            <div style=""margin: 20px 0 10px 0;"">
                <span style=""font-size: 12px; font-weight: 600; color: #475569; text-transform: uppercase;"">Clave de Acceso SRI:</span>
                <div class=""clave-acceso"">{claveAcceso}</div>
            </div>
            <div class=""attachments-notice"">
                <span>📎 <strong>Archivos Adjuntos:</strong> En este correo encontrará adjunto su comprobante en formato <strong>PDF (RIDE)</strong> y <strong>XML firmado</strong> válido ante el SRI.</span>
            </div>
        </div>
        <div class=""footer"">
            Este es un mensaje automático generado por el sistema de Facturación Electrónica. Por favor, no responda a este correo.
        </div>
    </div>
</body>
</html>";
    }
}
