using System.Security.Claims;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
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
        groupBuilder.MapGet(GetUsers, "").RequireAuthorization();
        groupBuilder.MapPost(CreateUser, "").RequireAuthorization();
        groupBuilder.MapPost(RegistrarUsuario, "registro").AllowAnonymous();
        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
    }

    [EndpointSummary("Obtener perfil y tenant del usuario actual")]
    [EndpointDescription("Devuelve los datos del usuario autenticado incluyendo ID, username, correo, roles asignados y el TenantId de su organización.")]
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
            UserName = user.UserName,
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
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public Guid TenantId { get; init; }
        public string? TenantNombre { get; init; }
        public Guid? PartnerId { get; init; }
        public List<string> Roles { get; init; } = [];
    }

    public record UserSummaryDto
    {
        public string Id { get; init; } = null!;
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public Guid TenantId { get; init; }
        public List<string> Roles { get; init; } = [];
    }

    public record CreateUserRequest
    {
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public string Password { get; init; } = string.Empty;
        public string? Role { get; init; }
        public Guid? TenantId { get; init; }
    }

    [EndpointSummary("Listar usuarios del tenant actual")]
    [EndpointDescription("Obtiene la lista de usuarios asociados al tenant del usuario autenticado (o filtrado por tenantId si es Administrador).")]
    public static async Task<Results<Ok<List<UserSummaryDto>>, UnauthorizedHttpResult>> GetUsers(
        UserManager<ApplicationUser> userManager,
        IUser currentUser,
        IIdentityService identityService,
        [FromQuery] Guid? tenantId)
    {
        var currentUserId = currentUser.Id;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return TypedResults.Unauthorized();
        }

        bool isAdmin = await identityService.IsInRoleAsync(currentUserId, Roles.Administrator);
        Guid effectiveTenantId = isAdmin && tenantId.HasValue
            ? tenantId.Value
            : (currentUser.TenantId ?? Guid.Empty);

        var query = userManager.Users.AsQueryable();
        if (!isAdmin || tenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == effectiveTenantId);
        }

        var users = await query.ToListAsync();
        var result = new List<UserSummaryDto>();

        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            result.Add(new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                TenantId = u.TenantId,
                Roles = roles.ToList()
            });
        }

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Crear un nuevo usuario por email o username")]
    [EndpointDescription("Permite agregar un usuario especificando email, username o ambos, asignándolo al tenant del usuario autenticado (o tenantId indicado si es Administrador).")]
    public static async Task<Results<Created<UserSummaryDto>, BadRequest<string>, UnauthorizedHttpResult>> CreateUser(
        IIdentityService identityService,
        IUser currentUser,
        [FromBody] CreateUserRequest request)
    {
        var currentUserId = currentUser.Id;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.UserName))
        {
            return TypedResults.BadRequest("Debe proporcionar al menos un correo electrónico o un nombre de usuario.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest("La contraseña es requerida.");
        }

        bool isAdmin = await identityService.IsInRoleAsync(currentUserId, Roles.Administrator);
        Guid effectiveTenantId = isAdmin && request.TenantId.HasValue
            ? request.TenantId.Value
            : (currentUser.TenantId ?? Guid.Empty);

        if (effectiveTenantId == Guid.Empty && !isAdmin)
        {
            return TypedResults.BadRequest("El usuario no tiene una organización (Tenant) asignada.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var userName = string.IsNullOrWhiteSpace(request.UserName)
            ? (email ?? throw new InvalidOperationException())
            : request.UserName.Trim();

        var (result, userId) = await identityService.CreateUserWithTenantAsync(
            userName,
            email,
            request.Password.Trim(),
            effectiveTenantId,
            request.Role);

        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(string.Join(", ", result.Errors));
        }

        var dto = new UserSummaryDto
        {
            Id = userId,
            UserName = userName,
            Email = email,
            TenantId = effectiveTenantId,
            Roles = !string.IsNullOrWhiteSpace(request.Role) ? [request.Role] : []
        };

        return TypedResults.Created($"/api/Users/{userId}", dto);
    }

    [EndpointSummary("Registrar nuevo usuario por email o username")]
    [EndpointDescription("Permite a un usuario registrarse indicando username, email o ambos, y contraseña.")]
    public static async Task<Results<Ok<UserSummaryDto>, BadRequest<string>>> RegistrarUsuario(
        IIdentityService identityService,
        [FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.UserName))
        {
            return TypedResults.BadRequest("Debe proporcionar al menos un correo electrónico o un nombre de usuario.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest("La contraseña es requerida.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var userName = string.IsNullOrWhiteSpace(request.UserName)
            ? (email ?? throw new InvalidOperationException())
            : request.UserName.Trim();

        var tenantId = request.TenantId ?? Guid.Empty;

        var (result, userId) = await identityService.CreateUserWithTenantAsync(
            userName,
            email,
            request.Password.Trim(),
            tenantId,
            request.Role);

        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(string.Join(", ", result.Errors));
        }

        return TypedResults.Ok(new UserSummaryDto
        {
            Id = userId,
            UserName = userName,
            Email = email,
            TenantId = tenantId,
            Roles = !string.IsNullOrWhiteSpace(request.Role) ? [request.Role] : []
        });
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
