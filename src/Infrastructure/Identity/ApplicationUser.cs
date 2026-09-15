using Microsoft.AspNetCore.Identity;

namespace BillingSaaS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid TenantId { get; set; }
}
