using AutoMapper;
using AutoMapper.QueryableExtensions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models;
using BillingSaaS.Domain.Constants;

namespace BillingSaaS.Application.NotasDebito.Queries.GetNotasDebito;

public record GetNotasDebitoQuery : IRequest<PaginatedList<NotaDebitoBriefDto>>
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

public class GetNotasDebitoQueryHandler : IRequestHandler<GetNotasDebitoQuery, PaginatedList<NotaDebitoBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetNotasDebitoQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<PaginatedList<NotaDebitoBriefDto>> Handle(GetNotasDebitoQuery request, CancellationToken cancellationToken)
    {
        var query = _context.NotasDebito.AsNoTracking();

        var isAdmin = _user.Roles?.Contains(Roles.Administrator) == true;
        if (!isAdmin)
        {
            if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
            {
                query = query.Where(nd => nd.TenantId == _user.TenantId.Value);
            }
        }
        else if (request.TenantId.HasValue)
        {
            query = query.Where(nd => nd.TenantId == request.TenantId.Value);
        }

        if (request.EmisorId.HasValue)
        {
            query = query.Where(nd => nd.EmisorId == request.EmisorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            query = query.Where(nd => nd.Estado == request.Estado.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.IdentificacionComprador))
        {
            query = query.Where(nd => nd.IdentificacionComprador == request.IdentificacionComprador.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.TerminoBusqueda))
        {
            var search = request.TerminoBusqueda.Trim();
            query = query.Where(nd =>
                nd.RazonSocialComprador.Contains(search) ||
                nd.IdentificacionComprador.Contains(search) ||
                nd.Secuencial.Contains(search) ||
                nd.NumDocModificado.Contains(search) ||
                nd.ClaveAcceso.Contains(search));
        }

        if (request.FechaInicio.HasValue)
        {
            var inicio = request.FechaInicio.Value.Date;
            query = query.Where(nd => nd.FechaEmision >= inicio);
        }

        if (request.FechaFin.HasValue)
        {
            var fin = request.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(nd => nd.FechaEmision <= fin);
        }

        var pageNumber = request.PageNumber is > 0 ? request.PageNumber.Value : 1;
        var pageSize = request.PageSize is > 0 ? request.PageSize.Value : 10;

        var projected = query
            .OrderByDescending(nd => nd.FechaEmision)
            .ThenByDescending(nd => nd.Id)
            .ProjectTo<NotaDebitoBriefDto>(_mapper.ConfigurationProvider);

        return await PaginatedList<NotaDebitoBriefDto>.CreateAsync(projected, pageNumber, pageSize, cancellationToken);
    }
}
