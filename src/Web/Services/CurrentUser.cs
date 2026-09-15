using System.Security.Claims;

using BillingSaaS.Application.Common.Interfaces;

namespace BillingSaaS.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public List<string>? Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
    public Guid? TenantId => Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id"), out var tid) ? tid : null;

}
