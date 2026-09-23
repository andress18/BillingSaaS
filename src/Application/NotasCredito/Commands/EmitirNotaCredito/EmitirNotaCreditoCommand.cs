using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Services;

namespace BillingSaaS.Application.NotasCredito.Commands.EmitirNotaCredito;

public record EmitirNotaCreditoCommand : IRequest<EmitirNotaCreditoResponseDto>
{
    public int EmisorId { get; init; }
    public string? RegimenRimpe { get; init; }
    public CompradorDto Cliente { get; init; } = null!;
    public string CodDocModificado { get; init; } = "01";
    public string NumDocModificado { get; init; } = null!;
    public DateTime FechaEmisionDocSustento { get; init; }
    public string Motivo { get; init; } = null!;
    public List<DetalleDto> Detalles { get; init; } = new();
    public Guid? CatalogoClienteId { get; init; }

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
        List<ImpuestoDto> Impuestos,
        Guid? CatalogoProductoId = null);

    public record ImpuestoDto(
        string Codigo,
        string CodigoPorcentaje,
        decimal Tarifa,
        decimal BaseImponible);
}

public class EmitirNotaCreditoCommandHandler : IRequestHandler<EmitirNotaCreditoCommand, EmitirNotaCreditoResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly INotaCreditoXmlGenerator _xmlGenerator;
    private readonly ISriSignatureService _signatureService;
    private readonly ISriRecepcionService _recepcionService;
    private readonly ICertificateEncryptionService _encryptionService;
    private readonly ISubscriptionValidationService _subscriptionValidator;
    private readonly IUser _user;

    public EmitirNotaCreditoCommandHandler(
        IApplicationDbContext context,
        INotaCreditoXmlGenerator xmlGenerator,
        ISriSignatureService signatureService,
        ISriRecepcionService recepcionService,
        ICertificateEncryptionService encryptionService,
        ISubscriptionValidationService subscriptionValidator,
        IUser user)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _signatureService = signatureService;
        _recepcionService = recepcionService;
        _encryptionService = encryptionService;
        _subscriptionValidator = subscriptionValidator;
        _user = user;
    }

    public async Task<EmitirNotaCreditoResponseDto> Handle(EmitirNotaCreditoCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener y validar el Emisor
        var emisor = await _context.Emisores.FindAsync([request.EmisorId], cancellationToken);
        Guard.Against.NotFound(request.EmisorId, emisor);

        var isAdmin = _user.Roles?.Contains(BillingSaaS.Domain.Constants.Roles.Administrator) == true;
        if (!isAdmin && _user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && emisor.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para emitir notas de crédito a nombre de este emisor.");
        }

        if (!emisor.Activo)
            throw new InvalidOperationException("El emisor se encuentra inactivo.");

        if (!emisor.TieneCertificadoValido())
            throw new InvalidOperationException("El emisor no tiene un certificado digital válido configurado o ya ha caducado.");

        // 1.1 Validar suscripción activa y límites de emisión para "04" (Nota de Crédito)
        await _subscriptionValidator.ValidarEmisionAsync(
            emisor.TenantId,
            codDoc: "04",
            codigoEstablecimiento: emisor.CodigoEstablecimiento,
            cancellationToken);

        // 2. Obtener siguiente número secuencial atómico para Nota de Crédito
        var secuencial = emisor.ObtenerSiguienteSecuencialNotaCredito();

        // 3. Crear objetos de Dominio
        var comprador = Comprador.Crear(
            request.Cliente.TipoIdentificacion,
            request.Cliente.Identificacion,
            request.Cliente.RazonSocial,
            request.Cliente.Direccion,
            request.Cliente.CorreoElectronico
        );

        var detalles = request.Detalles.Select(d =>
        {
            var impuestos = d.Impuestos.Select(i => Impuesto.Crear(
                i.Codigo,
                i.CodigoPorcentaje,
                i.Tarifa,
                i.BaseImponible
            )).ToList();

            return DetalleNotaCredito.Crear(
                d.CodigoPrincipal,
                d.Descripcion,
                d.Cantidad,
                d.PrecioUnitario,
                d.Descuento,
                impuestos,
                d.CatalogoProductoId
            );
        }).ToList();

        // 4. Crear entidad NotaCredito en el huso horario oficial de Ecuador (UTC-5)
        var fechaEmision = DateTime.UtcNow.AddHours(-5).Date;

        var regimenRimpe = !string.IsNullOrWhiteSpace(request.RegimenRimpe)
            ? Domain.Constants.RegimenRimpeTipos.Normalizar(request.RegimenRimpe)
            : emisor.RegimenRimpe;

        var notaCredito = NotaCredito.Crear(
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
            numDocModificado: request.NumDocModificado,
            fechaEmisionDocSustento: request.FechaEmisionDocSustento,
            motivo: request.Motivo,
            detalles: detalles,
            codDocModificado: request.CodDocModificado,
            emisorId: emisor.Id,
            contribuyenteRimpe: regimenRimpe,
            catalogoClienteId: request.CatalogoClienteId
        );

        // 5. Generar Clave de Acceso de 49 dígitos con Módulo 11 (codDoc = 04)
        string fechaFormato = fechaEmision.ToString("ddMMyyyy");
        string codigoNumerico = Random.Shared.Next(10000000, 99999999).ToString();
        string tipoEmision = "1";
        string cadenaBase = $"{fechaFormato}04{emisor.Ruc}{emisor.Ambiente}{emisor.CodigoEstablecimiento}{emisor.PuntoEmision}{secuencial}{codigoNumerico}{tipoEmision}";
        string claveAcceso = ClaveAccesoService.GenerarDigitoVerificador(cadenaBase);
        notaCredito.AsignarClaveAcceso(claveAcceso);

        // 6. Generar y Firmar XML v1.1.0 con XAdES-BES
        var xmlSinFirma = _xmlGenerator.GenerarXml(notaCredito);

        var aad = emisor.TenantId.ToByteArray();
        var rawCertBytes = _encryptionService.Decrypt(emisor.CertificadoDigital!, aad);
        var rawPassword = _encryptionService.DecryptString(emisor.PasswordCertificado!, aad);

        var xmlFirmadoDoc = _signatureService.FirmarXml(xmlSinFirma, rawCertBytes, rawPassword);
        byte[] xmlFirmadoBytes = Encoding.UTF8.GetBytes(xmlFirmadoDoc.OuterXml);
        notaCredito.AsignarXmlFirmado(xmlFirmadoDoc.OuterXml);

        // 7. Transmisión al Web Service de Recepción del SRI
        string? mensajeDevolucion = null;
        bool esRecibida = false;

        try
        {
            var recepcionResult = await _recepcionService.ValidarComprobanteAsync(xmlFirmadoBytes, emisor.Ambiente, cancellationToken);
            esRecibida = recepcionResult.EsRecibida;

            if (recepcionResult.EsRecibida)
            {
                notaCredito.MarcarComoRecibida();
            }
            else
            {
                var mensajes = recepcionResult.Comprobantes
                    .SelectMany(c => c.Mensajes)
                    .Select(m => $"[{m.Tipo}] ({m.Identificador}): {m.Mensaje} {m.InformacionAdicional}")
                    .ToList();

                mensajeDevolucion = string.Join(" | ", mensajes);
                notaCredito.MarcarComoDevuelta(mensajeDevolucion);
            }
        }
        catch (HttpRequestException ex)
        {
            mensajeDevolucion = $"Servidor SRI no disponible temporalmente: {ex.Message}. El comprobante quedó firmado y listo para reintento.";
            notaCredito.MarcarComoDevuelta(mensajeDevolucion);
        }

        // 8. Persistencia transaccional
        _context.NotasCredito.Add(notaCredito);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmitirNotaCreditoResponseDto
        {
            NotaCreditoId = notaCredito.Id,
            ClaveAcceso = notaCredito.ClaveAcceso,
            Secuencial = $"{emisor.CodigoEstablecimiento}-{emisor.PuntoEmision}-{secuencial}",
            Estado = notaCredito.Estado,
            EsRecibida = esRecibida,
            MensajeDevolucion = mensajeDevolucion
        };
    }
}

