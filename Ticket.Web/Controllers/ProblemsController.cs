using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Problems;

namespace Ticket.Web.Controllers;

public sealed class ProblemsController : Controller
{
    private readonly AppDbContext _db;

    public ProblemsController(AppDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:Problems.View")]
    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, bool showInactive = false)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var query = _db.Problems
            .AsNoTracking()
            .AsQueryable();

        //  showInactive false ise sadece aktifler
        if (!showInactive)
            query = query.Where(p => p.IsActive);

        //  case-insensitive arama
        if (q is not null)
        {
            var term = q.ToLower();

            query = query.Where(p =>
                p.Code.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term) ||
                p.Department.ToLower().Contains(term));
        }

        
        query = query.OrderBy(p => p.Department).ThenBy(p => p.Code);

        var items = await query
            .Select(p => new ProblemRowVm
            {
                ProblemId = p.ProblemId,
                Code = p.Code,
                Name = p.Name,
                Department = p.Department,

                IsActive = p.IsActive,
                UsedTicketCount = _db.Tickets.Count(t => t.ProblemId == p.ProblemId)
            })
            .ToListAsync();

        return View(new ProblemIndexVm
        {
            Q = q,
            ShowInactive = showInactive,
            Items = items
        });
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ProblemEditVm());
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProblemEditVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var entity = new Problem
        {
            ProblemId = Guid.NewGuid(),
            Code = vm.Code.Trim(),
            Name = vm.Name.Trim(),
            Department = vm.Department.Trim(),
            IsActive = true
        };


        _db.Problems.Add(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Code unique index var. :contentReference[oaicite:3]{index=3}
            ModelState.AddModelError(nameof(vm.Code), "Bu kod zaten mevcut.");
            return View(vm);
        }

        TempData["Ok"] = "Sorun/Hizmet eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var p = await _db.Problems.AsNoTracking().FirstOrDefaultAsync(x => x.ProblemId == id);
        if (p is null) return NotFound();

        return View(new ProblemEditVm
        {
            ProblemId = p.ProblemId,
            Code = p.Code,
            Name = p.Name,
            Department = p.Department
        });
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProblemEditVm vm)
    {
        if (vm.ProblemId is null) return BadRequest();

        if (!ModelState.IsValid)
            return View(vm);

        var entity = await _db.Problems.FirstOrDefaultAsync(x => x.ProblemId == vm.ProblemId.Value);
        if (entity is null) return NotFound();

        entity.Code = vm.Code.Trim();
        entity.Name = vm.Name.Trim();
        entity.Department = vm.Department.Trim();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(vm.Code), "Bu kod zaten mevcut.");
            return View(vm);
        }

        TempData["Ok"] = "Sorun/Hizmet güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, string? q = null, bool showInactive = false)
    {
        var p = await _db.Problems.FirstOrDefaultAsync(x => x.ProblemId == id);
        if (p is null) return NotFound();

        p.IsActive = false;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Kayıt pasife alındı.";
        return RedirectToAction(nameof(Index), new { q, showInactive });
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id, string? q = null, bool showInactive = false)
    {
        var p = await _db.Problems.FirstOrDefaultAsync(x => x.ProblemId == id);
        if (p is null) return NotFound();

        p.IsActive = true;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Kayıt aktif edildi.";
        return RedirectToAction(nameof(Index), new { q, showInactive });
    }

    [Authorize(Policy = "perm:Problems.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? q = null, bool showInactive = false)
    {
        var used = await _db.Tickets.AsNoTracking().AnyAsync(t => t.ProblemId == id);
        if (used)
        {
            TempData["Err"] = "Bu sorun/hizmet ticketlarda kullanıldığı için silinemez. Pasife alabilirsiniz.";
            return RedirectToAction(nameof(Index), new { q, showInactive });
        }

        var p = await _db.Problems.FirstOrDefaultAsync(x => x.ProblemId == id);
        if (p is null) return NotFound();

        _db.Problems.Remove(p);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Sorun/Hizmet silindi.";
        return RedirectToAction(nameof(Index), new { q, showInactive });
    }

}
