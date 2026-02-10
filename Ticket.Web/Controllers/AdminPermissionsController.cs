using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Admin;

namespace Ticket.Web.Controllers;

[Authorize(Policy = "perm:Admin.Permissions")]
public sealed class AdminPermissionsController : Controller
{
    private readonly AppDbContext _db;

    public AdminPermissionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? roleId = null)
    {
        var roles = await _db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleOptionVm { RoleId = r.Id, Name = r.Name! })
            .ToListAsync();

        if (roles.Count == 0) return View(new AdminPermissionsVm());


        var selectedRoleId = roleId ?? roles[0].RoleId;

        var selectedRole = await _db.Roles.AsNoTracking()
            .Where(r => r.Id == selectedRoleId)
            .Select(r => new { r.Id, r.Name })
            .FirstOrDefaultAsync();
        
        if (selectedRole is null) return NotFound();
        var isAdminRole = string.Equals(selectedRole.Name, "Admin", StringComparison.OrdinalIgnoreCase);

        var allPerms = await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new { p.Key, p.Name })
            .ToListAsync();

        var assignedKeys = await _db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == selectedRoleId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        var vm = new AdminPermissionsVm
        {
            SelectedRoleId = selectedRole.Id,
            SelectedRoleName = selectedRole.Name ?? "",
            Roles = roles,
            IsRoleLocked = isAdminRole,
            LockReason = isAdminRole ? "Admin rolü kilitlidir. Bu rolden yetki kaldırılamaz." : null,
            Permissions = allPerms.Select(p => new PermissionRowVm
            {
                Key = p.Key,
                Name = p.Name,
                Assigned = assignedKeys.Contains(p.Key),
                Locked = isAdminRole
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid selectedRoleId, string[]? keys)
    {
        var roleName = await _db.Roles
            .Where(r => r.Id == selectedRoleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync();

        if (roleName is null) return NotFound();

        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Err"] = "Admin rolü kilitlidir. Değişiklik kaydedilmedi.";
            return RedirectToAction(nameof(Index), new { roleId = selectedRoleId });
        }

        keys ??= Array.Empty<string>();

        var validKeys = await _db.Permissions.AsNoTracking()
            .Select(p => p.Key)
            .ToListAsync();

        var desired = keys
            .Intersect(validKeys)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var current = await _db.RolePermissions
            .Where(rp => rp.RoleId == selectedRoleId)
            .ToListAsync();

        var currentSet = current
            .Select(x => x.PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        var toRemove = current.Where(x => !desired.Contains(x.PermissionKey)).ToList();
        _db.RolePermissions.RemoveRange(toRemove);

        var toAdd = desired.Where(k => !currentSet.Contains(k))
            .Select(k => new Ticket.Infrastructure.Identity.RolePermission
            {
                RoleId = selectedRoleId,
                PermissionKey = k
            })
            .ToList();

        _db.RolePermissions.AddRange(toAdd);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Yetkiler kaydedildi.";
        return RedirectToAction(nameof(Index), new { roleId = selectedRoleId });
    }

}
