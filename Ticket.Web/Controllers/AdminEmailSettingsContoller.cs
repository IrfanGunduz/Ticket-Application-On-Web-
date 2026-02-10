using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Models.Admin;

namespace Ticket.Web.Controllers;

[Authorize(Policy = "perm:Admin.Users")]
public sealed class AdminEmailSettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<AdminEmailSettingsController> _logger;

    public AdminEmailSettingsController(
        AppDbContext db,
        IDataProtectionProvider dp,
        ILogger<AdminEmailSettingsController> logger)
    {
        _db = db;
        _protector = dp.CreateProtector("Ticket.EmailIngestSettings.v1");
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var s = await GetOrCreateAsync();

        var vm = new AdminEmailSettingsVm
        {
            Enabled = s.Enabled,
            PollSeconds = s.PollSeconds,
            TargetAddress = s.TargetAddress,
            Protocol = s.Protocol,

            ImapHost = s.ImapHost ?? "",
            ImapPort = s.ImapPort,
            ImapUseSsl = s.ImapUseSsl,
            ImapUserName = s.ImapUserName ?? "",
            ImapPassword = null,
            MarkAsRead = s.MarkAsRead,
            Folder = s.Folder,

            Pop3Host = s.Pop3Host ?? "",
            Pop3Port = s.Pop3Port,
            Pop3UseSsl = s.Pop3UseSsl,
            Pop3UserName = s.Pop3UserName ?? "",
            Pop3Password = null
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminEmailSettingsVm vm)
    {
        // Debug için: post edilenleri net gör
        _logger.LogWarning(
            "POST Protocol={Protocol} Pop3Host='{Pop3Host}' Pop3UserName='{Pop3UserName}' ImapHost='{ImapHost}' ImapUserName='{ImapUserName}'",
            vm.Protocol, vm.Pop3Host, vm.Pop3UserName, vm.ImapHost, vm.ImapUserName);

        if (!ModelState.IsValid)
        {
            var errs = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            _logger.LogWarning("Email settings invalid: {Errors}", string.Join(" | ", errs));
            TempData["Err"] = string.Join(" | ", errs);

            // UX: Folder default boş kalmasın vs.
            vm.Folder = string.IsNullOrWhiteSpace(vm.Folder) ? "INBOX" : vm.Folder.Trim();
            return View(vm);
        }

        var s = await GetOrCreateAsync();

        // Ortak alanlar
        s.Enabled = vm.Enabled;
        s.PollSeconds = vm.PollSeconds;
        s.TargetAddress = string.IsNullOrWhiteSpace(vm.TargetAddress) ? null : vm.TargetAddress.Trim();
        s.Protocol = vm.Protocol;

        if (vm.Protocol == EmailIngestProtocol.Imap)
        {
            s.ImapHost = string.IsNullOrWhiteSpace(vm.ImapHost) ? null : vm.ImapHost.Trim();
            s.ImapPort = vm.ImapPort;
            s.ImapUseSsl = vm.ImapUseSsl;
            s.ImapUserName = string.IsNullOrWhiteSpace(vm.ImapUserName) ? null : vm.ImapUserName.Trim();
            s.MarkAsRead = vm.MarkAsRead;
            s.Folder = string.IsNullOrWhiteSpace(vm.Folder) ? "INBOX" : vm.Folder.Trim();

            if (!string.IsNullOrWhiteSpace(vm.ImapPassword))
                s.EncryptedImapPassword = _protector.Protect(vm.ImapPassword);
        }
        else // Pop3
        {
            s.Pop3Host = string.IsNullOrWhiteSpace(vm.Pop3Host) ? null : vm.Pop3Host.Trim();
            s.Pop3Port = vm.Pop3Port;
            s.Pop3UseSsl = vm.Pop3UseSsl;
            s.Pop3UserName = string.IsNullOrWhiteSpace(vm.Pop3UserName) ? null : vm.Pop3UserName.Trim();

            if (!string.IsNullOrWhiteSpace(vm.Pop3Password))
                s.EncryptedPop3Password = _protector.Protect(vm.Pop3Password);
        }

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Mail ayarları kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<EmailIngestSettings> GetOrCreateAsync()
    {
        var s = await _db.EmailIngestSettings
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

        if (s is not null) return s;

        s = new EmailIngestSettings();
        _db.EmailIngestSettings.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }
}
