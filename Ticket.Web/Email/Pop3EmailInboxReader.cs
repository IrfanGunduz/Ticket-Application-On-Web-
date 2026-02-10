using MailKit.Net.Pop3;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;

namespace Ticket.Web.Email;

public sealed class Pop3EmailInboxReader : IEmailInboxReader
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;

    public Pop3EmailInboxReader(AppDbContext db, IDataProtectionProvider dp)
    {
        _db = db;
        _protector = dp.CreateProtector("Ticket.EmailIngestSettings.v1");
    }

    public async Task<IReadOnlyList<InboundEmailMessage>> FetchNewAsync(CancellationToken ct)
    {
        var s = await _db.EmailIngestSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null || !s.Enabled) return Array.Empty<InboundEmailMessage>();
        if (s.Protocol != EmailIngestProtocol.Pop3) return Array.Empty<InboundEmailMessage>();

        if (string.IsNullOrWhiteSpace(s.Pop3Host) ||
            string.IsNullOrWhiteSpace(s.Pop3UserName) ||
            string.IsNullOrWhiteSpace(s.EncryptedPop3Password))
            return Array.Empty<InboundEmailMessage>();

        string password;
        try { password = _protector.Unprotect(s.EncryptedPop3Password); }
        catch { return Array.Empty<InboundEmailMessage>(); }

        using var client = new Pop3Client();

        var ssl = s.Pop3UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(s.Pop3Host, s.Pop3Port, ssl, ct);
        await client.AuthenticateAsync(s.Pop3UserName, password, ct);

        var count = client.Count;
        if (count <= 0)
        {
            await client.DisconnectAsync(true, ct);
            return Array.Empty<InboundEmailMessage>();
        }

        // UIDL listesi (Count ile aynı uzunlukta)
        var uidls = await client.GetMessageUidsAsync(ct);

        // Son 25 maili tara (POP3 index 0-based)
        var takeCount = Math.Min(25, count);
        var start = Math.Max(0, count - takeCount);
        var indices = Enumerable.Range(start, takeCount).ToList();

        // ExternalId = pop3:{UIDL}
        var candidateExtIds = indices.Select(i => $"pop3:{uidls[i]}").ToList();

        // Body indirmeden dedupe: ExternalId var mı?
        var existingMsgExternalIds = await _db.TicketMessages.AsNoTracking()
            .Where(m => m.ExternalMessageId != null && candidateExtIds.Contains(m.ExternalMessageId))
            .Select(m => m.ExternalMessageId!)
            .ToListAsync(ct);

        var existingReceiptExternalIds = await _db.EmailIngestReceipts.AsNoTracking()
            .Where(r => candidateExtIds.Contains(r.ExternalMessageId))
            .Select(r => r.ExternalMessageId)
            .ToListAsync(ct);

        var existingExternalSet = existingMsgExternalIds
            .Concat(existingReceiptExternalIds)
            .ToHashSet(StringComparer.Ordinal);

        var target = NormalizeEmail(s.TargetAddress);

        var result = new List<InboundEmailMessage>(takeCount);

        foreach (var i in indices)
        {
            var externalId = $"pop3:{uidls[i]}";
            if (existingExternalSet.Contains(externalId)) continue;

            var msg = await client.GetMessageAsync(i, ct);

            // To/Cc filter
            if (!string.IsNullOrWhiteSpace(target))
            {
                var toOk = AnyRecipientMatches(msg.To, target) || AnyRecipientMatches(msg.Cc, target);
                if (!toOk) continue;
            }

            static string? Norm(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();


            var from = msg.From.Mailboxes.FirstOrDefault()?.Address ?? "";
            var to = string.Join(";", msg.To.Mailboxes.Select(m => m.Address));
            var subject = msg.Subject;
            var body = ExtractBody(msg);

            var messageId = Norm(msg.MessageId);
            var inReplyTo = Norm(msg.InReplyTo);

            IReadOnlyList<string>? refs = msg.References?
                .Select(Norm)
                .Where(x => x is not null)
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(new InboundEmailMessage(
                ExternalId: externalId,
                MessageId: messageId,
                InReplyTo: inReplyTo,
                References: refs,
                From: from,
                To: to,
                Subject: subject,
                Body: body,
                ReceivedAtUtc: (msg.Date.UtcDateTime == default ? DateTime.UtcNow : msg.Date.UtcDateTime)
            ));
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }

    public Task AcknowledgeAsync(IEnumerable<string> externalIds, CancellationToken ct)
    {
        // POP3: mail Outlook’ta kalsın -> DELE yok. No-op.
        // (MarkAsRead POP3’te çalışmaz; Seen flag IMAP’e özgü.)
        return Task.CompletedTask;
    }

    private static string NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? "" : email.Trim().ToLowerInvariant();

    private static bool AnyRecipientMatches(InternetAddressList list, string targetLower)
        => list.Mailboxes.Any(m => NormalizeEmail(m.Address) == targetLower);

    private static string ExtractBody(MimeMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.TextBody)) return msg.TextBody;

        if (!string.IsNullOrWhiteSpace(msg.HtmlBody))
        {
            // IMAP’te yaptığın gibi basit html strip
            var t = System.Text.RegularExpressions.Regex.Replace(msg.HtmlBody, "<.*?>", " ");
            return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        }

        return "";
    }
}
