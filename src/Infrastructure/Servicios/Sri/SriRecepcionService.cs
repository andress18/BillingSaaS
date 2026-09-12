using System.Text;
using System.Xml.Linq;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Servicios.Sri;

public class SriRecepcionService : ISriRecepcionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SriRecepcionService> _logger;

    public SriRecepcionService(HttpClient httpClient, ILogger<SriRecepcionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SriRecepcionResponseDto> ValidarComprobanteAsync(
        byte[] xmlFirmadoBytes,
        int ambiente,
        CancellationToken cancellationToken = default)
    {
        var url = SriEndpoints.ObtenerUrlRecepcion(ambiente);
        var base64Xml = Convert.ToBase64String(xmlFirmadoBytes);

        var soapRequest = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ec=""http://ec.gob.sri.ws.recepcion"">
    <soapenv:Header/>
    <soapenv:Body>
        <ec:validarComprobante>
            <xml>{base64Xml}</xml>
        </ec:validarComprobante>
    </soapenv:Body>
</soapenv:Envelope>";

        _logger.LogInformation("Enviando comprobante al SRI Recepción (Ambiente: {Ambiente}, URL: {Url})", ambiente, url);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(soapRequest, Encoding.UTF8, "text/xml")
        };
        httpRequest.Headers.Add("SOAPAction", "\"\"");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseXml = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Error HTTP al comunicarse con el SRI Recepción: {StatusCode}. Respuesta: {Response}",
                httpResponse.StatusCode, responseXml);
            throw new HttpRequestException($"SRI Recepción respondió con status {httpResponse.StatusCode}: {responseXml}");
        }

        return ParsearRespuestaRecepcion(responseXml);
    }

    public static SriRecepcionResponseDto ParsearRespuestaRecepcion(string responseXml)
    {
        var result = new SriRecepcionResponseDto();

        if (string.IsNullOrWhiteSpace(responseXml))
            return result;

        var doc = XDocument.Parse(responseXml);

        var respuestaNode = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "RespuestaRecepcionComprobante");
        if (respuestaNode == null)
        {
            // Podría ser un Fault de SOAP
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null)
            {
                var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
                result.Estado = "DEVUELTA";
                result.Comprobantes.Add(new SriComprobanteRecepcionDto
                {
                    Mensajes = new List<SriMensajeDto>
                    {
                        new() { Identificador = "SOAP_FAULT", Mensaje = faultString ?? "Error SOAP desconocido", Tipo = "ERROR" }
                    }
                });
            }
            return result;
        }

        result.Estado = respuestaNode.Elements().FirstOrDefault(e => e.Name.LocalName == "estado")?.Value ?? string.Empty;

        var comprobantesNode = respuestaNode.Elements().FirstOrDefault(e => e.Name.LocalName == "comprobantes");
        if (comprobantesNode != null)
        {
            foreach (var compNode in comprobantesNode.Elements().Where(e => e.Name.LocalName == "comprobante"))
            {
                var comprobanteDto = new SriComprobanteRecepcionDto
                {
                    ClaveAcceso = compNode.Elements().FirstOrDefault(e => e.Name.LocalName == "claveAcceso")?.Value ?? string.Empty
                };

                var mensajesNode = compNode.Elements().FirstOrDefault(e => e.Name.LocalName == "mensajes");
                if (mensajesNode != null)
                {
                    foreach (var msgNode in mensajesNode.Elements().Where(e => e.Name.LocalName == "mensaje"))
                    {
                        comprobanteDto.Mensajes.Add(new SriMensajeDto
                        {
                            Identificador = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName == "identificador")?.Value ?? string.Empty,
                            Mensaje = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName == "mensaje")?.Value ?? string.Empty,
                            InformacionAdicional = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName == "informacionAdicional")?.Value,
                            Tipo = msgNode.Elements().FirstOrDefault(e => e.Name.LocalName == "tipo")?.Value ?? string.Empty
                        });
                    }
                }

                result.Comprobantes.Add(comprobanteDto);
            }
        }

        return result;
    }
}

