using System.Security.Claims;

namespace Ticket.Web.Auth;

public static class PermissionExtensions
{
    public static bool HasPerm(this ClaimsPrincipal user, string key)
        => user?.HasClaim("perm", key) == true;
}
