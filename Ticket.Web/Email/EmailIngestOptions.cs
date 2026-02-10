namespace Ticket.Web.Email;

public sealed class EmailIngestOptions
{
    public bool Enabled { get; set; } = false;
    public int PollSeconds { get; set; } = 30;
}
