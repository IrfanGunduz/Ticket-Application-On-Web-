using Microsoft.AspNetCore.Authorization;

namespace Ticket.Web.Auth;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Admin rolü her şeye yetkili
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Permission claim varsa OK
        if (context.User.HasClaim("perm", requirement.PermissionKey))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
