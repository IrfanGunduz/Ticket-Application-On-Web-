namespace Ticket.Web.Models.Problems;

public sealed class ProblemIndexVm
{
    public string? Q { get; set; }
    public bool ShowInactive { get; set; }
    public List<ProblemRowVm> Items { get; set; } = new();
}

public sealed class ProblemRowVm
{
    public Guid ProblemId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsActive { get; set; }
    public int UsedTicketCount { get; set; }
}
