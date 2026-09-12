using BillingSaaS.Application.Common.Models.Sri;

namespace BillingSaaS.Application.Common.Interfaces;

public interface ISriRecepcionService
{
    Task<SriRecepcionResponseDto> ValidarComprobanteAsync(
        byte[] xmlFirmadoBytes,
        int ambiente,
        CancellationToken cancellationToken = default);
}

