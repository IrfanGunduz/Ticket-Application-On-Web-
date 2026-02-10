using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Persistence;

namespace Ticket.Web.Email;

public sealed class EmailInboxReaderRouter : IEmailInboxReader
{
    private readonly AppDbContext _db;
    private readonly ImapEmailInboxReader _imap;
    private readonly Pop3EmailInboxReader _pop3;

    public EmailInboxReaderRouter(AppDbContext db, ImapEmailInboxReader imap, Pop3EmailInboxReader pop3)
    {
        _db = db;
        _imap = imap;
        _pop3 = pop3;
    }

    private async Task<IEmailInboxReader> PickAsync(CancellationToken ct)
    {
        var s = await _db.EmailIngestSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return (s?.Protocol ?? EmailIngestProtocol.Imap) == EmailIngestProtocol.Pop3 ? _pop3 : _imap;
    }

    public async Task<IReadOnlyList<InboundEmailMessage>> FetchNewAsync(CancellationToken ct)
        => await (await PickAsync(ct)).FetchNewAsync(ct);

    public async Task AcknowledgeAsync(IEnumerable<string> externalIds, CancellationToken ct)
        => await (await PickAsync(ct)).AcknowledgeAsync(externalIds, ct);
}
