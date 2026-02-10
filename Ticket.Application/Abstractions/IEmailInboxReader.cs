namespace Ticket.Application.Abstractions;

public interface IEmailInboxReader
{
    /// Yeni gelen e-postaları çeker (unread/new mantığı implementasyona bağlıdır).
    Task<IReadOnlyList<InboundEmailMessage>> FetchNewAsync(CancellationToken ct);

    /// İşlenen mesajları (örn. read/archived) olarak işaretlemek için. Null impl’de no-op.
    Task AcknowledgeAsync(IEnumerable<string> externalIds, CancellationToken ct);
}

public sealed record InboundEmailMessage(
    string ExternalId,
    string? MessageId,
    string? InReplyTo,
    IReadOnlyList<string>? References,
    string? From,
    string? To,
    string? Subject,
    string Body,
    DateTime ReceivedAtUtc
);

