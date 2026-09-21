using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Services;

namespace BillingSaaS.Application.NotasDebito.Commands.EmitirNotaDebito;

public record EmitirNotaDebitoCommand : IRequest<EmitirNotaDebitoResponseDto>
{
    public int EmisorId { get; init; }
    public string? RegimenRimpe { get; init; }
    public CompradorDto Cliente { get; init; } = null!;
    public string CodDocModificado { get; init; } = "01";
    public string NumDocModificado { get; init; } = null!;
    public DateTime FechaEmisionDocSustento { get; init; }
    public List<MotivoDto> Motivos { get; init; } = new();
    public List<ImpuestoDto> Impuestos { get; init; } = new();
    public List<PagoDto>? Pagos { get; init; }

    public record CompradorDto(
        string TipoIdentificacion,
        string Identificacion,
        string RazonSocial,
        string? Direccion,
        string? CorreoElectronico);

    public record MotivoDto(string Razon, decimal Valor);

    public record ImpuestoDto(string Codigo, string CodigoPorcentaje, decimal Tarifa, decimal BaseImponible);

    public record PagoDto(string FormaPago, decimal Total, decimal? Plazo, string? UnidadTiempo);
}

public class EmitirNotaDebitoCommandHandler : IRequestHandler<EmitirNotaDebitoCommand, EmitirNotaDebitoResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly INotaDebitoXmlGenerator _xmlGenerator;
    private readonly ISriSignatureService _signatureService;
    private readonly ISriRecepcionService _recepcionService;
    private readonly ICertificateEncryptionService _encryptionService;
    private readonly ISubscriptionValidationService _subscriptionValidator;
    private readonly IUser _user;

    public EmitirNotaDebitoCommandHandler(
        IApplicationDbContext context,
        INotaDebitoXmlGenerator xmlGenerator,
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

    public async Task<EmitirNotaDebitoResponseDto> Handle(EmitirNotaDebitoCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener y validar el Emisor
        var emisor = await _context.Emisores.FindAsync([request.EmisorId], cancellationToken);
        Guard.Against.NotFound(request.EmisorId, emisor);

        var isAdmin = _user.Roles?.Contains(BillingSaaS.Domain.Constants.Roles.Administrator) == true;
        if (!isAdmin && _user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && emisor.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para emitir notas de débito a nombre de este emisor.");
        }

        if (!emisor.Activo)
            throw new InvalidOperationException("El emisor se encuentra inactivo.");

        if (!emisor.TieneCertificadoValido())
            throw new InvalidOperationException("El emisor no tiene un certificado digital válido configurado o ya ha caducado.");

        // 1.1 Validar suscripción activa y límites de emisión para "05" (Nota de Débito)
        await _subscriptionValidator.ValidarEmisionAsync(
            emisor.TenantId,
            codDoc: "05",
            codigoEstablecimiento: emisor.CodigoEstablecimiento,
            cancellationToken);

        // 2. Obtener siguiente número secuencial atómico para Nota de Débito
        var secuencial = emisor.ObtenerSiguienteSecuencialNotaDebito();

        // 3. Crear objetos de Dominio
        var comprador = Comprador.Crear(
            request.Cliente.TipoIdentificacion,
            request.Cliente.Identificacion,
            request.Cliente.RazonSocial,
            request.Cliente.Direccion,
            request.Cliente.CorreoElectronico
        );

        var motivos = request.Motivos.Select(m => MotivoNotaDebito.Crear(m.Razon, m.Valor)).ToList();

        var impuestos = request.Impuestos.Select(i => ImpuestoNotaDebito.Crear(
            i.Codigo,
            i.CodigoPorcentaje,
            i.Tarifa,
            i.BaseImponible
        )).ToList();

        var pagos = request.Pagos?.Select(p => PagoNotaDebito.Crear(
            p.FormaPago,
            p.Total,
            p.Plazo,
            p.UnidadTiempo
        )).ToList();

        // 4. Crear entidad NotaDebito en el huso horario oficial de Ecuador (UTC-5)
        var fechaEmision = DateTime.UtcNow.AddHours(-5).Date;

        var regimenRimpe = !string.IsNullOrWhiteSpace(request.RegimenRimpe)
            ? Domain.Constants.RegimenRimpeTipos.Normalizar(request.RegimenRimpe)
            : emisor.RegimenRimpe;

        var notaDebito = NotaDebito.Crear(
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
            motivos: motivos,
            impuestos: impuestos,
            pagos: pagos,
            codDocModificado: request.CodDocModificado,
            emisorId: emisor.Id,
            contribuyenteRimpe: regimenRimpe
        );

        // 5. Generar Clave de Acceso de 49 dígitos con Módulo 11 (codDoc = 05)
        string fechaFormato = fechaEmision.ToString("ddMMyyyy");
        string codigoNumerico = Random.Shared.Next(10000000, 99999999).ToString();
        string tipoEmision = "1";
        string cadenaBase = $"{fechaFormato}05{emisor.Ruc}{emisor.Ambiente}{emisor.CodigoEstablecimiento}{emisor.PuntoEmision}{secuencial}{codigoNumerico}{tipoEmision}";
        string claveAcceso = ClaveAccesoService.GenerarDigitoVerificador(cadenaBase);
        notaDebito.AsignarClaveAcceso(claveAcceso);

        // 6. Generar y Firmar XML v1.0.0 con XAdES-BES
        var xmlSinFirma = _xmlGenerator.GenerarXml(notaDebito);

        var aad = emisor.TenantId.ToByteArray();
        var rawCertBytes = _encryptionService.Decrypt(emisor.CertificadoDigital!, aad);
        var rawPassword = _encryptionService.DecryptString(emisor.PasswordCertificado!, aad);

        var xmlFirmadoDoc = _signatureService.FirmarXml(xmlSinFirma, rawCertBytes, rawPassword);
        byte[] xmlFirmadoBytes = Encoding.UTF8.GetBytes(xmlFirmadoDoc.OuterXml);
        notaDebito.AsignarXmlFirmado(xmlFirmadoDoc.OuterXml);

        // 7. Transmisión al Web Service de Recepción del SRI
        string? mensajeDevolucion = null;
        bool esRecibida = false;

        try
        {
            var recepcionResult = await _recepcionService.ValidarComprobanteAsync(xmlFirmadoBytes, emisor.Ambiente, cancellationToken);
            esRecibida = recepcionResult.EsRecibida;

            if (recepcionResult.EsRecibida)
            {
                notaDebito.MarcarComoRecibida();
            }
            else
            {
                var mensajes = recepcionResult.Comprobantes
                    .SelectMany(c => c.Mensajes)
                    .Select(m => $"[{m.Tipo}] ({m.Identificador}): {m.Mensaje} {m.InformacionAdicional}")
                    .ToList();

                mensajeDevolucion = string.Join(" | ", mensajes);
                notaDebito.MarcarComoDevuelta(mensajeDevolucion);
            }
        }
        catch (HttpRequestException ex)
        {
            mensajeDevolucion = $"Servidor SRI no disponible temporalmente: {ex.Message}. El comprobante quedó firmado y listo para reintento.";
            notaDebito.MarcarComoDevuelta(mensajeDevolucion);
        }

        // 8. Persistencia transaccional
        _context.NotasDebito.Add(notaDebito);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmitirNotaDebitoResponseDto
        {
            NotaDebitoId = notaDebito.Id,
            ClaveAcceso = notaDebito.ClaveAcceso,
            Secuencial = $"{emisor.CodigoEstablecimiento}-{emisor.PuntoEmision}-{secuencial}",
            Estado = notaDebito.Estado,
            EsRecibida = esRecibida,
            MensajeDevolucion = mensajeDevolucion
        };
    }
}
