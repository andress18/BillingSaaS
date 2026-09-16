using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Emisores.Queries.GetEmisores;

public class EmisorBriefDto
{
    public int Id { get; init; }
    public Guid TenantId { get; init; }
    public string Ruc { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? NombreComercial { get; init; }
    public string DireccionMatriz { get; init; } = null!;
    public string? DireccionEstablecimiento { get; init; }
    public string CodigoEstablecimiento { get; init; } = null!;
    public string PuntoEmision { get; init; } = null!;
    public int Ambiente { get; init; }
    public bool ObligadoContabilidad { get; init; }
    public string? RegimenRimpe { get; init; }
    public string? ContribuyenteEspecial { get; init; }
    public bool TieneCertificadoDigital { get; init; }
    public DateTime? FechaCaducidadCertificado { get; init; }
    public string? SubjectCertificado { get; init; }
    public bool Activo { get; init; }
    public int SecuencialFactura { get; init; }


    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Emisor, EmisorBriefDto>()
                .ForMember(d => d.TieneCertificadoDigital, opt => opt.MapFrom(s => s.CertificadoDigital != null && s.CertificadoDigital.Length > 0));
        }
    }
}

