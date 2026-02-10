using Microsoft.AspNetCore.Authorization;

namespace Ticket.Web.Auth;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }
    public PermissionRequirement(string permissionKey) => PermissionKey = permissionKey;
}
