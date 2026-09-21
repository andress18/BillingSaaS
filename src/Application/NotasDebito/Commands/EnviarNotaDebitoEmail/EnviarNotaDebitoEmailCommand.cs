using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasDebito.Commands.EnviarNotaDebitoEmail;

public record EnviarNotaDebitoEmailCommand : IRequest<bool>
{
    public int NotaDebitoId { get; init; }
    public string? EmailDestino { get; init; }
}

public class EnviarNotaDebitoEmailCommandHandler : IRequestHandler<EnviarNotaDebitoEmailCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public EnviarNotaDebitoEmailCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IEmailService emailService,
        IRidePdfGenerator ridePdfGenerator)
    {
        _context = context;
        _user = user;
        _emailService = emailService;
        _ridePdfGenerator = ridePdfGenerator;
    }

    public async Task<bool> Handle(EnviarNotaDebitoEmailCommand request, CancellationToken cancellationToken)
    {
        var notaDebito = await _context.NotasDebito
            .Include(nd => nd.Cliente)
            .Include(nd => nd.Motivos)
            .Include(nd => nd.Impuestos)
            .Include(nd => nd.Pagos)
            .FirstOrDefaultAsync(nd => nd.Id == request.NotaDebitoId, cancellationToken);

        Guard.Against.NotFound(request.NotaDebitoId, notaDebito);

        // Aislamiento Multi-tenant
        if (_user.TenantId.HasValue && notaDebito.TenantId != _user.TenantId.Value)
        {
            throw new ForbiddenAccessException();
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == notaDebito.EmisorId, cancellationToken);

        Guard.Against.NotFound(notaDebito.EmisorId, emisor);

        var emailDestino = !string.IsNullOrWhiteSpace(request.EmailDestino)
            ? request.EmailDestino.Trim()
            : notaDebito.Cliente?.CorreoElectronico?.Trim();

        if (string.IsNullOrWhiteSpace(emailDestino))
        {
            throw new InvalidOperationException("No se especificó un correo electrónico de destino para la nota de débito.");
        }

        // Generar RIDE PDF
        var pdfBytes = _ridePdfGenerator.GenerarNotaDebitoRide(notaDebito, emisor);

        // Obtener XML firmado
        var xmlContent = notaDebito.XmlFirmado;
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new InvalidOperationException("La nota de débito no dispone de archivo XML firmado para adjuntar.");
        }
        var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);

        var numeroNotaDebito = $"{notaDebito.Establecimiento}-{notaDebito.PuntoEmision}-{notaDebito.Secuencial}";
        var razonSocialComprador = notaDebito.Cliente?.RazonSocial ?? notaDebito.RazonSocialComprador;

        await _emailService.SendNotaDebitoEmailAsync(
            emailDestino,
            razonSocialComprador,
            numeroNotaDebito,
            notaDebito.RazonSocial,
            notaDebito.ClaveAcceso,
            notaDebito.ValorTotal,
            pdfBytes,
            xmlBytes,
            cancellationToken);

        return true;
    }
}

