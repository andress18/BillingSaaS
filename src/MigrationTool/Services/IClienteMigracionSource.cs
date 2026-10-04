using BillingSaaS.MigrationTool.Models;

namespace BillingSaaS.MigrationTool.Services;

public interface IClienteMigracionSource
{
    Task<IReadOnlyList<ClienteMigracionDto>> ObtenerClientesAsync(CancellationToken cancellationToken = default);
}
