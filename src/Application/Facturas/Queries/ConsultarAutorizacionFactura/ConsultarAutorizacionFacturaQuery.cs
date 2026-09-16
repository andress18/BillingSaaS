using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Facturas.Queries.ConsultarAutorizacionFactura;

public record ConsultarAutorizacionFacturaQuery(string ClaveAcceso) : IRequest<SriAutorizacionResponseDto>;

public class ConsultarAutorizacionFacturaQueryHandler : IRequestHandler<ConsultarAutorizacionFacturaQuery, SriAutorizacionResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISriAutorizacionService _autorizacionService;

    public ConsultarAutorizacionFacturaQueryHandler(
        IApplicationDbContext context,
        ISriAutorizacionService autorizacionService)
    {
        _context = context;
        _autorizacionService = autorizacionService;
    }

    public async Task<SriAutorizacionResponseDto> Handle(ConsultarAutorizacionFacturaQuery request, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .FirstOrDefaultAsync(f => f.ClaveAcceso == request.ClaveAcceso, cancellationToken);

        Guard.Against.NotFound(request.ClaveAcceso, factura);

        // Consultar al SRI
        var resultadoSri = await _autorizacionService.ConsultarAutorizacionAsync(
            request.ClaveAcceso,
            factura.Ambiente,
            cancellationToken);

        // Si fue autorizado, actualizar la factura localmente
        if (resultadoSri.EstaAutorizado && resultadoSri.PrimeraAutorizacion is { } aut)
        {
            if (!string.IsNullOrWhiteSpace(aut.NumeroAutorizacion) && aut.FechaAutorizacion.HasValue)
            {
                factura.MarcarComoAutorizada(aut.NumeroAutorizacion, aut.FechaAutorizacion.Value, aut.ComprobanteXml);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        else if (resultadoSri.PrimeraAutorizacion is { Estado: "NO AUTORIZADO" } noAut)
        {
            var motivo = string.Join(" | ", noAut.Mensajes.Select(m => $"[{m.Tipo}] ({m.Identificador}): {m.Mensaje}"));
            factura.MarcarComoNoAutorizada(motivo);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return resultadoSri;
    }
}

