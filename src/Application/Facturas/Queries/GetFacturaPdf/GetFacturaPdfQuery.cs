using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Facturas.Queries.GetFacturaPdf;

public record FacturaPdfFileDto(byte[] Content, string ContentType, string FileName);

public record GetFacturaPdfQuery(int Id) : IRequest<FacturaPdfFileDto>;

public class GetFacturaPdfQueryHandler : IRequestHandler<GetFacturaPdfQuery, FacturaPdfFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IRidePdfGenerator _pdfGenerator;
    private readonly IUser _user;

    public GetFacturaPdfQueryHandler(
        IApplicationDbContext context,
        IRidePdfGenerator pdfGenerator,
        IUser user)
    {
        _context = context;
        _pdfGenerator = pdfGenerator;
        _user = user;
    }

    public async Task<FacturaPdfFileDto> Handle(GetFacturaPdfQuery request, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Detalles)
                .ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, factura);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && factura.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para acceder a esta factura.");
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == factura.EmisorId, cancellationToken);

        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura, emisor);
        var fileName = $"FACTURA_{factura.Establecimiento}_{factura.PuntoEmision}_{factura.Secuencial}.pdf";

        return new FacturaPdfFileDto(pdfBytes, "application/pdf", fileName);
    }
}

