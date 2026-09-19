using BillingSaaS.Application.Common.Models;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<(Result Result, string UserId)> CreateUserWithTenantAsync(string userName, string password, Guid tenantId, string? role = null);

    Task<Result> DeleteUserAsync(string userId);
}
