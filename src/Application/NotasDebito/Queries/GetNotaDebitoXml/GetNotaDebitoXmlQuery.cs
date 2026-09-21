using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasDebito.Queries.GetNotaDebitoXml;

public record NotaDebitoXmlFileDto(byte[] Content, string ContentType, string FileName);

public record GetNotaDebitoXmlQuery(int Id, bool Raw = false) : IRequest<NotaDebitoXmlFileDto>;

public class GetNotaDebitoXmlQueryHandler : IRequestHandler<GetNotaDebitoXmlQuery, NotaDebitoXmlFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly INotaDebitoXmlGenerator _xmlGenerator;
    private readonly IUser _user;

    public GetNotaDebitoXmlQueryHandler(
        IApplicationDbContext context,
        INotaDebitoXmlGenerator xmlGenerator,
        IUser user)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _user = user;
    }

    public async Task<NotaDebitoXmlFileDto> Handle(GetNotaDebitoXmlQuery request, CancellationToken cancellationToken)
    {
        var notaDebito = await _context.NotasDebito
            .AsNoTracking()
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

        byte[] xmlBytes;
        if (request.Raw)
        {
            if (!string.IsNullOrWhiteSpace(notaDebito.XmlFirmado))
            {
                xmlBytes = Encoding.UTF8.GetBytes(notaDebito.XmlFirmado);
            }
            else
            {
                xmlBytes = _xmlGenerator.GenerarXmlBytes(notaDebito);
            }
        }
        else
        {
            xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(notaDebito, notaDebito.XmlFirmado);
        }

        var fileName = $"NOTA_DEBITO_{notaDebito.Establecimiento}_{notaDebito.PuntoEmision}_{notaDebito.Secuencial}.xml";

        return new NotaDebitoXmlFileDto(xmlBytes, "application/xml; charset=utf-8", fileName);
    }
}

