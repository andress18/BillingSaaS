using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillingSaaS.Infrastructure.Identity;

public class ApplicationUserManager : UserManager<ApplicationUser>
{
    public ApplicationUserManager(
        IUserStore<ApplicationUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<ApplicationUser>> logger)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
    }

    public override async Task<ApplicationUser?> FindByNameAsync(string userName)
    {
        var user = await base.FindByNameAsync(userName);
        if (user == null && !string.IsNullOrWhiteSpace(userName))
        {
            user = await base.FindByEmailAsync(userName);
        }
        return user;
    }

    public override async Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        var user = await base.FindByEmailAsync(email);
        if (user == null && !string.IsNullOrWhiteSpace(email))
        {
            user = await base.FindByNameAsync(email);
        }
        return user;
    }
}
