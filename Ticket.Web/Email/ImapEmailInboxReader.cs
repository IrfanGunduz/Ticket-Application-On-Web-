using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Persistence;

namespace Ticket.Web.Email;

public sealed class ImapEmailInboxReader : IEmailInboxReader
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;

    public ImapEmailInboxReader(AppDbContext db, IDataProtectionProvider dp)
    {
        _db = db;
        _protector = dp.CreateProtector("Ticket.EmailIngestSettings.v1");
    }

    public async Task<IReadOnlyList<InboundEmailMessage>> FetchNewAsync(CancellationToken ct)
    {
        var s = await _db.EmailIngestSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null || !s.Enabled) return Array.Empty<InboundEmailMessage>();

        if (string.IsNullOrWhiteSpace(s.ImapHost) || string.IsNullOrWhiteSpace(s.ImapUserName) || string.IsNullOrWhiteSpace(s.EncryptedImapPassword))
            return Array.Empty<InboundEmailMessage>();

        string password;
        try { password = _protector.Unprotect(s.EncryptedImapPassword); }
        catch { return Array.Empty<InboundEmailMessage>(); }

        using var client = new ImapClient();
        var ssl = s.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(s.ImapHost, s.ImapPort, ssl, ct);
        await client.AuthenticateAsync(s.ImapUserName, password, ct);

        var folder = await client.GetFolderAsync(s.Folder ?? "INBOX", ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);
        if (uids.Count == 0) return Array.Empty<InboundEmailMessage>();

        // Çok mail varsa son 25 tanesini al
        var take = uids.Count > 25 ? uids.Skip(uids.Count - 25).ToList() : uids.ToList();

        var target = NormalizeEmail(s.TargetAddress);

        var result = new List<InboundEmailMessage>(take.Count);

        foreach (var uid in take)
        {
            var msg = await folder.GetMessageAsync(uid, ct);

            // To/Cc filter
            if (!string.IsNullOrWhiteSpace(target))
            {
                var toOk = AnyRecipientMatches(msg.To, target) || AnyRecipientMatches(msg.Cc, target);
                if (!toOk) continue;
            }

            var from = (msg.From.Mailboxes.FirstOrDefault()?.Address) ?? "";
            var to = string.Join(";", msg.To.Mailboxes.Select(m => m.Address));
            var subject = msg.Subject;
            var body = ExtractBody(msg);

            // ExternalId: UIDVALIDITY + UID
            var externalId = $"imap:{folder.UidValidity}:{uid.Id}";

            static string? Norm(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            var messageId = Norm(msg.MessageId);
            var inReplyTo = Norm(msg.InReplyTo);

            IReadOnlyList<string>? refs = msg.References?
                .Select(Norm)
                .Where(x => x is not null)
                .Select(x => x!) // ✅ artık string
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();       // ✅ List<string> => IReadOnlyList<string>



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

    public async Task AcknowledgeAsync(IEnumerable<string> externalIds, CancellationToken ct)
    {
        var s = await _db.EmailIngestSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null || !s.Enabled || !s.MarkAsRead) return;

        if (string.IsNullOrWhiteSpace(s.ImapHost) || string.IsNullOrWhiteSpace(s.ImapUserName) || string.IsNullOrWhiteSpace(s.EncryptedImapPassword))
            return;

        string password;
        try { password = _protector.Unprotect(s.EncryptedImapPassword); }
        catch { return; }

        // externalId: imap:<uidvalidity>:<uid>
        var ids = externalIds
            .Select(ParseUid)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();

        if (ids.Count == 0) return;

        using var client = new ImapClient();
        var ssl = s.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(s.ImapHost, s.ImapPort, ssl, ct);
        await client.AuthenticateAsync(s.ImapUserName, password, ct);

        var folder = await client.GetFolderAsync(s.Folder ?? "INBOX", ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);

        foreach (var uid in ids)
            await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);

        await client.DisconnectAsync(true, ct);
    }

    private static UniqueId? ParseUid(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return null;
        if (!externalId.StartsWith("imap:", StringComparison.OrdinalIgnoreCase)) return null;

        // imap:uidvalidity:uid
        var parts = externalId.Split(':', 3);
        if (parts.Length != 3) return null;

        if (!uint.TryParse(parts[2], out var uid)) return null;
        return new UniqueId(uid);
    }

    private static bool AnyRecipientMatches(InternetAddressList list, string targetLower)
        => list.Mailboxes.Any(m => NormalizeEmail(m.Address) == targetLower);

    private static string NormalizeEmail(string? s)
        => (s ?? "").Trim().ToLowerInvariant();

    private static string ExtractBody(MimeMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.TextBody)) return msg.TextBody;
        if (!string.IsNullOrWhiteSpace(msg.HtmlBody))
        {
            // basit html strip 
            var t = System.Text.RegularExpressions.Regex.Replace(msg.HtmlBody, "<.*?>", " ");
            return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        }
        return "";
    }
}
