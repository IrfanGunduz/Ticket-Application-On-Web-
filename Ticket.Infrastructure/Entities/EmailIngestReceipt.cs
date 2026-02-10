namespace Ticket.Infrastructure.Entities;

public sealed class EmailIngestReceipt
{
    public long Id { get; set; }

    // POP3 UIDL ya da IMAP externalId (imap:uidValidity:uid)
    public string ExternalMessageId { get; set; } = null!;

    public string Status { get; set; } = null!; // örn: "Skipped.NotAllowlisted"
    public string? From { get; set; }
    public string? Subject { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? InternetMessageId { get; set; }
    public string? InReplyToInternetMessageId { get; set; } // istersen (opsiyonel)

}
