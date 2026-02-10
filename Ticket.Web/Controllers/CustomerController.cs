using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Customers;

namespace Ticket.Web.Controllers;

public sealed class CustomersController : Controller
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:Customers.View")]
    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, bool showInactive = false)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var query = _db.Customers.AsNoTracking().AsQueryable();

        
        if (!showInactive)
            query = query.Where(c => c.IsActive);

        if (q is not null)
        {
            var term = q.ToLower();

            query = query.Where(c =>
                c.Code.ToLower().Contains(term) ||
                c.Title.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }


        var items = await query
            .OrderBy(c => c.Code)
            .Select(c => new CustomerRowVm
            {
                CustomerId = c.CustomerId,
                Code = c.Code,
                Title = c.Title,
                Phone = c.Phone,
                Email = c.Email,
                IsActive = c.IsActive
            })
            .ToListAsync();

        return View(new CustomerIndexVm { Q = q, Items = items, ShowInactive = showInactive });
    }


    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpGet]
    public IActionResult Create()
        => View(new CustomerEditVm());

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerEditVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var entity = new Customer
        {
            CustomerId = Guid.NewGuid(),
            Code = vm.Code.Trim(),
            Title = vm.Title.Trim(),
            Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim(),
        };

        _db.Customers.Add(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Customer.Code unique index var. :contentReference[oaicite:2]{index=2}
            ModelState.AddModelError(nameof(vm.Code), "Bu kod zaten mevcut.");
            return View(vm);
        }

        TempData["Ok"] = "Müşteri eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound();

        return View(new CustomerEditVm
        {
            CustomerId = c.CustomerId,
            Code = c.Code,
            Title = c.Title,
            Phone = c.Phone,
            Email = c.Email,
            Address = c.Address
        });
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerEditVm vm)
    {
        if (vm.CustomerId is null) return BadRequest();

        if (!ModelState.IsValid)
            return View(vm);

        var entity = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == vm.CustomerId.Value);
        if (entity is null) return NotFound();

        entity.Code = vm.Code.Trim();
        entity.Title = vm.Title.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
        entity.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        entity.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(vm.Code), "Bu kod zaten mevcut.");
            return View(vm);
        }

        TempData["Ok"] = "Müşteri güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound();

        c.IsActive = true;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Müşteri tekrar aktif edildi.";
        return RedirectToAction(nameof(Index), new { showInactive = true });
    }


    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound();

        // zaten pasifse idempotent
        if (!c.IsActive)
        {
            TempData["Ok"] = "Müşteri zaten pasif.";
            return RedirectToAction(nameof(Index));
        }

        c.IsActive = false;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Müşteri pasife alındı.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpGet]
    public async Task<IActionResult> Contacts(Guid id)
    {
        var c = await _db.Customers
            .AsNoTracking()
            .Include(x => x.Contacts)
            .FirstOrDefaultAsync(x => x.CustomerId == id);

        if (c is null) return NotFound();

        var vm = new CustomerContactsVm
        {
            CustomerId = c.CustomerId,
            CustomerTitle = c.Title,
            Items = c.Contacts
                .OrderBy(x => x.FullName)
                .Select(x => new CustomerContactRowVm
                {
                    CustomerContactId = x.CustomerContactId,
                    FullName = x.FullName,
                    Phone = x.Phone,
                    Mobile = x.Mobile,
                    Email = x.Email
                }).ToList()
        };

        return View(vm);
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContact(Guid customerId, string fullName, string? phone, string? mobile, string? email)
    {
        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["Err"] = "Ad Soyad boş olamaz.";
            return RedirectToAction(nameof(Contacts), new { id = customerId });
        }

        var exists = await _db.Customers.AnyAsync(x => x.CustomerId == customerId);
        if (!exists) return NotFound();

        _db.CustomerContacts.Add(new CustomerContact
        {
            CustomerContactId = Guid.NewGuid(),
            CustomerId = customerId,
            FullName = fullName,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim()
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "İletişim eklendi.";
        return RedirectToAction(nameof(Contacts), new { id = customerId });
    }

    [Authorize(Policy = "perm:Customers.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContact(Guid customerId, Guid contactId)
    {
        var cc = await _db.CustomerContacts.FirstOrDefaultAsync(x => x.CustomerContactId == contactId && x.CustomerId == customerId);
        if (cc is null) return NotFound();

        _db.CustomerContacts.Remove(cc);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "İletişim silindi.";
        return RedirectToAction(nameof(Contacts), new { id = customerId });
    }

}
