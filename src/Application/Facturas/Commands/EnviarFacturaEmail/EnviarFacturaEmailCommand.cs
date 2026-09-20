using System.Text;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Facturas.Commands.EnviarFacturaEmail;

public record EnviarFacturaEmailCommand : IRequest<bool>
{
    public int FacturaId { get; init; }
    public string? EmailDestino { get; init; } // Si es null, se usa el correo registrado del cliente
}

public class EnviarFacturaEmailCommandHandler : IRequestHandler<EnviarFacturaEmailCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IEmailService _emailService;
    private readonly IRidePdfGenerator _ridePdfGenerator;

    public EnviarFacturaEmailCommandHandler(
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

    public async Task<bool> Handle(EnviarFacturaEmailCommand request, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .Include(f => f.Cliente)
            .Include(f => f.Detalles)
            .FirstOrDefaultAsync(f => f.Id == request.FacturaId, cancellationToken);

        Guard.Against.NotFound(request.FacturaId, factura);

        // Aislamiento Multi-tenant
        if (_user.TenantId.HasValue && factura.TenantId != _user.TenantId.Value)
        {
            throw new ForbiddenAccessException();
        }

        var emisor = await _context.Emisores
            .FirstOrDefaultAsync(e => e.Id == factura.EmisorId, cancellationToken);

        Guard.Against.NotFound(factura.EmisorId, emisor);

        var emailDestino = !string.IsNullOrWhiteSpace(request.EmailDestino)
            ? request.EmailDestino.Trim()
            : factura.Cliente?.CorreoElectronico?.Trim();

        if (string.IsNullOrWhiteSpace(emailDestino))
        {
            throw new InvalidOperationException("No se especificó un correo electrónico de destino para la factura.");
        }

        // Generar RIDE PDF
        var pdfBytes = _ridePdfGenerator.GenerarFacturaRide(factura, emisor);

        // Obtener XML firmado
        var xmlContent = factura.XmlFirmado;
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new InvalidOperationException("La factura no dispone de archivo XML firmado para adjuntar.");
        }
        var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);

        var numeroFactura = $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial:D9}";
        var razonSocialComprador = factura.Cliente?.RazonSocial ?? "Consumidor Final";

        await _emailService.SendFacturaEmailAsync(
            emailDestino,
            razonSocialComprador,
            numeroFactura,
            factura.RazonSocial,
            factura.ClaveAcceso,
            factura.ImporteTotal,
            pdfBytes,
            xmlBytes,
            cancellationToken);

        return true;
    }
}
