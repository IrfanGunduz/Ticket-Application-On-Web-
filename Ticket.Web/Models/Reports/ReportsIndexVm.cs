namespace Ticket.Web.Models.Reports;

public sealed class ReportsIndexVm
{
    public List<TopCustomerRow> TopCustomers { get; set; } = new();
    public List<TopProblemRow> TopProblems { get; set; } = new();
    public List<MonthlyRow> Monthly { get; set; } = new();
}

public sealed class TopCustomerRow
{
    public Guid CustomerId { get; set; }
    public string CustomerTitle { get; set; } = "";
    public int TicketCount { get; set; }
}

public sealed class TopProblemRow
{
    public Guid ProblemId { get; set; }
    public string ProblemName { get; set; } = "";
    public string Department { get; set; } = "";
    public int TicketCount { get; set; }
}

public sealed class MonthlyRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TicketCount { get; set; }
}
