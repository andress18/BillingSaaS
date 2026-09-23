using AutoMapper;
using AutoMapper.QueryableExtensions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models;
using BillingSaaS.Domain.Constants;

namespace BillingSaaS.Application.NotasCredito.Queries.GetNotasCredito;

public record GetNotasCreditoQuery : IRequest<PaginatedList<NotaCreditoBriefDto>>
{
    public Guid? TenantId { get; init; }
    public int? EmisorId { get; init; }
    public string? Estado { get; init; }
    public string? IdentificacionComprador { get; init; }
    public string? TerminoBusqueda { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
    public int? PageNumber { get; init; } = 1;
    public int? PageSize { get; init; } = 10;
}

public class GetNotasCreditoQueryHandler : IRequestHandler<GetNotasCreditoQuery, PaginatedList<NotaCreditoBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetNotasCreditoQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<PaginatedList<NotaCreditoBriefDto>> Handle(GetNotasCreditoQuery request, CancellationToken cancellationToken)
    {
        var query = _context.NotasCredito.AsNoTracking();

        var isAdmin = _user.Roles?.Contains(Roles.Administrator) == true;
        if (!isAdmin)
        {
            if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
            {
                query = query.Where(nc => nc.TenantId == _user.TenantId.Value);
            }
        }
        else if (request.TenantId.HasValue)
        {
            query = query.Where(nc => nc.TenantId == request.TenantId.Value);
        }

        if (request.EmisorId.HasValue)
        {
            query = query.Where(nc => nc.EmisorId == request.EmisorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            query = query.Where(nc => nc.Estado == request.Estado.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.IdentificacionComprador))
        {
            query = query.Where(nc => nc.IdentificacionComprador == request.IdentificacionComprador.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.TerminoBusqueda))
        {
            var search = request.TerminoBusqueda.Trim();
            query = query.Where(nc =>
                nc.RazonSocialComprador.Contains(search) ||
                nc.IdentificacionComprador.Contains(search) ||
                nc.Secuencial.Contains(search) ||
                nc.NumDocModificado.Contains(search) ||
                nc.ClaveAcceso.Contains(search));
        }

        if (request.FechaInicio.HasValue)
        {
            var inicio = request.FechaInicio.Value.Date;
            query = query.Where(nc => nc.FechaEmision >= inicio);
        }

        if (request.FechaFin.HasValue)
        {
            var fin = request.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(nc => nc.FechaEmision <= fin);
        }

        var pageNumber = request.PageNumber is > 0 ? request.PageNumber.Value : 1;
        var pageSize = request.PageSize is > 0 ? request.PageSize.Value : 10;

        var projected = query
            .OrderByDescending(nc => nc.FechaEmision)
            .ThenByDescending(nc => nc.Id)
            .ProjectTo<NotaCreditoBriefDto>(_mapper.ConfigurationProvider);

        return await PaginatedList<NotaCreditoBriefDto>.CreateAsync(projected, pageNumber, pageSize, cancellationToken);
    }
}

