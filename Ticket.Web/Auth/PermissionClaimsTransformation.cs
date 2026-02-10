using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Setup;

namespace Ticket.Web.Auth;

public sealed class PermissionsClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _db;
    private readonly ISetupState _setup;

    public PermissionsClaimsTransformation(AppDbContext db, ISetupState setup)
    {
        _db = db;
        _setup = setup;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!_setup.IsConfigured) return principal;
        if (principal.Identity?.IsAuthenticated != true) return principal;

        var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return principal;

        var identity = (ClaimsIdentity)principal.Identity;

        // Refresh: yetki değişiklikleri anında yansısın
        foreach (var c in identity.FindAll("perm").ToList())
            identity.RemoveClaim(c);

        var isAdminRole = principal.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase));

        // Admin => HER ZAMAN tüm permission claim’leri 
        if (isAdminRole)
        {
            var allKeys = await _db.Permissions.AsNoTracking()
                .Select(p => p.Key)
                .ToListAsync();

            foreach (var k in allKeys.Distinct(StringComparer.Ordinal))
                identity.AddClaim(new Claim("perm", k));

            return principal;
        }

        // Role perms
        var roles = principal.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        var rolePerms = new List<string>();
        if (roles.Count > 0)
        {
            var roleIds = await _db.Roles
                .Where(r => roles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            if (roleIds.Count > 0)
            {
                rolePerms = await _db.RolePermissions
                    .Where(rp => roleIds.Contains(rp.RoleId))
                    .Select(rp => rp.PermissionKey)
                    .Distinct()
                    .ToListAsync();
            }
        }

        // User grants
        var grants = await _db.UserPermissionGrants
            .Where(x => x.UserId == userId)
            .Select(x => x.PermissionKey)
            .Distinct()
            .ToListAsync();

        // User denies
        var denyList = await _db.UserPermissionDenies.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PermissionKey)
            .Distinct()
            .ToListAsync();

        var denies = denyList.ToHashSet(StringComparer.Ordinal);


        var effective = rolePerms.Concat(grants)
            .Distinct(StringComparer.Ordinal)
            .Where(k => !denies.Contains(k));

        foreach (var k in effective)
            identity.AddClaim(new Claim("perm", k));

        return principal;
    }
}
