using AutoMapper;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.NotasDebito.Queries.GetNotasDebito;

public class NotaDebitoBriefDto
{
    public int Id { get; init; }
    public int EmisorId { get; init; }
    public string ClaveAcceso { get; init; } = null!;
    public string Establecimiento { get; init; } = null!;
    public string PuntoEmision { get; init; } = null!;
    public string Secuencial { get; init; } = null!;
    public string NumeroComprobante => $"{Establecimiento}-{PuntoEmision}-{Secuencial}";
    public string CodDocModificado { get; init; } = null!;
    public string NumDocModificado { get; init; } = null!;
    public DateTime FechaEmisionDocSustento { get; init; }
    public DateTime FechaEmision { get; init; }
    public string TipoIdentificacionComprador { get; init; } = null!;
    public string RazonSocialComprador { get; init; } = null!;
    public string IdentificacionComprador { get; init; } = null!;
    public decimal TotalSinImpuestos { get; init; }
    public decimal ValorTotal { get; init; }
    public string Estado { get; init; } = null!;
    public string? NumeroAutorizacion { get; init; }
    public DateTime? FechaAutorizacion { get; init; }
    public string? MensajeErrorSri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<NotaDebito, NotaDebitoBriefDto>();
        }
    }
}

