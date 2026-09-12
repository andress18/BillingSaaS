using System.Globalization;
using System.Text;
using System.Xml.Linq;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Servicios.Sri;

public class SriAutorizacionService : ISriAutorizacionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SriAutorizacionService> _logger;

    public SriAutorizacionService(HttpClient httpClient, ILogger<SriAutorizacionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SriAutorizacionResponseDto> ConsultarAutorizacionAsync(
        string claveAcceso,
        int ambiente,
        CancellationToken cancellationToken = default)
    {
        var url = SriEndpoints.ObtenerUrlAutorizacion(ambiente);

        var soapRequest = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.autorizacion"">
    <soapenv:Header/>
    <soapenv:Body>
        <ec:autorizacionComprobante>
            <claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante>
        </ec:autorizacionComprobante>
    </soapenv:Body>
</soapenv:Envelope>";

        _logger.LogInformation("Consultando autorización en el SRI (Clave: {Clave}, Ambiente: {Ambiente}, URL: {Url})",
            claveAcceso, ambiente, url);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(soapRequest, Encoding.UTF8, "text/xml")
        };
        httpRequest.Headers.Add("SOAPAction", "\"\"");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseXml = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Error HTTP al comunicarse con el SRI Autorización: {StatusCode}. Respuesta: {Response}",
                httpResponse.StatusCode, responseXml);
            throw new HttpRequestException($"SRI Autorización respondió con status {httpResponse.StatusCode}: {responseXml}");
        }

        return ParsearRespuestaAutorizacion(responseXml);
    }

    public static SriAutorizacionResponseDto ParsearRespuestaAutorizacion(string responseXml)
    {
        var result = new SriAutorizacionResponseDto();

        if (string.IsNullOrWhiteSpace(responseXml))
            return result;

        var doc = XDocument.Parse(responseXml);

        var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
        if (fault != null)
        {
            var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase))?.Value;
            result.Autorizaciones.Add(new SriAutorizacionDto
            {
                Estado = "ERROR",
                Mensajes = new List<SriMensajeDto>
                {
                    new() { Identificador = "SOAP_FAULT", Mensaje = faultString ?? "Error SOAP desconocido", Tipo = "ERROR" }
                }
            });
            return result;
        }

        result.ClaveAccesoConsultada = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("claveAccesoConsultada", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        result.NumeroComprobantes = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("numeroComprobantes", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        var autNodes = doc.Descendants().Where(e => e.Name.LocalName.Equals("autorizacion", StringComparison.OrdinalIgnoreCase));
        foreach (var autNode in autNodes)
        {
            var autDto = new SriAutorizacionDto
            {
                Estado = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("estado", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                NumeroAutorizacion = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("numeroAutorizacion", StringComparison.OrdinalIgnoreCase))?.Value,
                Ambiente = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("ambiente", StringComparison.OrdinalIgnoreCase))?.Value,
                ComprobanteXml = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("comprobante", StringComparison.OrdinalIgnoreCase))?.Value
            };

            var fechaStr = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("fechaAutorizacion", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(fechaStr) && DateTime.TryParse(fechaStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                autDto.FechaAutorizacion = fecha;
            }

            var mensajesNode = autNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("mensajes", StringComparison.OrdinalIgnoreCase));
            if (mensajesNode != null)
            {
                foreach (var msgNode in mensajesNode.Elements().Where(e => e.Name.LocalName.Equals("mensaje", StringComparison.OrdinalIgnoreCase)))
                {
                    autDto.Mensajes.Add(new SriMensajeDto
                    {
                        Identificador = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("identificador", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                        Mensaje = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("mensaje", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                        InformacionAdicional = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("informacionAdicional", StringComparison.OrdinalIgnoreCase))?.Value,
                        Tipo = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("tipo", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty
                    });
                }
            }

            result.Autorizaciones.Add(autDto);
        }

        return result;
    }
}

