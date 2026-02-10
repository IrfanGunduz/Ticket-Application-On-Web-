using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Persistence;
using Ticket.Infrastructure.Entities;
using Ticket.Web.Models.Home;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    [Authorize] 
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var openCount = await _db.Tickets.AsNoTracking()
            .CountAsync(t => t.Status != TicketStatus.Closed);

        var unassignedCount = await _db.Tickets.AsNoTracking()
            .CountAsync(t => t.Status != TicketStatus.Closed && t.AssignedToUserId == null);

        var todayCreatedCount = await _db.Tickets.AsNoTracking()
            .CountAsync(t => t.CreatedAt >= todayUtc && t.CreatedAt < tomorrowUtc);

        var latest = await _db.Tickets.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(8)
            .Select(t => new HomeLatestTicketVm
            {
                TicketItemId = t.TicketItemId,
                TicketNo = t.TicketNo,
                Subject = t.Subject,
                StatusValue = t.Status,
                ChannelValue = t.Channel,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        var vm = new HomeIndexVm
        {
            OpenCount = openCount,
            UnassignedCount = unassignedCount,
            TodayCreatedCount = todayCreatedCount,
            LatestTickets = latest
        };

        return View(vm);
    }
}
