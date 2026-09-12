using BillingSaaS.Application.Common.Models.Sri;

namespace BillingSaaS.Application.Common.Interfaces;

public interface ISriAutorizacionService
{
    Task<SriAutorizacionResponseDto> ConsultarAutorizacionAsync(
        string claveAcceso,
        int ambiente,
        CancellationToken cancellationToken = default);
}

