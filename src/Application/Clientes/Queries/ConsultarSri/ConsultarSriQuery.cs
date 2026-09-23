using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Queries.ConsultarSri;

public record ConsultarSriQuery(string Identificacion) : IRequest<SriContribuyenteDto?>;

public class ConsultarSriQueryHandler : IRequestHandler<ConsultarSriQuery, SriContribuyenteDto?>
{
    private readonly ISriConsultaRucService _sriService;
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ConsultarSriQueryHandler(
        ISriConsultaRucService sriService,
        IApplicationDbContext context,
        IUser user)
    {
        _sriService = sriService;
        _context = context;
        _user = user;
    }

    public async Task<SriContribuyenteDto?> Handle(ConsultarSriQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identificacion))
            return null;

        var identificacion = request.Identificacion.Trim();

        // 1. Revisar si ya lo tenemos registrado en el catálogo local del tenant para responder inmediatamente
        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            var localCliente = await _context.CatalogoClientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _user.TenantId.Value && c.Identificacion == identificacion, cancellationToken);

            if (localCliente != null)
            {
                return new SriContribuyenteDto(
                    Identificacion: localCliente.Identificacion,
                    RazonSocial: localCliente.RazonSocial,
                    TipoIdentificacion: localCliente.TipoIdentificacion,
                    Estado: localCliente.Activo ? "ACTIVO" : "INACTIVO",
                    RegimenRimpe: null
                );
            }
        }

        // 2. Si no está en el catálogo local, consultar al servicio web público del SRI
        return await _sriService.ConsultarPorIdentificacionAsync(identificacion, cancellationToken);
    }
}

