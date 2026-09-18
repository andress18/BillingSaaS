using System;
using System.Threading;
using System.Threading.Tasks;

namespace BillingSaaS.Application.Common.Interfaces;

public interface ISubscriptionValidationService
{
    Task ValidarEmisionAsync(Guid tenantId, string codDoc, string codigoEstablecimiento, CancellationToken cancellationToken = default);
}

