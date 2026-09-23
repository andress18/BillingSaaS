using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasCredito.Queries.GetNotaCreditoPdf;

public record NotaCreditoPdfFileDto(byte[] Content, string ContentType, string FileName);

public record GetNotaCreditoPdfQuery(int Id) : IRequest<NotaCreditoPdfFileDto>;

public class GetNotaCreditoPdfQueryHandler : IRequestHandler<GetNotaCreditoPdfQuery, NotaCreditoPdfFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IRidePdfGenerator _pdfGenerator;
    private readonly IUser _user;

    public GetNotaCreditoPdfQueryHandler(
        IApplicationDbContext context,
        IRidePdfGenerator pdfGenerator,
        IUser user)
    {
        _context = context;
        _pdfGenerator = pdfGenerator;
        _user = user;
    }

    public async Task<NotaCreditoPdfFileDto> Handle(GetNotaCreditoPdfQuery request, CancellationToken cancellationToken)
    {
        var notaCredito = await _context.NotasCredito
            .Include(nc => nc.Cliente)
            .Include(nc => nc.Detalles)
            .FirstOrDefaultAsync(nc => nc.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, notaCredito);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && notaCredito.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para acceder a esta nota de crédito.");
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == notaCredito.EmisorId, cancellationToken);

        var pdfBytes = _pdfGenerator.GenerarNotaCreditoRide(notaCredito, emisor);
        var fileName = $"NOTA_CREDITO_{notaCredito.Establecimiento}_{notaCredito.PuntoEmision}_{notaCredito.Secuencial}.pdf";

        return new NotaCreditoPdfFileDto(pdfBytes, "application/pdf", fileName);
    }
}

