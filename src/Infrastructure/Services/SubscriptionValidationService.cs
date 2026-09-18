using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Infrastructure.Services;

public class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SubscriptionValidationService(
        IApplicationDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task ValidarEmisionAsync(
        Guid tenantId,
        string codDoc,
        string codigoEstablecimiento,
        CancellationToken cancellationToken = default)
    {
        var suscripcion = await _context.Suscripciones
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (suscripcion == null)
        {
            throw new SubscriptionRequiredException(tenantId);
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Validar vigencia (con periodo de gracia)
        if (!suscripcion.EstaVigente(ahoraUtc))
        {
            throw new SubscriptionExpiredException(suscripcion.FechaVencimiento);
        }

        // 2. Validar tipo de comprobante permitido
        if (!suscripcion.Plan.PermiteTipoDocumento(codDoc))
        {
            throw new DocumentTypeNotAllowedException(codDoc, suscripcion.Plan.Nombre);
        }

        // 3. Validar establecimiento permitido
        if (int.TryParse(codigoEstablecimiento, out int numEstablecimiento) && numEstablecimiento > suscripcion.Plan.MaxEstablecimientos)
        {
            throw new EstablishmentLimitExceededException(suscripcion.Plan.MaxEstablecimientos, numEstablecimiento);
        }

        // 4. Validar cuota de documentos en el periodo actual
        if (!suscripcion.Plan.EsIlimitado(suscripcion.Frecuencia))
        {
            var limite = suscripcion.Plan.ObtenerLimiteDocumentos(suscripcion.Frecuencia)!.Value;
            var inicioCiclo = suscripcion.ObtenerInicioCicloActual(ahoraUtc);

            var emitidos = await _context.Facturas
                .CountAsync(f => f.TenantId == tenantId && f.FechaEmision >= inicioCiclo && f.Estado != "DEVUELTA", cancellationToken);

            if (emitidos >= limite)
            {
                throw new SubscriptionLimitExceededException(suscripcion.Plan.Nombre, limite, emitidos);
            }
        }
    }
}

