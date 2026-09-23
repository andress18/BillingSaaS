namespace BillingSaaS.Application.Common.Interfaces;

public interface ISriConsultaRucService
{
    Task<SriContribuyenteDto?> ConsultarPorIdentificacionAsync(string identificacion, CancellationToken cancellationToken = default);
}

public record SriContribuyenteDto(
    string Identificacion,
    string RazonSocial,
    string TipoIdentificacion,
    string? Estado = null,
    string? RegimenRimpe = null
);

