using System.ComponentModel.DataAnnotations;
using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Models.Admin;

public sealed class AdminEmailSettingsVm : IValidatableObject
{
    public bool Enabled { get; set; }

    [Range(5, 3600)]
    public int PollSeconds { get; set; } = 30;

    [EmailAddress]
    public string? TargetAddress { get; set; }

    public EmailIngestProtocol Protocol { get; set; } = EmailIngestProtocol.Imap;

    // IMAP (Required YOK)
    public string? ImapHost { get; set; }
    [Range(1, 65535)]
    public int ImapPort { get; set; } = 993;
    public bool ImapUseSsl { get; set; } = true;
    public string? ImapUserName { get; set; }
    public string? ImapPassword { get; set; }

    public bool MarkAsRead { get; set; } = true;
    public string Folder { get; set; } = "INBOX";

    // POP3 (Required YOK)
    public string? Pop3Host { get; set; }
    [Range(1, 65535)]
    public int Pop3Port { get; set; } = 995;
    public bool Pop3UseSsl { get; set; } = true;
    public string? Pop3UserName { get; set; }
    public string? Pop3Password { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Protocol == EmailIngestProtocol.Imap)
        {
            if (string.IsNullOrWhiteSpace(ImapHost))
                yield return new ValidationResult("IMAP Host zorunludur.", new[] { nameof(ImapHost) });

            if (string.IsNullOrWhiteSpace(ImapUserName))
                yield return new ValidationResult("IMAP Username zorunludur.", new[] { nameof(ImapUserName) });
        }
        else // Pop3
        {
            if (string.IsNullOrWhiteSpace(Pop3Host))
                yield return new ValidationResult("POP3 Host zorunludur.", new[] { nameof(Pop3Host) });

            if (string.IsNullOrWhiteSpace(Pop3UserName))
                yield return new ValidationResult("POP3 Username zorunludur.", new[] { nameof(Pop3UserName) });
        }
    }
}
