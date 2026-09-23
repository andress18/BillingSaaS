using System.Text;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasCredito.Queries.ConsultarAutorizacionNotaCredito;

public record ConsultarAutorizacionNotaCreditoQuery(string ClaveAcceso) : IRequest<SriAutorizacionResponseDto>;

public class ConsultarAutorizacionNotaCreditoQueryHandler : IRequestHandler<ConsultarAutorizacionNotaCreditoQuery, SriAutorizacionResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISriAutorizacionService _autorizacionService;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public ConsultarAutorizacionNotaCreditoQueryHandler(
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

    public async Task<SriAutorizacionResponseDto> Handle(ConsultarAutorizacionNotaCreditoQuery request, CancellationToken cancellationToken)
    {
        var notaCredito = await _context.NotasCredito
            .Include(nc => nc.Cliente)
            .Include(nc => nc.Detalles)
            .FirstOrDefaultAsync(nc => nc.ClaveAcceso == request.ClaveAcceso, cancellationToken);

        Guard.Against.NotFound(request.ClaveAcceso, notaCredito);

        bool yaEstabaAutorizada = notaCredito.Estado == "AUTORIZADO";

        // Consultar al SRI
        var resultadoSri = await _autorizacionService.ConsultarAutorizacionAsync(
            request.ClaveAcceso,
            notaCredito.Ambiente,
            cancellationToken);

        // Si fue autorizado, actualizar la nota de crédito localmente
        if (resultadoSri.EstaAutorizado && resultadoSri.PrimeraAutorizacion is { } aut)
        {
            if (!string.IsNullOrWhiteSpace(aut.NumeroAutorizacion) && aut.FechaAutorizacion.HasValue)
            {
                notaCredito.MarcarComoAutorizada(aut.NumeroAutorizacion, aut.FechaAutorizacion.Value, aut.ComprobanteXml);
                await _context.SaveChangesAsync(cancellationToken);

                // Enviar correo transaccional automáticamente al comprador si no había sido enviada previamente
                if (!yaEstabaAutorizada && !string.IsNullOrWhiteSpace(notaCredito.Cliente?.CorreoElectronico))
                {
                    try
                    {
                        var emisor = await _context.Emisores.FirstOrDefaultAsync(e => e.Id == notaCredito.EmisorId, cancellationToken);
                        if (emisor != null)
                        {
                            var pdfBytes = _ridePdfGenerator.GenerarNotaCreditoRide(notaCredito, emisor);
                            var xmlStr = notaCredito.XmlFirmado ?? aut.ComprobanteXml ?? string.Empty;
                            var xmlBytes = Encoding.UTF8.GetBytes(xmlStr);
                            var numeroNotaCredito = $"{notaCredito.Establecimiento}-{notaCredito.PuntoEmision}-{notaCredito.Secuencial}";

                            await _emailService.SendNotaCreditoEmailAsync(
                                notaCredito.Cliente.CorreoElectronico,
                                notaCredito.Cliente.RazonSocial,
                                numeroNotaCredito,
                                notaCredito.RazonSocial,
                                notaCredito.ClaveAcceso,
                                notaCredito.ValorModificacion,
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
            notaCredito.MarcarComoNoAutorizada(motivo);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return resultadoSri;
    }
}

