using Microsoft.AspNetCore.Mvc.Rendering;
using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Models.Tickets;

public sealed class TicketIndexVm
{
    public string? Q { get; set; }

    public TicketStatus? Status { get; set; }
    public TicketChannel? Channel { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool Today { get; set; }
    public bool AutoRefresh { get; set; } = true;

    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> ChannelOptions { get; set; } = new();

    public List<SelectListItem> AssigneeOptions { get; set; } = new();

    public List<TicketIndexRowVm> Items { get; set; } = new();
    public List<TicketIndexRowVm> OpenTickets { get; set; } = new();
    public List<TicketIndexRowVm> ClosedTickets { get; set; } = new();
    

}

public sealed class TicketIndexRowVm
{
    public Guid TicketItemId { get; set; }
    public string TicketNo { get; set; } = "";
    public string Subject { get; set; } = "";

    public TicketStatus StatusValue { get; set; }
    public TicketChannel ChannelValue { get; set; }

    public string? CustomerTitle { get; set; }
    public string? ProblemName { get; set; }

    public string? AssignedToUserName { get; set; }

    public DateTime CreatedAt { get; set; }
}
