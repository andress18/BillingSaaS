using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Services;

namespace BillingSaaS.Application.Facturas.Commands.EmitirFactura;

public record EmitirFacturaCommand : IRequest<string>
{
    public Guid TenantId { get; set; }
    public int Ambiente { get; set; }
    public int TipoEmision { get; set; }
    public string Establecimiento { get; set; } = null!;
    public string PuntoEmision { get; set; } = null!;
    public string Secuencial { get; set; } = null!;

    // Datos obligatorios del EMISOR[cite: 1]
    public string RucEmisor { get; set; } = null!;
    public string RazonSocialEmisor { get; set; } = null!;
    public string DireccionMatrizEmisor { get; set; } = null!;

    public DateTime FechaEmision { get; set; }

    public CompradorDto Cliente { get; set; } = null!;
    public List<DetalleDto> Detalles { get; set; } = new();

    // DTOs anidados para mapear el JSON de entrada
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

public class EmitirFacturaCommandHandler : IRequestHandler<EmitirFacturaCommand, string>
{
    private readonly IApplicationDbContext _context;

    public EmitirFacturaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(EmitirFacturaCommand request, CancellationToken cancellationToken)
    {
        // 1. Convertimos los DTOs crudos en objetos de Dominio validados
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

        // 2. Creamos la entidad Raíz inyectando los datos del EMISOR y los objetos compuestos (comprador/detalles)
        var entity = Factura.Crear(
            request.TenantId,
            request.Ambiente,
            request.RazonSocialEmisor,     // Dato del Emisor (Farmacia)
            request.RucEmisor,             // Dato del Emisor (Farmacia)
            request.Establecimiento,
            request.PuntoEmision,
            request.Secuencial,
            request.DireccionMatrizEmisor, // Dato del Emisor (Farmacia)
            request.FechaEmision,
            comprador,                     // Objeto validado del Cliente
            detalles                       // Lista validada de Productos
        );

        // 3. Construcción de la cadena base y cálculo del dígito verificador
        string fechaFormato = request.FechaEmision.ToString("ddMMyyyy");
        string codigoNumerico = "12345678";
        string cadenaBase = $"{fechaFormato}01{request.RucEmisor}{request.Ambiente}{request.Establecimiento}{request.PuntoEmision}{request.Secuencial}{codigoNumerico}{request.TipoEmision}";

        string claveAcceso = ClaveAccesoService.GenerarDigitoVerificador(cadenaBase);
        entity.AsignarClaveAcceso(claveAcceso);

        _context.Facturas.Add(entity);

        // 4. Guardado y disparo de Eventos de Dominio
        await _context.SaveChangesAsync(cancellationToken);

        return entity.ClaveAcceso;
    }
}
