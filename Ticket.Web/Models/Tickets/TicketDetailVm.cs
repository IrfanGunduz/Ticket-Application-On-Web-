using Microsoft.AspNetCore.Mvc.Rendering;
using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Models.Tickets;

public sealed class TicketDetailVm
{
    public Guid TicketItemId { get; set; }
    public string TicketNo { get; set; } = "";
    public string Subject { get; set; } = "";

    
    public TicketStatus StatusValue { get; set; }
    public TicketChannel ChannelValue { get; set; }

    public string? CustomerTitle { get; set; }
    public string? ProblemName { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ProblemId { get; set; }

    public List<SelectListItem> CustomerOptions { get; set; } = new();
    public List<SelectListItem> ProblemOptions { get; set; } = new();


    public List<SelectListItem> Assignees { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();

    public List<TicketActivityVm> Activities { get; set; } = new();
    public List<TicketMessageVm> Messages { get; set; } = new();
    public List<string> CustomerContactEmails { get; set; } = new();


    
    public string? NewNote { get; set; }
}

public sealed class TicketMessageVm
{
    public DateTime CreatedAt { get; set; }
    public string Direction { get; set; } = ""; 
    public string DirectionTr =>
       Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase) ? "Gelen" :
       Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase) ? "Giden" :
       Direction;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
}

public sealed class TicketActivityVm
{
    public DateTime CreatedAt { get; set; }
    public string Type { get; set; } = "";
    public string Note { get; set; } = "";
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

}
