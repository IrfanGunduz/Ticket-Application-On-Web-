namespace Ticket.Infrastructure.Entities;

public sealed class EmailIngestSettings
{
    public int Id { get; set; }

    public bool Enabled { get; set; } = false;
    public int PollSeconds { get; set; } = 30;

    // Inbox'a gelen maillerin hangi adrese geldiyse işleneceği (To/Cc filter)
    public string? TargetAddress { get; set; }

    public EmailIngestProtocol Protocol { get; set; } = EmailIngestProtocol.Imap;

    // IMAP
    public string? ImapHost { get; set; }
    public int ImapPort { get; set; } = 993;
    public bool ImapUseSsl { get; set; } = true;
    public string? ImapUserName { get; set; }

    // Güvenlik: DB'de şifreli sakla
    public string? EncryptedImapPassword { get; set; }

    public bool MarkAsRead { get; set; } = true;
    public string Folder { get; set; } = "INBOX";

    // POP3
    public string? Pop3Host { get; set; }
    public int Pop3Port { get; set; } = 995;
    public bool Pop3UseSsl { get; set; } = true;
    public string? Pop3UserName { get; set; }

    // Güvenlik: DB'de şifreli sakla
    public string? EncryptedPop3Password { get; set; }
}
