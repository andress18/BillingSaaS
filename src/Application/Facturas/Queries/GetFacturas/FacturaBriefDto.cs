using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Facturas.Queries.GetFacturas;

public class FacturaBriefDto
{
    public int Id { get; init; }
    public int EmisorId { get; init; }
    public string ClaveAcceso { get; init; } = null!;
    public string Establecimiento { get; init; } = null!;
    public string PuntoEmision { get; init; } = null!;
    public string Secuencial { get; init; } = null!;
    public string NumeroComprobante => $"{Establecimiento}-{PuntoEmision}-{Secuencial}";
    public DateTime FechaEmision { get; init; }
    public string TipoIdentificacionComprador { get; init; } = null!;
    public string RazonSocialComprador { get; init; } = null!;
    public string IdentificacionComprador { get; init; } = null!;
    public string? DireccionComprador { get; init; }
    public string? CorreoElectronicoComprador { get; init; }
    public Guid? CatalogoClienteId { get; init; }
    public decimal TotalSinImpuestos { get; init; }
    public decimal TotalDescuento { get; init; }
    public decimal ImporteTotal { get; init; }
    public string FormaPago { get; init; } = "01";
    public decimal? Plazo { get; init; }
    public string? UnidadTiempo { get; init; }
    public string Estado { get; init; } = null!;
    public string? NumeroAutorizacion { get; init; }
    public DateTime? FechaAutorizacion { get; init; }
    public string? MensajeErrorSri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Factura, FacturaBriefDto>()
                .ForMember(d => d.DireccionComprador, opt => opt.MapFrom(s => s.Cliente.Direccion))
                .ForMember(d => d.CorreoElectronicoComprador, opt => opt.MapFrom(s => s.Cliente.CorreoElectronico));
        }
    }
}

