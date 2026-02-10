namespace Ticket.Web.Models.Admin;

public sealed class AdminUsersIndexVm
{
    public List<AdminUserRowVm> Users { get; set; } = new();
}

public sealed class AdminUserRowVm
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string Roles { get; set; } = "";
}
