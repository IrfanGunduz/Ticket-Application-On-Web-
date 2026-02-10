using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;
using Ticket.Infrastructure.Identity;
using Ticket.Web.Models.Tickets;

namespace Ticket.Web.Controllers;

public sealed class TicketsController : Controller
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:Tickets.View")]
    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, TicketStatus? status = null, TicketChannel? channel = null, Guid? assignedToUserId = null, bool today = false, bool? autoRefresh = null)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var query = _db.Tickets
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        

        if (status is not null)
            query = query.Where(t => t.Status == status.Value);

        if (channel is not null)
            query = query.Where(t => t.Channel == channel.Value);

        if (assignedToUserId is not null)
            query = query.Where(t => t.AssignedToUserId == assignedToUserId.Value);

        if (today)
        {
            
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch
            {
                
                tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var startLocal = nowLocal.Date; 
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = startUtc.AddDays(1);

            query = query.Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc);
        }


        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t =>
                t.TicketNo.Contains(term) ||
                t.Subject.Contains(term) ||
                (t.Customer != null && t.Customer.Title.Contains(term)) ||
                (t.Problem != null && t.Problem.Name.Contains(term)));
        }



        var items = await query
            .Select(t => new TicketIndexRowVm
            {
                TicketItemId = t.TicketItemId,
                TicketNo = t.TicketNo,
                Subject = t.Subject,
                StatusValue = t.Status,          
                ChannelValue = t.Channel,        
                CustomerTitle = t.Customer != null ? t.Customer.Title : null,
                ProblemName = t.Problem != null ? t.Problem.Name : null,
                AssignedToUserName = t.AssignedToUserId == null
                    ? null
                    : _db.Users.Where(u => u.Id == t.AssignedToUserId).Select(u => u.UserName).FirstOrDefault(),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();



        var vm = new TicketIndexVm
        {
            Q = q,
            Status = status,
            Channel = channel,
            AssignedToUserId = assignedToUserId,
            Today = today,
            Items = items,
            AutoRefresh = autoRefresh ??  true
        };

        vm.Items = items;
        vm.OpenTickets = items.Where(x => x.StatusValue != TicketStatus.Closed).ToList();
        vm.ClosedTickets = items.Where(x => x.StatusValue == TicketStatus.Closed).ToList();



        // Status dropdown
        vm.StatusOptions.Add(new SelectListItem { Value = "", Text = "(Tümü)" });
        foreach (var s in Enum.GetValues<TicketStatus>())
            vm.StatusOptions.Add(new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString(),
                Selected = status == s
            });

        // Channel dropdown
        vm.ChannelOptions.Add(new SelectListItem { Value = "", Text = "(Tümü)" });
        foreach (var c in Enum.GetValues<TicketChannel>())
            vm.ChannelOptions.Add(new SelectListItem
            {
                Value = c.ToString(),
                Text = c.ToString(),
                Selected = channel == c
            });

        // Assignee dropdown
        vm.AssigneeOptions.Add(new SelectListItem { Value = "", Text = "(Tümü)" });
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        foreach (var u in users)
            vm.AssigneeOptions.Add(new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.UserName ?? u.Id.ToString(),
                Selected = assignedToUserId == u.Id
            });

        return View(vm);
    }

    [Authorize(Policy = "perm:Tickets.Create")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new TicketCreateVm();
        await FillDropdownsAsync(vm);

        vm.ChannelOptions.Clear();
        foreach (var c in Enum.GetValues<TicketChannel>())
        {
            vm.ChannelOptions.Add(new SelectListItem
            {
                Value = c.ToString(),
                Text = TrChannel(c),
                Selected = (vm.Channel == c)
            });
        }

        return View(vm);
    }

    [Authorize(Policy = "perm:Tickets.Create")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            await FillDropdownsAsync(vm);
            return View(vm);
        }

        var now = DateTime.UtcNow;
        var createdByUserId = GetCurrentUserIdOrNull();

        // TicketNo max len 30 ve unique index var -> kısa üret + çakışma kontrolü
        var ticketNo = await GenerateUniqueTicketNoAsync(now);
        if (ticketNo is null)
        {
            ModelState.AddModelError("", "Ticket numarası üretilemedi. Lütfen tekrar deneyin.");
            await FillDropdownsAsync(vm);
            return View(vm);
        }

        var ticketId = Guid.NewGuid();

        var ticket = new TicketItem
        {
            TicketItemId = ticketId,
            TicketNo = ticketNo,

            CustomerId = vm.CustomerId,
            ProblemId = vm.ProblemId,

            Subject = vm.Subject.Trim(),
            Status = TicketStatus.New,
            Channel = vm.Channel,

            CreatedAt = now,
            CreatedByUserId = createdByUserId
        };

        _db.Tickets.Add(ticket);

        // Note required (null olamaz) + max 2000
        var note = string.IsNullOrWhiteSpace(vm.InitialNote)
            ? "Ticket oluşturuldu."
            : vm.InitialNote.Trim();

        if (note.Length > 2000)
            note = note.Substring(0, 2000);

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = ticketId,
            Type = "Created",
            Note = note,
            CreatedAt = now,
            CreatedByUserId = createdByUserId
        });

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Ticket oluşturuldu: {ticketNo}";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "perm:Tickets.View")]
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Problem)
            .Include(t => t.Activities)
            .FirstOrDefaultAsync(t => t.TicketItemId == id);

        if (ticket is null)
            return NotFound();

        // Assigned username
        string? assignedUserName = null;
        if (ticket.AssignedToUserId is not null)
        {
            assignedUserName = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == ticket.AssignedToUserId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();
        }

        // Messages
        var messages = await _db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketItemId == ticket.TicketItemId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageVm
            {
                CreatedAt = m.CreatedAt,
                Direction = m.Direction.ToString(),
                From = m.From,
                To = m.To,
                Subject = m.Subject,
                Body = m.Body ?? ""
            })
            .ToListAsync();

        // Activities + username map
        var acts = ticket.Activities
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var userIds = acts
            .Where(a => a.CreatedByUserId != null)
            .Select(a => a.CreatedByUserId!.Value)
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName);

        
        var customerContactEmails = ticket.CustomerId is null
            ? new List<string>()
            : await _db.CustomerContacts.AsNoTracking()
                .Where(x => x.CustomerId == ticket.CustomerId.Value && x.Email != null)
                .OrderBy(x => x.FullName)
                .Select(x => $"{x.FullName}: {x.Email}" +
                             (x.IsActive ? "" : " (Pasif)") +
                             (x.AllowEmailIngest ? "" : " (Email ingest kapalı)"))
                .ToListAsync();

        var vm = new TicketDetailVm
        {
            TicketItemId = ticket.TicketItemId,
            TicketNo = ticket.TicketNo,
            Subject = ticket.Subject,

            StatusValue = ticket.Status,
            ChannelValue = ticket.Channel,

            CustomerId = ticket.CustomerId,
            ProblemId = ticket.ProblemId,

            CustomerTitle = ticket.Customer?.Title,
            ProblemName = ticket.Problem?.Name,
            CreatedAt = ticket.CreatedAt,

            AssignedToUserId = ticket.AssignedToUserId,
            AssignedToUserName = assignedUserName,

            Activities = acts.Select(a => new TicketActivityVm
            {
                CreatedAt = a.CreatedAt,
                Type = a.Type,
                Note = a.Note,
                CreatedByUserId = a.CreatedByUserId,
                CreatedByUserName = a.CreatedByUserId != null && userMap.TryGetValue(a.CreatedByUserId.Value, out var name)
                    ? name
                    : null
            }).ToList(),

            Messages = messages,

            
            CustomerContactEmails = customerContactEmails
        };

        // =========================
        // Customer dropdown
        // =========================
        var activeCustomers = await _db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Title)
            .Select(c => new { c.CustomerId, c.Title })
            .ToListAsync();

        vm.CustomerOptions.Clear();
        vm.CustomerOptions.Add(new SelectListItem
        {
            Value = "",
            Text = "(Seçilmedi)",
            Selected = ticket.CustomerId is null
        });

        // seçili customer pasifse -> ekle
        if (ticket.CustomerId is not null && !activeCustomers.Any(x => x.CustomerId == ticket.CustomerId.Value))
        {
            var selected = await _db.Customers.AsNoTracking()
                .Where(c => c.CustomerId == ticket.CustomerId.Value)
                .Select(c => new { c.CustomerId, c.Title, c.IsActive })
                .FirstOrDefaultAsync();

            if (selected is not null)
            {
                vm.CustomerOptions.Add(new SelectListItem
                {
                    Value = selected.CustomerId.ToString(),
                    Text = $"{selected.Title} (Pasif)",
                    Selected = true
                });
            }
        }

        foreach (var c in activeCustomers)
        {
            vm.CustomerOptions.Add(new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = c.Title,
                Selected = ticket.CustomerId == c.CustomerId
            });
        }

        // =========================
        // Problem dropdown
        // =========================
        var activeProblems = await _db.Problems.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.ProblemId, p.Name })
            .ToListAsync();

        vm.ProblemOptions.Clear();
        vm.ProblemOptions.Add(new SelectListItem
        {
            Value = "",
            Text = "(Seçilmedi)",
            Selected = ticket.ProblemId is null
        });

        // seçili problem pasifse -> ekle
        if (ticket.ProblemId is not null && !activeProblems.Any(x => x.ProblemId == ticket.ProblemId.Value))
        {
            var selected = await _db.Problems.AsNoTracking()
                .Where(p => p.ProblemId == ticket.ProblemId.Value)
                .Select(p => new { p.ProblemId, p.Name, p.IsActive })
                .FirstOrDefaultAsync();

            if (selected is not null)
            {
                vm.ProblemOptions.Add(new SelectListItem
                {
                    Value = selected.ProblemId.ToString(),
                    Text = $"{selected.Name} (Pasif)",
                    Selected = true
                });
            }
        }

        foreach (var p in activeProblems)
        {
            vm.ProblemOptions.Add(new SelectListItem
            {
                Value = p.ProblemId.ToString(),
                Text = p.Name,
                Selected = ticket.ProblemId == p.ProblemId
            });
        }

        // =========================
        // Assignee dropdown
        // =========================
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        vm.Assignees.Clear();
        vm.Assignees.Add(new SelectListItem
        {
            Value = "",
            Text = "(Atanmamış)",
            Selected = ticket.AssignedToUserId is null
        });

        foreach (var u in users)
        {
            vm.Assignees.Add(new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.UserName ?? u.Id.ToString(),
                Selected = ticket.AssignedToUserId == u.Id
            });
        }

        // =========================
        // Status dropdown
        // =========================
        vm.StatusOptions.Clear();
        foreach (var s in Enum.GetValues<TicketStatus>())
        {
            vm.StatusOptions.Add(new SelectListItem
            {
                Value = s.ToString(),
                Text = TrStatus(s),
                Selected = ticket.Status == s
            });
        }

        return View(vm);
    }


    [Authorize(Policy = "perm:Tickets.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(Guid id, string? newNote)
    {
        // ticket var mı?
        var exists = await _db.Tickets.AnyAsync(t => t.TicketItemId == id);
        if (!exists) return NotFound();

        var note = string.IsNullOrWhiteSpace(newNote)
            ? ""
            : newNote.Trim();

        if (note.Length == 0)
        {
            TempData["Err"] = "Not boş olamaz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // TicketActivity.Note max 2000 :contentReference[oaicite:2]{index=2}
        if (note.Length > 2000) note = note.Substring(0, 2000);

        var now = DateTime.UtcNow;
        var userId = GetCurrentUserIdOrNull();

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = id,
            Type = "NoteAdded", // max 80 :contentReference[oaicite:3]{index=3}
            Note = note,
            CreatedAt = now,
            CreatedByUserId = userId
        });

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Not eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillDropdownsAsync(TicketCreateVm vm)
    {
        vm.Customers = await _db.Customers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Title)
            .Select(x => new SelectListItem
            {
                Value = x.CustomerId.ToString(),
                Text = $"{x.Code} - {x.Title}"
            })
            .ToListAsync();

        vm.Problems = await _db.Problems
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Department).ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.ProblemId.ToString(),
                Text = $"{x.Code} - {x.Name} ({x.Department})"
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:Tickets.Assign")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid? assignedToUserId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.TicketItemId == id);
        if (ticket is null) return NotFound();

        
        var oldAssignedId = ticket.AssignedToUserId;

        
        if (oldAssignedId == assignedToUserId)
            return RedirectToAction(nameof(Details), new { id });

        
        ticket.AssignedToUserId = assignedToUserId;

        var now = DateTime.UtcNow;
        var me = GetCurrentUserIdOrNull();

        
        var idsToResolve = new List<Guid>();
        if (oldAssignedId is not null) idsToResolve.Add(oldAssignedId.Value);
        if (assignedToUserId is not null) idsToResolve.Add(assignedToUserId.Value);

        var nameMap = await _db.Users.AsNoTracking()
            .Where(u => idsToResolve.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName);

        static string NameOrDash(Guid? id, Dictionary<Guid, string?> map)
        {
            if (id is null) return "-";
            return map.TryGetValue(id.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n! : id.Value.ToString();
        }

        var oldName = NameOrDash(oldAssignedId, nameMap);
        var newName = NameOrDash(assignedToUserId, nameMap);

        var note = assignedToUserId is null
            ? $"Atama kaldırıldı. (Önceki: {oldName})"
            : $"Atama güncellendi: {oldName} → {newName}";

        if (note.Length > 2000) note = note[..2000];

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = id,
            Type = "Assigned",
            Note = note,
            CreatedAt = now,
            CreatedByUserId = me
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Atama güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }


    [Authorize(Policy = "perm:Tickets.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(Guid id, TicketStatus newStatus)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.TicketItemId == id);
        if (ticket is null) return NotFound();

        if (ticket.Status == newStatus)
            return RedirectToAction(nameof(Details), new { id });

        var old = ticket.Status;
        ticket.Status = newStatus;

        var now = DateTime.UtcNow;
        var me = GetCurrentUserIdOrNull();

        var note = $"Status: {old} → {newStatus}";
        if (note.Length > 2000) note = note[..2000];

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = id,
            Type = "StatusChanged",
            Note = note,
            CreatedAt = now,
            CreatedByUserId = me
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Durum güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "perm:Tickets.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeCustomer(Guid id, Guid? customerId)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.TicketItemId == id);

        if (ticket is null) return NotFound();

        var oldId = ticket.CustomerId;
        var oldName = ticket.Customer?.Title;

        ticket.CustomerId = customerId;

        string? newName = null;
        if (customerId is not null)
        {
            newName = await _db.Customers.AsNoTracking()
                .Where(c => c.CustomerId == customerId.Value)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();
        }

        var note =
            customerId is null ? $"Müşteri kaldırıldı. (Önceki: {oldName ?? (oldId?.ToString() ?? "-")})"
            : $"Müşteri güncellendi: {(oldName ?? (oldId?.ToString() ?? "-"))} → {(newName ?? customerId.ToString())}";

        if (note.Length > 2000) note = note[..2000];

        var userId = GetCurrentUserIdOrNull();

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = id,
            Type = "Customer.Changed",
            Note = note,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Müşteri güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }


    [Authorize(Policy = "perm:Tickets.Edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeProblem(Guid id, Guid? problemId)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Problem)
            .FirstOrDefaultAsync(t => t.TicketItemId == id);

        if (ticket is null) return NotFound();

        var oldId = ticket.ProblemId;
        var oldName = ticket.Problem?.Name;

        ticket.ProblemId = problemId;

        string? newName = null;
        if (problemId is not null)
        {
            newName = await _db.Problems.AsNoTracking()
                .Where(p => p.ProblemId == problemId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
        }

        var note =
            problemId is null ? $"Sorun/Hizmet kaldırıldı. (Önceki: {oldName ?? (oldId?.ToString() ?? "-")})"
            : $"Sorun/Hizmet güncellendi: {(oldName ?? (oldId?.ToString() ?? "-"))} → {(newName ?? problemId.ToString())}";

        if (note.Length > 2000) note = note[..2000];

        var userId = GetCurrentUserIdOrNull();

        _db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = id,
            Type = "Problem.Changed",
            Note = note,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        });

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Sorun/Hizmet güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }



    private Guid? GetCurrentUserIdOrNull()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private async Task<string?> GenerateUniqueTicketNoAsync(DateTime utcNow)
    {
        // Format: T-YYYYMMDD-HHMMSS-XXXX  (<= 22 chars)
        static string Candidate(DateTime t)
            => $"T-{t:yyyyMMdd}-{t:HHmmss}-{Random.Shared.Next(1000, 9999)}";

        for (var i = 0; i < 5; i++)
        {
            var c = Candidate(utcNow);
            var exists = await _db.Tickets.AnyAsync(x => x.TicketNo == c);
            if (!exists) return c;
        }

        return null;
    }
    private static string TrChannel(TicketChannel c) => c switch
    {
        TicketChannel.Manual => "Manuel",
        TicketChannel.Email => "E-posta",
        TicketChannel.Phone => "Telefon",
        _ => c.ToString()
    };


    private static string TrStatus(TicketStatus s) => s switch
    {
        TicketStatus.New => "Yeni",
        TicketStatus.InProgress => "İşlemde",
        TicketStatus.WaitingCustomer => "Müşteri Bekleniyor",
        TicketStatus.Closed => "Kapalı",
        TicketStatus.Canceled => "İptal Edildi",
        _ => s.ToString()
    };

}
