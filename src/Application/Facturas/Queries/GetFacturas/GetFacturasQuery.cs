using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models;

namespace BillingSaaS.Application.Facturas.Queries.GetFacturas;

public record GetFacturasQuery : IRequest<PaginatedList<FacturaBriefDto>>
{
    public int? EmisorId { get; init; }
    public string? Estado { get; init; }
    public string? IdentificacionComprador { get; init; }
    public string? TerminoBusqueda { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
    public int? PageNumber { get; init; } = 1;
    public int? PageSize { get; init; } = 10;
}

public class GetFacturasQueryHandler : IRequestHandler<GetFacturasQuery, PaginatedList<FacturaBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetFacturasQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<FacturaBriefDto>> Handle(GetFacturasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Facturas.AsNoTracking();

        if (request.EmisorId.HasValue)
        {
            query = query.Where(f => f.EmisorId == request.EmisorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            query = query.Where(f => f.Estado == request.Estado.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.IdentificacionComprador))
        {
            query = query.Where(f => f.IdentificacionComprador == request.IdentificacionComprador.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.TerminoBusqueda))
        {
            var term = request.TerminoBusqueda.Trim();
            query = query.Where(f => f.RazonSocialComprador.Contains(term)
                                  || f.Secuencial.Contains(term)
                                  || f.ClaveAcceso.Contains(term));
        }

        if (request.FechaInicio.HasValue)
        {
            query = query.Where(f => f.FechaEmision >= request.FechaInicio.Value);
        }

        if (request.FechaFin.HasValue)
        {
            query = query.Where(f => f.FechaEmision <= request.FechaFin.Value);
        }

        var pageNumber = request.PageNumber.GetValueOrDefault(1);
        var pageSize = request.PageSize.GetValueOrDefault(10);

        return await query
            .OrderByDescending(f => f.FechaEmision)
            .ThenByDescending(f => f.Id)
            .ProjectTo<FacturaBriefDto>(_mapper.ConfigurationProvider)
            .CreatePaginatedListAsync(pageNumber, pageSize, cancellationToken);
    }
}

public static class QueryableExtensions
{
    public static Task<PaginatedList<TDestination>> CreatePaginatedListAsync<TDestination>(
        this IQueryable<TDestination> queryable, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => PaginatedList<TDestination>.CreateAsync(queryable, pageNumber, pageSize, cancellationToken);
}
