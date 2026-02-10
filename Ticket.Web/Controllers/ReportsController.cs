using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Reports;

namespace Ticket.Web.Controllers;

[Authorize(Policy = "perm:Reports.View")]
public sealed class ReportsController : Controller
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Top Customers (ticket count)
        var topCustomers = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.CustomerId != null)
            .GroupBy(t => t.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, TicketCount = g.Count() })
            .OrderByDescending(x => x.TicketCount)
            .Take(10)
            .Join(
                _db.Customers.AsNoTracking(),
                x => x.CustomerId,
                c => c.CustomerId,
                (x, c) => new TopCustomerRow
                {
                    CustomerId = c.CustomerId,
                    CustomerTitle = c.Title,
                    TicketCount = x.TicketCount
                })
            .ToListAsync();

        // Top Problems (ticket count)
        var topProblems = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.ProblemId != null)
            .GroupBy(t => t.ProblemId!.Value)
            .Select(g => new { ProblemId = g.Key, TicketCount = g.Count() })
            .OrderByDescending(x => x.TicketCount)
            .Take(10)
            .Join(
                _db.Problems.AsNoTracking(),
                x => x.ProblemId,
                p => p.ProblemId,
                (x, p) => new TopProblemRow
                {
                    ProblemId = p.ProblemId,
                    ProblemName = p.Name,
                    Department = p.Department,
                    TicketCount = x.TicketCount
                })
            .ToListAsync();

        // Monthly counts (last 12 months)
        var fromUtc = DateTime.UtcNow.AddMonths(-11);
        var monthly = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= fromUtc)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new MonthlyRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TicketCount = g.Count()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var vm = new ReportsIndexVm
        {
            TopCustomers = topCustomers,
            TopProblems = topProblems,
            Monthly = monthly
        };

        return View(vm);
    }
}
