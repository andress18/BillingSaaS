using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Facturas.Queries.GetFacturaXml;

public record FacturaXmlFileDto(byte[] Content, string ContentType, string FileName);

public record GetFacturaXmlQuery(int Id, bool Raw = false) : IRequest<FacturaXmlFileDto>;

public class GetFacturaXmlQueryHandler : IRequestHandler<GetFacturaXmlQuery, FacturaXmlFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturaXmlGenerator _xmlGenerator;
    private readonly IUser _user;

    public GetFacturaXmlQueryHandler(
        IApplicationDbContext context,
        IFacturaXmlGenerator xmlGenerator,
        IUser user)
    {
        _context = context;
        _xmlGenerator = xmlGenerator;
        _user = user;
    }

    public async Task<FacturaXmlFileDto> Handle(GetFacturaXmlQuery request, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(f => f.Cliente)
            .Include(f => f.Detalles)
                .ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, factura);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty && factura.TenantId != _user.TenantId.Value)
        {
            throw new UnauthorizedAccessException("No tiene autorización para acceder a esta factura.");
        }

        byte[] xmlBytes;
        if (request.Raw)
        {
            if (!string.IsNullOrWhiteSpace(factura.XmlFirmado))
            {
                xmlBytes = Encoding.UTF8.GetBytes(factura.XmlFirmado);
            }
            else
            {
                xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
            }
        }
        else
        {
            xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(factura, factura.XmlFirmado);
        }

        var fileName = $"FACTURA_{factura.Establecimiento}_{factura.PuntoEmision}_{factura.Secuencial}.xml";

        return new FacturaXmlFileDto(xmlBytes, "application/xml; charset=utf-8", fileName);
    }
}

