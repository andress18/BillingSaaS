using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Facturas.Commands.EmitirFactura;

public record EmitirFacturaCommand : IRequest<EmitirFacturaResponseDto>
{
    public int EmisorId { get; init; }
    public string? RegimenRimpe { get; init; }
    public bool GuardarEnCatalogo { get; init; } = false;
    public CompradorDto Cliente { get; init; } = null!;
    public List<DetalleDto> Detalles { get; init; } = new();
    public List<CampoAdicionalDto>? CamposAdicionales { get; init; }
    public List<CampoAdicionalDto>? InfoAdicional { get; init; }
    public string? FormaPago { get; init; } = "01";
    public decimal? Plazo { get; init; }
    public string? UnidadTiempo { get; init; }
    public List<PagoDto>? Pagos { get; init; }

    public record PagoDto(string FormaPago, decimal? Total = null, decimal? Plazo = null, string? UnidadTiempo = null);
    public record CampoAdicionalDto(string Nombre, string Valor);

    public record CompradorDto(
        string TipoIdentificacion,
        string Identificacion,
        string RazonSocial,
        string? Direccion,
        string? CorreoElectronico,
        Guid? CatalogoClienteId = null);

    public record DetalleDto(
        string CodigoPrincipal,
        string Descripcion,
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal Descuento,
        List<ImpuestoDto> Impuestos,
        Guid? CatalogoProductoId = null);

    public record ImpuestoDto(string Codigo, string CodigoPorcentaje, decimal Tarifa, decimal BaseImponible);
}

public class EmitirFacturaCommandHandler : IRequestHandler<EmitirFacturaCommand, EmitirFacturaResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturaXmlGenerator _xmlGenerator;
    private readonly ISriSignatureService _signatureService;
    private readonly ISriRecepcionService _recepcionService;
    private readonly ICertificateEncryptionService _encryptionService;
    private readonly ISubscriptionValidationService _subscriptionValidator;
    private readonly IUser _user;

    public EmitirFacturaCommandHandler(
        IApplicationDbContext context,
        IFacturaXmlGenerator xmlGenerator,
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

    public async Task<EmitirFacturaResponseDto> Handle(EmitirFacturaCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener Emisor configurado desde la base de datos
        var emisor = await _context.Emisores.FindAsync([request.EmisorId], cancellationToken);
        Guard.Against.NotFound(request.EmisorId, emisor);

        var isAdmin = _user.Roles?.Contains(BillingSaaS.Domain.Constants.Roles.Administrator) == true;
        if (!isAdmin && _user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && emisor.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para emitir facturas a nombre de este emisor.");
        }

        if (!emisor.Activo)
            throw new InvalidOperationException("El emisor se encuentra inactivo.");

        if (!emisor.TieneCertificadoValido())
            throw new InvalidOperationException("El emisor no tiene un certificado digital válido configurado o ya ha caducado.");

        // 1.1 Validar suscripción activa y límites de emisión del plan SaaS
        await _subscriptionValidator.ValidarEmisionAsync(
            emisor.TenantId,
            codDoc: "01",
            codigoEstablecimiento: emisor.CodigoEstablecimiento,
            cancellationToken);

        // 2. Obtener siguiente número secuencial atómico
        var secuencial = emisor.ObtenerSiguienteSecuencialFactura();

        // 3. Crear objetos de valor de Dominio (Comprador, Detalles e Impuestos)
        Guid? catalogoClienteId = request.Cliente.CatalogoClienteId;

        if (request.GuardarEnCatalogo && request.Cliente.TipoIdentificacion != "07" && request.Cliente.Identificacion != "9999999999999")
        {
            var identificacionLimpia = request.Cliente.Identificacion.Trim();
            var clienteCatalogo = await _context.CatalogoClientes
                .FirstOrDefaultAsync(c => c.TenantId == emisor.TenantId && c.Identificacion == identificacionLimpia, cancellationToken);

            if (clienteCatalogo != null)
            {
                clienteCatalogo.ActualizarContacto(
                    request.Cliente.RazonSocial,
                    request.Cliente.Direccion,
                    request.Cliente.CorreoElectronico,
                    request.Cliente.TipoIdentificacion);
                clienteCatalogo.Activar();
                catalogoClienteId = clienteCatalogo.Id;
            }
            else
            {
                var totalClientes = await _context.CatalogoClientes
                    .CountAsync(c => c.TenantId == emisor.TenantId && c.Activo, cancellationToken);
                if (totalClientes >= 10)
                {
                    throw new InvalidOperationException("Ha alcanzado el límite máximo permitido de 10 compradores registrados en su catálogo.");
                }

                clienteCatalogo = CatalogoCliente.Crear(
                    tenantId: emisor.TenantId,
                    tipoIdentificacion: request.Cliente.TipoIdentificacion,
                    identificacion: identificacionLimpia,
                    razonSocial: request.Cliente.RazonSocial,
                    direccion: request.Cliente.Direccion,
                    correoElectronico: request.Cliente.CorreoElectronico);
                _context.CatalogoClientes.Add(clienteCatalogo);
                catalogoClienteId = clienteCatalogo.Id;
            }
        }

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
            d.Impuestos.Select(i => Impuesto.Crear(i.Codigo, i.CodigoPorcentaje, i.Tarifa, i.BaseImponible)).ToList(),
            catalogoProductoId: d.CatalogoProductoId
        )).ToList();

        // 4. Crear entidad Factura en el huso horario oficial de Ecuador (UTC-5)
        var fechaEmision = DateTime.UtcNow.AddHours(-5).Date;

        var regimenRimpe = !string.IsNullOrWhiteSpace(request.RegimenRimpe)
            ? Domain.Constants.RegimenRimpeTipos.Normalizar(request.RegimenRimpe)
            : emisor.RegimenRimpe;

        var camposSource = request.CamposAdicionales ?? request.InfoAdicional;
        var camposAdicionales = camposSource?
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre) && !string.IsNullOrWhiteSpace(c.Valor))
            .Select(c => CampoAdicional.Crear(c.Nombre, c.Valor))
            .ToList();

        string formaPago = "01";
        decimal? plazo = null;
        string? unidadTiempo = null;

        if (request.Pagos != null && request.Pagos.Count > 0)
        {
            var primerPago = request.Pagos[0];
            formaPago = string.IsNullOrWhiteSpace(primerPago.FormaPago) ? "01" : primerPago.FormaPago;
            plazo = primerPago.Plazo;
            unidadTiempo = primerPago.UnidadTiempo;
        }
        else if (!string.IsNullOrWhiteSpace(request.FormaPago))
        {
            formaPago = request.FormaPago;
            plazo = request.Plazo;
            unidadTiempo = request.UnidadTiempo;
        }

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
            emisorId: emisor.Id,
            contribuyenteRimpe: regimenRimpe,
            catalogoClienteId: catalogoClienteId,
            camposAdicionales: camposAdicionales,
            formaPago: formaPago,
            plazo: plazo,
            unidadTiempo: unidadTiempo
        );

        // 5. Generar Clave de Acceso de 49 dígitos con Módulo 11
        string fechaFormato = fechaEmision.ToString("ddMMyyyy");
        string codigoNumerico = Random.Shared.Next(10000000, 99999999).ToString();
        string tipoEmision = "1";
        string cadenaBase = $"{fechaFormato}01{emisor.Ruc}{emisor.Ambiente}{emisor.CodigoEstablecimiento}{emisor.PuntoEmision}{secuencial}{codigoNumerico}{tipoEmision}";
        string claveAcceso = ClaveAccesoService.GenerarDigitoVerificador(cadenaBase);
        factura.AsignarClaveAcceso(claveAcceso);

        // 6. Generar y Firmar XML v1.1.0 con XAdES-BES (descifrado estrictamente en memoria volátil)
        var xmlSinFirma = _xmlGenerator.GenerarXml(factura);

        var aad = emisor.TenantId.ToByteArray();
        var rawCertBytes = _encryptionService.Decrypt(emisor.CertificadoDigital!, aad);
        var rawPassword = _encryptionService.DecryptString(emisor.PasswordCertificado!, aad);

        var xmlFirmadoDoc = _signatureService.FirmarXml(xmlSinFirma, rawCertBytes, rawPassword);
        byte[] xmlFirmadoBytes = Encoding.UTF8.GetBytes(xmlFirmadoDoc.OuterXml);
        factura.AsignarXmlFirmado(xmlFirmadoDoc.OuterXml);

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
            MensajeDevolucion = mensajeDevolucion,
            ProximoSecuencial = emisor.SecuencialFactura,
            ProximoSecuencialNumero = emisor.SecuencialFactura.ToString("D9")
        };
    }
}
