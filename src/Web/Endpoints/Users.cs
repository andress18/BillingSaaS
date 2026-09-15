using System.Security.Claims;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();

        groupBuilder.MapGet(GetCurrentUserProfile, "me").RequireAuthorization();
        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
    }

    [EndpointSummary("Obtener perfil y tenant del usuario actual")]
    [EndpointDescription("Devuelve los datos del usuario autenticado incluyendo ID, correo, roles asignados y el TenantId de su organización.")]
    public static async Task<Results<Ok<UserProfileDto>, UnauthorizedHttpResult>> GetCurrentUserProfile(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);

        return TypedResults.Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            TenantId = user.TenantId,
            TenantNombre = tenant?.Nombre,
            PartnerId = tenant?.PartnerId,
            Roles = roles.ToList()
        });
    }

    public record UserProfileDto
    {
        public string Id { get; init; } = null!;
        public string? Email { get; init; }
        public Guid TenantId { get; init; }
        public string? TenantNombre { get; init; }
        public Guid? PartnerId { get; init; }
        public List<string> Roles { get; init; } = [];
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Logs out the current user by clearing the authentication cookie.")]
    public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(SignInManager<ApplicationUser> signInManager, [FromBody] object empty)
    {
        if (empty != null)
        {
            await signInManager.SignOutAsync();
            return TypedResults.Ok();
        }

        return TypedResults.Unauthorized();
    }
}
