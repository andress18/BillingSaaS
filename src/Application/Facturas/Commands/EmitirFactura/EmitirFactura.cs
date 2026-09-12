using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Services;

namespace BillingSaaS.Application.Facturas.Commands.EmitirFactura;

public record EmitirFacturaCommand : IRequest<EmitirFacturaResponseDto>
{
    public int EmisorId { get; init; }
    public CompradorDto Cliente { get; init; } = null!;
    public List<DetalleDto> Detalles { get; init; } = new();

    public record CompradorDto(
        string TipoIdentificacion,
        string Identificacion,
        string RazonSocial,
        string? Direccion,
        string? CorreoElectronico);

    public record DetalleDto(
        string CodigoPrincipal,
        string Descripcion,
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal Descuento,
        List<ImpuestoDto> Impuestos);

    public record ImpuestoDto(string Codigo, string CodigoPorcentaje, decimal Tarifa, decimal BaseImponible);
}

public class EmitirFacturaCommandHandler : IRequestHandler<EmitirFacturaCommand, EmitirFacturaResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturaXmlGenerator _xmlGenerator;
    private readonly ISriSignatureService _signatureService;
    private readonly ISriRecepcionService _recepcionService;

    public EmitirFacturaCommandHandler(
        IApplicationDbContext context,
        IFacturaXmlGenerator xmlGenerator,
        ISriSignatureService signatureService,
        ISriRecepcionService recepcionService)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _signatureService = signatureService;
        _recepcionService = recepcionService;
    }

    public async Task<EmitirFacturaResponseDto> Handle(EmitirFacturaCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener Emisor configurado desde la base de datos
        var emisor = await _context.Emisores.FindAsync([request.EmisorId], cancellationToken);
        Guard.Against.NotFound(request.EmisorId, emisor);

        if (!emisor.Activo)
            throw new InvalidOperationException("El emisor se encuentra inactivo.");

        if (!emisor.TieneCertificadoValido())
            throw new InvalidOperationException("El emisor no tiene un certificado digital válido configurado o ya ha caducado.");

        // 2. Obtener siguiente número secuencial atómico
        var secuencial = emisor.ObtenerSiguienteSecuencialFactura();

        // 3. Crear objetos de valor de Dominio (Comprador, Detalles e Impuestos)
        var comprador = Comprador.Crear(
            request.Cliente.TipoIdentificacion,
            request.Cliente.Identificacion,
            request.Cliente.RazonSocial,
            request.Cliente.Direccion,
            request.Cliente.CorreoElectronico
        );

        var detalles = request.Detalles.Select(d => DetalleFactura.Crear(
            d.CodigoPrincipal,
            d.Descripcion,
            d.Cantidad,
            d.PrecioUnitario,
            d.Descuento,
            d.Impuestos.Select(i => Impuesto.Crear(i.Codigo, i.CodigoPorcentaje, i.Tarifa, i.BaseImponible)).ToList()
        )).ToList();

        // 4. Crear entidad Factura en el huso horario oficial de Ecuador (UTC-5)
        var fechaEmision = DateTime.UtcNow.AddHours(-5).Date;

        var factura = Factura.Crear(
            tenantId: emisor.TenantId,
            ambiente: emisor.Ambiente,
            razonSocial: emisor.RazonSocial,
            rucEmisor: emisor.Ruc,
            establecimiento: emisor.CodigoEstablecimiento,
            puntoEmision: emisor.PuntoEmision,
            secuencial: secuencial,
            direccionMatriz: emisor.DireccionMatriz,
            fechaEmision: fechaEmision,
            cliente: comprador,
            detalles: detalles,
            emisorId: emisor.Id
        );

        // 5. Generar Clave de Acceso de 49 dígitos con Módulo 11
        string fechaFormato = fechaEmision.ToString("ddMMyyyy");
        string codigoNumerico = Random.Shared.Next(10000000, 99999999).ToString();
        string tipoEmision = "1";
        string cadenaBase = $"{fechaFormato}01{emisor.Ruc}{emisor.Ambiente}{emisor.CodigoEstablecimiento}{emisor.PuntoEmision}{secuencial}{codigoNumerico}{tipoEmision}";
        string claveAcceso = ClaveAccesoService.GenerarDigitoVerificador(cadenaBase);
        factura.AsignarClaveAcceso(claveAcceso);

        // 6. Generar y Firmar XML v1.1.0 con XAdES-BES
        var xmlSinFirma = _xmlGenerator.GenerarXml(factura);
        var xmlFirmadoDoc = _signatureService.FirmarXml(xmlSinFirma, emisor.CertificadoDigital!, emisor.PasswordCertificado!);
        byte[] xmlFirmadoBytes = Encoding.UTF8.GetBytes(xmlFirmadoDoc.OuterXml);

        // 7. Transmisión al Web Service de Recepción del SRI (con manejo de contingencia)
        string? mensajeDevolucion = null;
        bool esRecibida = false;

        try
        {
            var recepcionResult = await _recepcionService.ValidarComprobanteAsync(xmlFirmadoBytes, emisor.Ambiente, cancellationToken);
            esRecibida = recepcionResult.EsRecibida;

            if (recepcionResult.EsRecibida)
            {
                factura.MarcarComoRecibida();
            }
            else
            {
                var mensajes = recepcionResult.Comprobantes
                    .SelectMany(c => c.Mensajes)
                    .Select(m => $"[{m.Tipo}] ({m.Identificador}): {m.Mensaje} {m.InformacionAdicional}")
                    .ToList();

                mensajeDevolucion = string.Join(" | ", mensajes);
                factura.MarcarComoDevuelta(mensajeDevolucion);
            }
        }
        catch (HttpRequestException ex)
        {
            // Si el servidor del SRI no responde (caída de celcer / reset de conexión), la factura queda generada, firmada y guardada localmente
            mensajeDevolucion = $"Servidor SRI no disponible temporalmente: {ex.Message}. El comprobante quedó firmado y listo para reintento.";
            factura.MarcarComoDevuelta(mensajeDevolucion);
        }

        // 8. Persistencia de la factura y secuencial del emisor
        _context.Facturas.Add(factura);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmitirFacturaResponseDto
        {
            FacturaId = factura.Id,
            ClaveAcceso = factura.ClaveAcceso,
            Secuencial = $"{emisor.CodigoEstablecimiento}-{emisor.PuntoEmision}-{secuencial}",
            Estado = factura.Estado,
            EsRecibida = esRecibida,
            MensajeDevolucion = mensajeDevolucion
        };
    }
}
