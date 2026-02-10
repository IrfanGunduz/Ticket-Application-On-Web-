using Ticket.Application.Abstractions;

namespace Ticket.Infrastructure.Email;

public sealed class NullEmailInboxReader : IEmailInboxReader
{
    public Task<IReadOnlyList<InboundEmailMessage>> FetchNewAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InboundEmailMessage>>(Array.Empty<InboundEmailMessage>());

    public Task AcknowledgeAsync(IEnumerable<string> externalIds, CancellationToken ct)
        => Task.CompletedTask;
}
