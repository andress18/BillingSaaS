using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Emisores.Commands.ActualizarLogoEmisor;

public record ActualizarLogoEmisorCommand : IRequest
{
    public int Id { get; init; }
    public string? Logo { get; init; }
}

public class ActualizarLogoEmisorCommandHandler : IRequestHandler<ActualizarLogoEmisorCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ActualizarLogoEmisorCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ActualizarLogoEmisorCommand request, CancellationToken cancellationToken)
    {
        var emisor = await _context.Emisores.FindAsync([request.Id], cancellationToken);
        Guard.Against.NotFound(request.Id, emisor);

        var isAdmin = _user.Roles?.Contains(BillingSaaS.Domain.Constants.Roles.Administrator) == true;
        if (!isAdmin && _user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && emisor.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para modificar este emisor.");
        }

        emisor.ActualizarLogo(request.Logo);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
