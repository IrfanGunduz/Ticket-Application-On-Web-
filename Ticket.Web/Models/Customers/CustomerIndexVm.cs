namespace Ticket.Web.Models.Customers;

public sealed class CustomerIndexVm
{
    public string? Q { get; set; }
    public bool ShowInactive { get; set; }

    public List<CustomerRowVm> Items { get; set; } = new();
}

public sealed class CustomerRowVm
{
    public Guid CustomerId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsActive { get; set; }

}
