using BillingSaaS.MigrationTool.Models;

namespace BillingSaaS.MigrationTool.Services;

public interface IClienteMigrador
{
    Task<MigrationResult> MigrarClientesAsync(
        IReadOnlyList<ClienteMigracionDto> clientes,
        CancellationToken cancellationToken = default);
}
