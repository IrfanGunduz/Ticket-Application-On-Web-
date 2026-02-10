using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Identity;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Admin;

namespace Ticket.Web.Controllers;

[Authorize(Policy = "perm:Admin.Users")]
public sealed class AdminUserPermissionsController : Controller
{
    private readonly AppDbContext _db;

    public AdminUserPermissionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? userId = null)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserOptionVm
            {
                UserId = u.Id,
                UserName = u.UserName ?? u.Id.ToString()
            })
            .ToListAsync();

        if (users.Count == 0)
            return View(new AdminUserPermissionsVm());

        var selectedUserId = userId ?? users[0].UserId;

        var selectedUserName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == selectedUserId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        if (selectedUserName is null)
            return NotFound();

        // Target user Admin mi? (UI’da deny’i kilitlemek için)
        var adminRoleId = await _db.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var targetIsAdmin = adminRoleId != Guid.Empty &&
                            await _db.UserRoles.AnyAsync(ur => ur.UserId == selectedUserId && ur.RoleId == adminRoleId);

        var allPerms = await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new { p.Key, p.Name })
            .ToListAsync();

        var grantList = await _db.UserPermissionGrants.AsNoTracking()
            .Where(x => x.UserId == selectedUserId)
            .Select(x => x.PermissionKey)
            .Distinct()
            .ToListAsync();

        var denyList = await _db.UserPermissionDenies.AsNoTracking()
            .Where(x => x.UserId == selectedUserId)
            .Select(x => x.PermissionKey)
            .Distinct()
            .ToListAsync();

        var grants = grantList.ToHashSet(StringComparer.Ordinal);
        var denies = denyList.ToHashSet(StringComparer.Ordinal);

        var vm = new AdminUserPermissionsVm
        {
            SelectedUserId = selectedUserId,
            SelectedUserName = selectedUserName,
            Users = users,
            Permissions = allPerms.Select(p => new PermissionRowVm
            {
                Key = p.Key,
                Name = p.Name,
                Assigned = grants.Contains(p.Key),
                Denied = denies.Contains(p.Key),
                Locked = targetIsAdmin 
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid selectedUserId, string[]? grantKeys, string[]? denyKeys)
    {
        grantKeys ??= Array.Empty<string>();
        denyKeys ??= Array.Empty<string>();

        var userExists = await _db.Users.AnyAsync(u => u.Id == selectedUserId);
        if (!userExists) return NotFound();

        
        var adminRoleId = await _db.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var targetIsAdmin = adminRoleId != Guid.Empty &&
                            await _db.UserRoles.AnyAsync(ur => ur.UserId == selectedUserId && ur.RoleId == adminRoleId);

        // valid permission key set
        var validKeys = await _db.Permissions.AsNoTracking()
            .Select(p => p.Key)
            .ToListAsync();

        var validSet = validKeys.ToHashSet(StringComparer.Ordinal);

        var desiredDenies = targetIsAdmin
            ? new HashSet<string>(StringComparer.Ordinal)
            : denyKeys.Where(k => !string.IsNullOrWhiteSpace(k))
                      .Select(k => k.Trim())
                      .Where(k => validSet.Contains(k))
                      .ToHashSet(StringComparer.Ordinal);

        // Deny öncelikli: deny seçilmişse grant'ten çıkar
        var desiredGrants = grantKeys.Where(k => !string.IsNullOrWhiteSpace(k))
                                     .Select(k => k.Trim())
                                     .Where(k => validSet.Contains(k))
                                     .Where(k => !desiredDenies.Contains(k))
                                     .ToHashSet(StringComparer.Ordinal);

        // ===== GRANTS sync =====
        var currentGrants = await _db.UserPermissionGrants
            .Where(x => x.UserId == selectedUserId)
            .ToListAsync();

        var currentGrantSet = currentGrants.Select(x => x.PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        _db.UserPermissionGrants.RemoveRange(
            currentGrants.Where(x => !desiredGrants.Contains(x.PermissionKey)));

        _db.UserPermissionGrants.AddRange(
            desiredGrants.Where(k => !currentGrantSet.Contains(k))
                         .Select(k => new UserPermissionGrant { UserId = selectedUserId, PermissionKey = k }));

        // ===== DENIES sync =====
        var currentDenies = await _db.UserPermissionDenies
            .Where(x => x.UserId == selectedUserId)
            .ToListAsync();

        var currentDenySet = currentDenies.Select(x => x.PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        _db.UserPermissionDenies.RemoveRange(
            currentDenies.Where(x => !desiredDenies.Contains(x.PermissionKey)));

        _db.UserPermissionDenies.AddRange(
            desiredDenies.Where(k => !currentDenySet.Contains(k))
                         .Select(k => new UserPermissionDeny { UserId = selectedUserId, PermissionKey = k }));

        await _db.SaveChangesAsync();

        TempData["Ok"] = targetIsAdmin
            ? "Kaydedildi. (Admin için deny uygulanmaz)"
            : "Kaydedildi.";

        return RedirectToAction(nameof(Index), new { userId = selectedUserId });
    }
}
