using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using System.Security.Cryptography.X509Certificates;

namespace BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;

public record ConfigurarEmisorCommand : IRequest<int>
{
    public Guid TenantId { get; init; }
    public string Ruc { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? NombreComercial { get; init; }
    public string DireccionMatriz { get; init; } = null!;
    public string? DireccionEstablecimiento { get; init; }
    public string CodigoEstablecimiento { get; init; } = "001";
    public string PuntoEmision { get; init; } = "001";
    public int Ambiente { get; init; } = 1; // 1: Pruebas, 2: Produccion
    public bool ObligadoContabilidad { get; init; }
    public string? RegimenRimpe { get; init; }
    public string? ContribuyenteEspecial { get; init; }
    public int SecuencialInicial { get; init; } = 0;

    // Certificado digital .p12 (base64 o hex) y su contraseña
    public string? CertificadoBase64 { get; init; }
    public string? PasswordCertificado { get; init; }
}

public class ConfigurarEmisorCommandHandler : IRequestHandler<ConfigurarEmisorCommand, int>
{
    private readonly IApplicationDbContext _context;

    public ConfigurarEmisorCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(ConfigurarEmisorCommand request, CancellationToken cancellationToken)
    {
        var emisor = Emisor.Crear(
            tenantId: request.TenantId,
            ruc: request.Ruc,
            razonSocial: request.RazonSocial,
            direccionMatriz: request.DireccionMatriz,
            codigoEstablecimiento: request.CodigoEstablecimiento,
            puntoEmision: request.PuntoEmision,
            ambiente: request.Ambiente,
            obligadoContabilidad: request.ObligadoContabilidad,
            nombreComercial: request.NombreComercial,
            direccionEstablecimiento: request.DireccionEstablecimiento,
            regimenRimpe: request.RegimenRimpe,
            contribuyenteEspecial: request.ContribuyenteEspecial,
            secuencialInicial: request.SecuencialInicial
        );

        if (!string.IsNullOrWhiteSpace(request.CertificadoBase64) && !string.IsNullOrWhiteSpace(request.PasswordCertificado))
        {
            var raw = request.CertificadoBase64.Trim();
            byte[] p12Bytes = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromHexString(raw[2..])
                : Convert.FromBase64String(raw);

            using var cert = X509CertificateLoader.LoadPkcs12(p12Bytes, request.PasswordCertificado);
            emisor.ConfigurarCertificado(p12Bytes, request.PasswordCertificado, cert.NotAfter, cert.Subject);
        }

        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync(cancellationToken);

        return emisor.Id;
    }
}

