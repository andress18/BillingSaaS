using System.Net;
using System.Text;
using BillingSaaS.Infrastructure.Servicios.Sri;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace BillingSaaS.Infrastructure.IntegrationTests;

[TestFixture]
public class SriServicesTests
{
    [Test]
    public void SriEndpoints_DebeRetornarUrlsCorrectas_ParaCadaAmbiente()
    {
        // Pruebas (1)
        SriEndpoints.ObtenerUrlRecepcion(1).ShouldBe("https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline");
        SriEndpoints.ObtenerUrlAutorizacion(1).ShouldBe("https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline");

        // Producción (2)
        SriEndpoints.ObtenerUrlRecepcion(2).ShouldBe("https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline");
        SriEndpoints.ObtenerUrlAutorizacion(2).ShouldBe("https://cel.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline");

        // Inválido
        Should.Throw<ArgumentOutOfRangeException>(() => SriEndpoints.ObtenerUrlRecepcion(99));
        Should.Throw<ArgumentOutOfRangeException>(() => SriEndpoints.ObtenerUrlAutorizacion(0));
    }

    [Test]
    public void ParsearRespuestaRecepcion_CuandoEsRecibida_DebeMapearCorrectamente()
    {
        var xmlRespuesta = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:validarComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.recepcion"">
            <RespuestaRecepcionComprobante>
                <estado>RECIBIDA</estado>
                <comprobantes/>
            </RespuestaRecepcionComprobante>
        </ns2:validarComprobanteResponse>
    </soap:Body>
</soap:Envelope>";

        var resultado = SriRecepcionService.ParsearRespuestaRecepcion(xmlRespuesta);

        resultado.Estado.ShouldBe("RECIBIDA");
        resultado.EsRecibida.ShouldBeTrue();
        resultado.Comprobantes.ShouldBeEmpty();
    }

    [Test]
    public void ParsearRespuestaRecepcion_CuandoEsDevuelta_DebeMapearErroresYComprobante()
    {
        var xmlRespuesta = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:validarComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.recepcion"">
            <RespuestaRecepcionComprobante>
                <estado>DEVUELTA</estado>
                <comprobantes>
                    <comprobante>
                        <claveAcceso>1109202601179000000000110010010000000011234567818</claveAcceso>
                        <mensajes>
                            <mensaje>
                                <identificador>43</identificador>
                                <mensaje>CLAVE ACCESO REGISTRADA</mensaje>
                                <informacionAdicional>La clave ya existe en el SRI</informacionAdicional>
                                <tipo>ERROR</tipo>
                            </mensaje>
                            <mensaje>
                                <identificador>35</identificador>
                                <mensaje>DOCUMENTO NO AUTORIZADO</mensaje>
                                <tipo>ERROR</tipo>
                            </mensaje>
                        </mensajes>
                    </comprobante>
                </comprobantes>
            </RespuestaRecepcionComprobante>
        </ns2:validarComprobanteResponse>
    </soap:Body>
</soap:Envelope>";

        var resultado = SriRecepcionService.ParsearRespuestaRecepcion(xmlRespuesta);

        resultado.Estado.ShouldBe("DEVUELTA");
        resultado.EsRecibida.ShouldBeFalse();
        resultado.Comprobantes.Count.ShouldBe(1);

        var comp = resultado.Comprobantes[0];
        comp.ClaveAcceso.ShouldBe("1109202601179000000000110010010000000011234567818");
        comp.Mensajes.Count.ShouldBe(2);

        comp.Mensajes[0].Identificador.ShouldBe("43");
        comp.Mensajes[0].Mensaje.ShouldBe("CLAVE ACCESO REGISTRADA");
        comp.Mensajes[0].InformacionAdicional.ShouldBe("La clave ya existe en el SRI");
        comp.Mensajes[0].Tipo.ShouldBe("ERROR");

        comp.Mensajes[1].Identificador.ShouldBe("35");
        comp.Mensajes[1].InformacionAdicional.ShouldBeNull();
    }

    [Test]
    public async Task ValidarComprobanteAsync_DebeEnviarSobreSoapValidoConBase64()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            if (request.Content != null)
            {
                capturedBody = await request.Content.ReadAsStringAsync();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                    <soap:Body>
                        <ns2:validarComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.recepcion"">
                            <RespuestaRecepcionComprobante>
                                <estado>RECIBIDA</estado>
                            </RespuestaRecepcionComprobante>
                        </ns2:validarComprobanteResponse>
                    </soap:Body>
                </soap:Envelope>", Encoding.UTF8, "text/xml")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new SriRecepcionService(httpClient, NullLogger<SriRecepcionService>.Instance);

        var xmlBytes = Encoding.UTF8.GetBytes("<factura id='comprobante'></factura>");
        var resultado = await service.ValidarComprobanteAsync(xmlBytes, 1);

        resultado.EsRecibida.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        capturedRequest.RequestUri!.ToString().ShouldBe(SriEndpoints.RecepcionPruebas);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("<ec:validarComprobante>");
        capturedBody.ShouldContain(Convert.ToBase64String(xmlBytes));
    }

    [Test]
    public void ParsearRespuestaAutorizacion_CuandoEsAutorizado_DebeMapearNumeroFechaYComprobante()
    {
        var xmlRespuesta = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:autorizacionComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.autorizacion"">
            <RespuestaAutorizacion>
                <claveAccesoConsultada>1109202601179000000000110010010000000011234567818</claveAccesoConsultada>
                <numeroComprobantes>1</numeroComprobantes>
                <autorizaciones>
                    <autorizacion>
                        <estado>AUTORIZADO</estado>
                        <numeroAutorizacion>1109202601179000000000110010010000000011234567818</numeroAutorizacion>
                        <fechaAutorizacion>2026-09-11T23:50:00-05:00</fechaAutorizacion>
                        <ambiente>PRUEBAS</ambiente>
                        <comprobante><![CDATA[<?xml version=""1.0"" encoding=""UTF-8""?><factura></factura>]]></comprobante>
                        <mensajes/>
                    </autorizacion>
                </autorizaciones>
            </RespuestaAutorizacion>
        </ns2:autorizacionComprobanteResponse>
    </soap:Body>
</soap:Envelope>";

        var resultado = SriAutorizacionService.ParsearRespuestaAutorizacion(xmlRespuesta);

        resultado.ClaveAccesoConsultada.ShouldBe("1109202601179000000000110010010000000011234567818");
        resultado.NumeroComprobantes.ShouldBe("1");
        resultado.EstaAutorizado.ShouldBeTrue();
        resultado.Autorizaciones.Count.ShouldBe(1);

        var aut = resultado.PrimeraAutorizacion!;
        aut.Estado.ShouldBe("AUTORIZADO");
        aut.NumeroAutorizacion.ShouldBe("1109202601179000000000110010010000000011234567818");
        aut.Ambiente.ShouldBe("PRUEBAS");
        aut.ComprobanteXml.ShouldNotBeNull();
        aut.ComprobanteXml.ShouldContain("<factura></factura>");
        aut.FechaAutorizacion.ShouldNotBeNull();
        aut.FechaAutorizacion.Value.Year.ShouldBe(2026);
    }

    [Test]
    public void ParsearRespuestaAutorizacion_CuandoNoEsAutorizado_DebeMapearMensajesDeError()
    {
        var xmlRespuesta = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:autorizacionComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.autorizacion"">
            <RespuestaAutorizacion>
                <claveAccesoConsultada>1109202601179000000000110010010000000011234567818</claveAccesoConsultada>
                <numeroComprobantes>1</numeroComprobantes>
                <autorizaciones>
                    <autorizacion>
                        <estado>NO AUTORIZADO</estado>
                        <fechaAutorizacion>2026-09-11T23:50:00-05:00</fechaAutorizacion>
                        <ambiente>PRUEBAS</ambiente>
                        <mensajes>
                            <mensaje>
                                <identificador>39</identificador>
                                <mensaje>FIRMA INVALIDA</mensaje>
                                <informacionAdicional>Certificado revocado</informacionAdicional>
                                <tipo>ERROR</tipo>
                            </mensaje>
                        </mensajes>
                    </autorizacion>
                </autorizaciones>
            </RespuestaAutorizacion>
        </ns2:autorizacionComprobanteResponse>
    </soap:Body>
</soap:Envelope>";

        var resultado = SriAutorizacionService.ParsearRespuestaAutorizacion(xmlRespuesta);

        resultado.EstaAutorizado.ShouldBeFalse();
        var aut = resultado.PrimeraAutorizacion!;
        aut.Estado.ShouldBe("NO AUTORIZADO");
        aut.NumeroAutorizacion.ShouldBeNull();
        aut.Mensajes.Count.ShouldBe(1);
        aut.Mensajes[0].Identificador.ShouldBe("39");
        aut.Mensajes[0].Mensaje.ShouldBe("FIRMA INVALIDA");
        aut.Mensajes[0].InformacionAdicional.ShouldBe("Certificado revocado");
    }

    [Test]
    public async Task ConsultarAutorizacionAsync_DebeEnviarSobreSoapValidoConClaveAcceso()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            if (request.Content != null)
            {
                capturedBody = await request.Content.ReadAsStringAsync();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                    <soap:Body>
                        <ns2:autorizacionComprobanteResponse xmlns:ns2=""http://ec.gob.sri.ws.autorizacion"">
                            <RespuestaAutorizacion>
                                <claveAccesoConsultada>1109202601179000000000110010010000000011234567818</claveAccesoConsultada>
                                <numeroComprobantes>0</numeroComprobantes>
                                <autorizaciones/>
                            </RespuestaAutorizacion>
                        </ns2:autorizacionComprobanteResponse>
                    </soap:Body>
                </soap:Envelope>", Encoding.UTF8, "text/xml")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new SriAutorizacionService(httpClient, NullLogger<SriAutorizacionService>.Instance);

        var claveAcceso = "1109202601179000000000110010010000000011234567818";
        var resultado = await service.ConsultarAutorizacionAsync(claveAcceso, 2);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.RequestUri!.ToString().ShouldBe(SriEndpoints.AutorizacionProduccion);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("<ec:autorizacionComprobante>");
        capturedBody.ShouldContain($"<claveAcceso>{claveAcceso}</claveAcceso>");
        resultado.ClaveAccesoConsultada.ShouldBe(claveAcceso);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handlerFunc(request);
        }
    }
}
