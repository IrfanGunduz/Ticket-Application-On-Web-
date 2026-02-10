using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Identity;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Admin;

namespace Ticket.Web.Controllers;

[Authorize(Policy = "perm:Admin.Users")]
public sealed class AdminUsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _db.Roles.AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        var userRoles = await _db.UserRoles.AsNoTracking().ToListAsync();

        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        var vm = new AdminUsersIndexVm
        {
            Users = users.Select(u =>
            {
                var rids = userRoles.Where(ur => ur.UserId == u.Id).Select(ur => ur.RoleId).ToList();
                var rnames = roles.Where(r => rids.Contains(r.Id)).Select(r => r.Name).Where(n => n != null)!;
                return new AdminUserRowVm
                {
                    Id = u.Id,
                    UserName = u.UserName ?? u.Id.ToString(),
                    Roles = string.Join(", ", rnames)
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AdminUserCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var exists = await _db.Users.AnyAsync(u => u.UserName == vm.UserName);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.UserName), "Bu kullanıcı adı zaten var.");
            return View(vm);
        }

        var userRoleId = await _db.Roles.Where(r => r.Name == "User").Select(r => r.Id).FirstOrDefaultAsync();
        var adminRoleId = await _db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();

        if (userRoleId == Guid.Empty)
        {
            ModelState.AddModelError("", "User rolü bulunamadı (seed sorunu).");
            return View(vm);
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = vm.UserName,
            NormalizedUserName = vm.UserName.ToUpperInvariant(),
            Email = null,
            NormalizedEmail = null,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, vm.Password);

        _db.Users.Add(user);

        // default role: User
        _db.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = user.Id,
            RoleId = userRoleId
        });

        // optional: Admin
        if (vm.IsAdmin && adminRoleId != Guid.Empty)
        {
            _db.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = adminRoleId
            });
        }

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Kullanıcı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        return View(new AdminUserResetPasswordVm { Id = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminUserResetPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == vm.Id);
        if (user is null) return NotFound();

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, vm.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Şifre sıfırlandı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Kendini silmeyi engelle
        var meStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(meStr, out var me) && me == id)
        {
            TempData["Err"] = "Kendi hesabını silemezsin.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        
        var grants = await _db.UserPermissionGrants.Where(x => x.UserId == id).ToListAsync();
        _db.UserPermissionGrants.RemoveRange(grants);

        var denies = await _db.UserPermissionDenies.Where(x => x.UserId == id).ToListAsync();
        _db.UserPermissionDenies.RemoveRange(denies);

        // userRoles cleanup
        var urs = await _db.UserRoles.Where(x => x.UserId == id).ToListAsync();
        _db.UserRoles.RemoveRange(urs);

        _db.Users.Remove(user);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Kullanıcı silindi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Admin.Users")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserName(Guid id, string? newUserName)
    {
        newUserName = (newUserName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(newUserName))
        {
            TempData["Err"] = "Kullanıcı adı boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        // no-op
        if (string.Equals(user.UserName, newUserName, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Ok"] = "Kullanıcı adı zaten bu şekilde.";
            return RedirectToAction(nameof(Index));
        }

        // duplicate kontrol (Normalized üzerinden)
        var normalized = _userManager.NormalizeName(newUserName);
        var exists = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id != id && u.NormalizedUserName == normalized);

        if (exists)
        {
            TempData["Err"] = "Bu kullanıcı adı zaten var.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.SetUserNameAsync(user, newUserName);
        if (!result.Succeeded)
        {
            TempData["Err"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Ok"] = "Kullanıcı adı güncellendi.";
        return RedirectToAction(nameof(Index));
    }





}
