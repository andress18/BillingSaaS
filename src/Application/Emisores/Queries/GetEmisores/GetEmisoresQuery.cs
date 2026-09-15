using BillingSaaS.Application.Common.Interfaces;

namespace BillingSaaS.Application.Emisores.Queries.GetEmisores;

public record GetEmisoresQuery : IRequest<List<EmisorBriefDto>>
{
    public Guid? TenantId { get; init; }
}

public class GetEmisoresQueryHandler : IRequestHandler<GetEmisoresQuery, List<EmisorBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetEmisoresQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<List<EmisorBriefDto>> Handle(GetEmisoresQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Emisores.AsNoTracking();

        // Si se especifica TenantId explícito se utiliza, de lo contrario se filtra por el TenantId del usuario autenticado
        var tenantId = request.TenantId ?? _user.TenantId;

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(e => e.TenantId == tenantId.Value);
        }

        return await query
            .OrderBy(e => e.RazonSocial)
            .ProjectTo<EmisorBriefDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}

