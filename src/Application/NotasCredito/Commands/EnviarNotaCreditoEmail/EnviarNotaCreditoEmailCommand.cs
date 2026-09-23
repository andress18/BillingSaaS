using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.NotasCredito.Commands.EnviarNotaCreditoEmail;

public record EnviarNotaCreditoEmailCommand : IRequest<bool>
{
    public int NotaCreditoId { get; init; }
    public string? EmailDestino { get; init; }
}

public class EnviarNotaCreditoEmailCommandHandler : IRequestHandler<EnviarNotaCreditoEmailCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public EnviarNotaCreditoEmailCommandHandler(
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

    public async Task<bool> Handle(EnviarNotaCreditoEmailCommand request, CancellationToken cancellationToken)
    {
        var notaCredito = await _context.NotasCredito
            .Include(nc => nc.Cliente)
            .Include(nc => nc.Detalles)
            .FirstOrDefaultAsync(nc => nc.Id == request.NotaCreditoId, cancellationToken);

        Guard.Against.NotFound(request.NotaCreditoId, notaCredito);

        // Aislamiento Multi-tenant
        if (_user.TenantId.HasValue && notaCredito.TenantId != _user.TenantId.Value)
        {
            throw new ForbiddenAccessException();
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == notaCredito.EmisorId, cancellationToken);

        Guard.Against.NotFound(notaCredito.EmisorId, emisor);

        var emailDestino = !string.IsNullOrWhiteSpace(request.EmailDestino)
            ? request.EmailDestino.Trim()
            : notaCredito.Cliente?.CorreoElectronico?.Trim();

        if (string.IsNullOrWhiteSpace(emailDestino))
        {
            throw new InvalidOperationException("No se especificó un correo electrónico de destino para la nota de crédito.");
        }

        // Generar RIDE PDF
        var pdfBytes = _ridePdfGenerator.GenerarNotaCreditoRide(notaCredito, emisor);

        // Obtener XML firmado
        var xmlContent = notaCredito.XmlFirmado;
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new InvalidOperationException("La nota de crédito no dispone de archivo XML firmado para adjuntar.");
        }
        var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);

        var numeroNotaCredito = $"{notaCredito.Establecimiento}-{notaCredito.PuntoEmision}-{notaCredito.Secuencial}";
        var razonSocialComprador = notaCredito.Cliente?.RazonSocial ?? notaCredito.RazonSocialComprador;

        await _emailService.SendNotaCreditoEmailAsync(
            emailDestino,
            razonSocialComprador,
            numeroNotaCredito,
            notaCredito.RazonSocial,
            notaCredito.ClaveAcceso,
            notaCredito.ValorModificacion,
            pdfBytes,
            xmlBytes,
            cancellationToken);

        return true;
    }
}

