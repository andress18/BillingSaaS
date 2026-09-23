using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasCredito.Queries.GetNotaCreditoXml;

public record NotaCreditoXmlFileDto(byte[] Content, string ContentType, string FileName);

public record GetNotaCreditoXmlQuery(int Id, bool Raw = false) : IRequest<NotaCreditoXmlFileDto>;

public class GetNotaCreditoXmlQueryHandler : IRequestHandler<GetNotaCreditoXmlQuery, NotaCreditoXmlFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly INotaCreditoXmlGenerator _xmlGenerator;
    private readonly IUser _user;

    public GetNotaCreditoXmlQueryHandler(
        IApplicationDbContext context,
        INotaCreditoXmlGenerator xmlGenerator,
        IUser user)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _user = user;
    }

    public async Task<NotaCreditoXmlFileDto> Handle(GetNotaCreditoXmlQuery request, CancellationToken cancellationToken)
    {
        var notaCredito = await _context.NotasCredito
            .AsNoTracking()
            .Include(nc => nc.Cliente)
            .Include(nc => nc.Detalles)
            .FirstOrDefaultAsync(nc => nc.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, notaCredito);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && notaCredito.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para acceder a esta nota de crédito.");
        }

        byte[] xmlBytes;
        if (request.Raw)
        {
            if (!string.IsNullOrWhiteSpace(notaCredito.XmlFirmado))
            {
                xmlBytes = Encoding.UTF8.GetBytes(notaCredito.XmlFirmado);
            }
            else
            {
                xmlBytes = _xmlGenerator.GenerarXmlBytes(notaCredito);
            }
        }
        else
        {
            xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(notaCredito, notaCredito.XmlFirmado);
        }

        var fileName = $"NOTA_CREDITO_{notaCredito.Establecimiento}_{notaCredito.PuntoEmision}_{notaCredito.Secuencial}.xml";

        return new NotaCreditoXmlFileDto(xmlBytes, "application/xml; charset=utf-8", fileName);
    }
}

