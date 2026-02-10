using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Models.Home;

public sealed class HomeIndexVm
{
    public int OpenCount { get; set; }
    public int UnassignedCount { get; set; }
    public int TodayCreatedCount { get; set; }

    public List<HomeLatestTicketVm> LatestTickets { get; set; } = new();
}

public sealed class HomeLatestTicketVm
{
    public Guid TicketItemId { get; set; }
    public string TicketNo { get; set; } = "";
    public string? Subject { get; set; }
    public TicketStatus StatusValue { get; set; }
    public TicketChannel ChannelValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
