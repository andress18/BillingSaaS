using System.Text;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Facturas.Queries.ConsultarAutorizacionFactura;

public record ConsultarAutorizacionFacturaQuery(string ClaveAcceso) : IRequest<SriAutorizacionResponseDto>;

public class ConsultarAutorizacionFacturaQueryHandler : IRequestHandler<ConsultarAutorizacionFacturaQuery, SriAutorizacionResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISriAutorizacionService _autorizacionService;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public ConsultarAutorizacionFacturaQueryHandler(
        IApplicationDbContext context,
        ISriAutorizacionService autorizacionService,
        IEmailService emailService,
        IRidePdfGenerator ridePdfGenerator)
    {
        _context = context;
        _autorizacionService = autorizacionService;
        _emailService = emailService;
        _ridePdfGenerator = ridePdfGenerator;
    }

    public async Task<SriAutorizacionResponseDto> Handle(ConsultarAutorizacionFacturaQuery request, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Detalles)
            .FirstOrDefaultAsync(f => f.ClaveAcceso == request.ClaveAcceso, cancellationToken);

        Guard.Against.NotFound(request.ClaveAcceso, factura);

        bool yaEstabaAutorizada = factura.Estado == "AUTORIZADO";

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

                // Enviar correo transaccional automáticamente al comprador si no había sido enviada previamente
                if (!yaEstabaAutorizada && !string.IsNullOrWhiteSpace(factura.Cliente?.CorreoElectronico))
                {
                    try
                    {
                        var emisor = await _context.Emisores.FirstOrDefaultAsync(e => e.Id == factura.EmisorId, cancellationToken);
                        if (emisor != null)
                        {
                            var pdfBytes = _ridePdfGenerator.GenerarFacturaRide(factura, emisor);
                            var xmlStr = factura.XmlFirmado ?? aut.ComprobanteXml ?? string.Empty;
                            var xmlBytes = Encoding.UTF8.GetBytes(xmlStr);
                            var numeroFactura = $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial:D9}";

                            await _emailService.SendFacturaEmailAsync(
                                factura.Cliente.CorreoElectronico,
                                factura.Cliente.RazonSocial,
                                numeroFactura,
                                factura.RazonSocial,
                                factura.ClaveAcceso,
                                factura.ImporteTotal,
                                pdfBytes,
                                xmlBytes,
                                cancellationToken);
                        }
                    }
                    catch
                    {
                        // Fallo controlado de correo para no interrumpir el flujo del SRI
                    }
                }
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
