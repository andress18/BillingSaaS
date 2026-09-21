using System.Text;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models.Sri;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasDebito.Queries.ConsultarAutorizacionNotaDebito;

public record ConsultarAutorizacionNotaDebitoQuery(string ClaveAcceso) : IRequest<SriAutorizacionResponseDto>;

public class ConsultarAutorizacionNotaDebitoQueryHandler : IRequestHandler<ConsultarAutorizacionNotaDebitoQuery, SriAutorizacionResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISriAutorizacionService _autorizacionService;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public ConsultarAutorizacionNotaDebitoQueryHandler(
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

    public async Task<SriAutorizacionResponseDto> Handle(ConsultarAutorizacionNotaDebitoQuery request, CancellationToken cancellationToken)
    {
        var notaDebito = await _context.NotasDebito
            .Include(nd => nd.Cliente)
            .Include(nd => nd.Motivos)
            .Include(nd => nd.Impuestos)
            .Include(nd => nd.Pagos)
            .FirstOrDefaultAsync(nd => nd.ClaveAcceso == request.ClaveAcceso, cancellationToken);

        Guard.Against.NotFound(request.ClaveAcceso, notaDebito);

        bool yaEstabaAutorizada = notaDebito.Estado == "AUTORIZADO";

        // Consultar al SRI
        var resultadoSri = await _autorizacionService.ConsultarAutorizacionAsync(
            request.ClaveAcceso,
            notaDebito.Ambiente,
            cancellationToken);

        // Si fue autorizado, actualizar la nota de débito localmente
        if (resultadoSri.EstaAutorizado && resultadoSri.PrimeraAutorizacion is { } aut)
        {
            if (!string.IsNullOrWhiteSpace(aut.NumeroAutorizacion) && aut.FechaAutorizacion.HasValue)
            {
                notaDebito.MarcarComoAutorizada(aut.NumeroAutorizacion, aut.FechaAutorizacion.Value, aut.ComprobanteXml);
                await _context.SaveChangesAsync(cancellationToken);

                // Enviar correo transaccional automáticamente al comprador si no había sido enviada previamente
                if (!yaEstabaAutorizada && !string.IsNullOrWhiteSpace(notaDebito.Cliente?.CorreoElectronico))
                {
                    try
                    {
                        var emisor = await _context.Emisores.FirstOrDefaultAsync(e => e.Id == notaDebito.EmisorId, cancellationToken);
                        if (emisor != null)
                        {
                            var pdfBytes = _ridePdfGenerator.GenerarNotaDebitoRide(notaDebito, emisor);
                            var xmlStr = notaDebito.XmlFirmado ?? aut.ComprobanteXml ?? string.Empty;
                            var xmlBytes = Encoding.UTF8.GetBytes(xmlStr);
                            var numeroNotaDebito = $"{notaDebito.Establecimiento}-{notaDebito.PuntoEmision}-{notaDebito.Secuencial}";

                            await _emailService.SendNotaDebitoEmailAsync(
                                notaDebito.Cliente.CorreoElectronico,
                                notaDebito.Cliente.RazonSocial,
                                numeroNotaDebito,
                                notaDebito.RazonSocial,
                                notaDebito.ClaveAcceso,
                                notaDebito.ValorTotal,
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
            notaDebito.MarcarComoNoAutorizada(motivo);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return resultadoSri;
    }
}

