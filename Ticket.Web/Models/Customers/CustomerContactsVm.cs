namespace Ticket.Web.Models.Customers;

public sealed class CustomerContactsVm
{
    public Guid CustomerId { get; set; }
    public string CustomerTitle { get; set; } = "";

    public List<CustomerContactRowVm> Items { get; set; } = new();

    // Add form
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}

public sealed class CustomerContactRowVm
{
    public Guid CustomerContactId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}
