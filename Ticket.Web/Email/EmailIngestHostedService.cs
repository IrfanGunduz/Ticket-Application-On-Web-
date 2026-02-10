using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Setup;

namespace Ticket.Web.Email;

public sealed class EmailIngestHostedService : BackgroundService
{
    private static readonly Regex TicketNoRegex =
        new(@"\bEML-\d{8}-[0-9A-F]{8}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<EmailIngestHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISetupState _setup;

    public EmailIngestHostedService(
        ILogger<EmailIngestHostedService> logger,
        IServiceScopeFactory scopeFactory,
        ISetupState setup)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _setup = setup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email ingest hosted service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_setup.IsConfigured)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var reader = scope.ServiceProvider.GetRequiredService<IEmailInboxReader>();

                var s = await db.EmailIngestSettings.AsNoTracking().FirstOrDefaultAsync(stoppingToken);
                var poll = s?.PollSeconds is > 0 ? s.PollSeconds : 30;

                if (s is null || !s.Enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(poll), stoppingToken);
                    continue;
                }

                var messages = await reader.FetchNewAsync(stoppingToken);

                // Aynı externalId birden çok kez gelirse tekilleştir (protokol bağımsız)
                var unique = messages
                    .GroupBy(m => m.ExternalId, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();

                var extIds = unique.Select(m => m.ExternalId).ToList();

                var msgIds = unique
                    .Select(m => (m.MessageId ?? "").Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();


                if (unique.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(poll), stoppingToken);
                    continue;
                }

                

                

                // 1) TicketMessages duplicate (ExternalId + MessageId)
                var existingMsgsByExternal = await db.TicketMessages.AsNoTracking()
                    .Where(m => m.ExternalMessageId != null && extIds.Contains(m.ExternalMessageId))
                    .Select(m => m.ExternalMessageId!)
                    .ToListAsync(stoppingToken);

                var existingMsgsByMsgId = msgIds.Count == 0
                    ? new List<string>()
                    : await db.TicketMessages.AsNoTracking()
                        .Where(m => m.InternetMessageId != null && msgIds.Contains(m.InternetMessageId))
                        .Select(m => m.InternetMessageId!)
                        .ToListAsync(stoppingToken);

                // 2) Receipt duplicate (D seçeneği) (ExternalId + MessageId)
                var existingReceiptsByExternal = await db.EmailIngestReceipts.AsNoTracking()
                    .Where(r => extIds.Contains(r.ExternalMessageId))
                    .Select(r => r.ExternalMessageId)
                    .ToListAsync(stoppingToken);

                var existingReceiptsByMsgId = msgIds.Count == 0
                    ? new List<string>()
                    : await db.EmailIngestReceipts.AsNoTracking()
                        .Where(r => r.InternetMessageId != null && msgIds.Contains(r.InternetMessageId))
                        .Select(r => r.InternetMessageId!)
                        .ToListAsync(stoppingToken);

                var existingExternalSet = existingMsgsByExternal
                    .Concat(existingReceiptsByExternal)
                    .ToHashSet(StringComparer.Ordinal);

                var existingMsgIdSet = existingMsgsByMsgId
                    .Concat(existingReceiptsByMsgId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var toHandle = unique.Where(m =>
                {
                    if (existingExternalSet.Contains(m.ExternalId)) return false;

                    var mid = (m.MessageId ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(mid) && existingMsgIdSet.Contains(mid)) return false;

                    return true;
                }).ToList();

                if (toHandle.Count == 0)
                {
                    // IMAP için Seen işaretleme vs (POP3 no-op olabilir)
                    await reader.AcknowledgeAsync(extIds, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(poll), stoppingToken);
                    continue;
                }

                var createdTickets = 0;
                var appendedToExisting = 0;
                var skippedNotAllowlisted = 0;

                foreach (var m in toHandle)
                {
                    var from = (m.From ?? "").Trim();
                    var body = (m.Body ?? "").Trim();
                    var messageId = (m.MessageId ?? "").Trim();
                    var inReplyTo = (m.InReplyTo ?? "").Trim();

                    Guid? customerId = null;

                    if (!string.IsNullOrWhiteSpace(from))
                    {
                        var fromLower = from.ToLowerInvariant();

                        var ccid = await db.CustomerContacts.AsNoTracking()
                            .Where(x =>
                                x.Email != null &&
                                x.Email.ToLower() == fromLower &&
                                x.IsActive &&
                                x.AllowEmailIngest)
                            .Select(x => x.CustomerId)
                            .FirstOrDefaultAsync(stoppingToken);

                        if (ccid != Guid.Empty)
                        {
                            customerId = ccid;
                        }
                        else
                        {
                            var cid = await db.Customers.AsNoTracking()
                                .Where(c =>
                                    c.IsActive &&
                                    c.Email != null &&
                                    c.Email.ToLower() == fromLower)
                                .Select(c => c.CustomerId)
                                .FirstOrDefaultAsync(stoppingToken);

                            if (cid != Guid.Empty) customerId = cid;
                        }
                    }

                    // D seçeneği: allowlist değilse receipt yaz ve bir daha poll’da görme
                    if (customerId is null)
                    {
                        skippedNotAllowlisted++;

                        db.EmailIngestReceipts.Add(new EmailIngestReceipt
                        {
                            ExternalMessageId = m.ExternalId,
                            InternetMessageId = string.IsNullOrWhiteSpace(messageId) ? null : messageId,
                            Status = "Skipped.NotAllowlisted",
                            From = m.From,
                            Subject = m.Subject,
                            ReceivedAtUtc = m.ReceivedAtUtc
                        });

                        _logger.LogInformation(
                            "Email ingest skipped (not allowlisted): From={From} Subject={Subject}",
                            m.From, m.Subject);

                        continue;
                    }

                    // 1) TicketNo (subject + body içinde ara)
                    var ticketNo = TryExtractTicketNo(m.Subject) ?? TryExtractTicketNo(body);

                    if (!string.IsNullOrWhiteSpace(ticketNo))
                    {
                        var existingTicket = await db.Tickets
                            .FirstOrDefaultAsync(t => t.TicketNo == ticketNo && t.CustomerId == customerId, stoppingToken);

                        if (existingTicket is not null)
                        {
                            ReopenIfNeeded(existingTicket);

                            AppendInboundMessage(db, existingTicket.TicketItemId, m, body);

                            appendedToExisting++;
                            continue;
                        }
                    }

                    // 2) Threading: In-Reply-To / References -> eski TicketMessage.InternetMessageId ile eşleştir
                    var existingTicketIdByThread = await ResolveByThreadHeadersAsync(db, customerId.Value, m, stoppingToken);

                    if (existingTicketIdByThread is not null)
                    {
                        var existingTicket = await db.Tickets
                            .FirstOrDefaultAsync(t => t.TicketItemId == existingTicketIdByThread.Value, stoppingToken);

                        if (existingTicket is not null)
                        {
                            ReopenIfNeeded(existingTicket);

                            AppendInboundMessage(db, existingTicket.TicketItemId, m, body);

                            appendedToExisting++;
                            continue;
                        }
                    }

                    // 3) Yeni ticket
                    var ticketId = Guid.NewGuid();

                    var ticket = new TicketItem
                    {
                        TicketItemId = ticketId,
                        TicketNo = GenerateTicketNo(),
                        CustomerId = customerId,
                        ProblemId = null,
                        Subject = string.IsNullOrWhiteSpace(m.Subject) ? "(Konu yok)" : m.Subject!,
                        Status = TicketStatus.New,
                        Channel = TicketChannel.Email,
                        AssignedToUserId = null,
                        CreatedByUserId = null,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Tickets.Add(ticket);

                    AppendInboundMessage(db, ticketId, m, body);

                    createdTickets++;
                }

                await db.SaveChangesAsync(stoppingToken);

                // IMAP için Seen işaretleme vs: handle edilen tüm externalId’leri ack et (POP3 reader no-op olabilir)
                await reader.AcknowledgeAsync(toHandle.Select(x => x.ExternalId), stoppingToken);

                _logger.LogInformation(
                    "Email ingest: created={Created} appended={Appended} skippedNotAllowlisted={Skipped}.",
                    createdTickets, appendedToExisting, skippedNotAllowlisted);

                await Task.Delay(TimeSpan.FromSeconds(poll), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email ingest loop error.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private static void AppendInboundMessage(AppDbContext db, Guid ticketItemId, InboundEmailMessage m, string body)
    {
        var msg = new TicketMessage
        {
            TicketMessageId = Guid.NewGuid(),
            TicketItemId = ticketItemId,
            Direction = MessageDirection.Inbound,
            From = m.From,
            To = m.To,
            Subject = m.Subject,
            Body = body,
            ExternalMessageId = m.ExternalId,
            InternetMessageId = string.IsNullOrWhiteSpace(m.MessageId) ? null : m.MessageId!.Trim(),
            InReplyToInternetMessageId = string.IsNullOrWhiteSpace(m.InReplyTo) ? null : m.InReplyTo!.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.TicketMessages.Add(msg);

        var preview = (body.Length > 200) ? body[..200] + "..." : body;

        db.TicketActivities.Add(new TicketActivity
        {
            TicketActivityId = Guid.NewGuid(),
            TicketItemId = ticketItemId,
            Type = "Email.Received",
            Note = $"From: {m.From}\nSubject: {m.Subject}\n\n{preview}",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = null
        });
    }

    private static async Task<Guid?> ResolveByThreadHeadersAsync(
        AppDbContext db,
        Guid customerId,
        InboundEmailMessage m,
        CancellationToken ct)
    {
        var ids = new List<string>(8);

        if (!string.IsNullOrWhiteSpace(m.InReplyTo))
            ids.Add(m.InReplyTo.Trim());

        if (m.References is not null)
        {
            ids.AddRange(m.References
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
        }

        ids = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return null;

        // Aynı müşteri kuralı: TicketMessage -> TicketItem join
        return await db.TicketMessages.AsNoTracking()
            .Where(tm => tm.InternetMessageId != null
                         && ids.Contains(tm.InternetMessageId)
                         && tm.TicketItem.CustomerId == customerId)
            .OrderByDescending(tm => tm.CreatedAt)
            .Select(tm => (Guid?)tm.TicketItemId)
            .FirstOrDefaultAsync(ct);
    }

    private static void ReopenIfNeeded(TicketItem t)
    {
        if (t.Status is TicketStatus.Closed or TicketStatus.Canceled or TicketStatus.WaitingCustomer)
            t.Status = TicketStatus.InProgress;
    }

    private static string? TryExtractTicketNo(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = TicketNoRegex.Match(text);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    private static string GenerateTicketNo()
    {
        var d = DateTime.UtcNow.ToString("yyyyMMdd");
        var s = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"EML-{d}-{s}";
    }
}
