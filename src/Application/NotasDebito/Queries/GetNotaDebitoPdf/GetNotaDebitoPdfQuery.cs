using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasDebito.Queries.GetNotaDebitoPdf;

public record NotaDebitoPdfFileDto(byte[] Content, string ContentType, string FileName);

public record GetNotaDebitoPdfQuery(int Id) : IRequest<NotaDebitoPdfFileDto>;

public class GetNotaDebitoPdfQueryHandler : IRequestHandler<GetNotaDebitoPdfQuery, NotaDebitoPdfFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IRidePdfGenerator _pdfGenerator;
    private readonly IUser _user;

    public GetNotaDebitoPdfQueryHandler(
        IApplicationDbContext context,
        IRidePdfGenerator pdfGenerator,
        IUser user)
    {
        _context = context;
        _pdfGenerator = pdfGenerator;
        _user = user;
    }

    public async Task<NotaDebitoPdfFileDto> Handle(GetNotaDebitoPdfQuery request, CancellationToken cancellationToken)
    {
        var notaDebito = await _context.NotasDebito
            .Include(nd => nd.Cliente)
            .Include(nd => nd.Motivos)
            .Include(nd => nd.Impuestos)
            .Include(nd => nd.Pagos)
            .FirstOrDefaultAsync(nd => nd.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, notaDebito);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && notaDebito.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para acceder a esta nota de débito.");
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == notaDebito.EmisorId, cancellationToken);

        var pdfBytes = _pdfGenerator.GenerarNotaDebitoRide(notaDebito, emisor);
        var fileName = $"NOTA_DEBITO_{notaDebito.Establecimiento}_{notaDebito.PuntoEmision}_{notaDebito.Secuencial}.pdf";

        return new NotaDebitoPdfFileDto(pdfBytes, "application/pdf", fileName);
    }
}

