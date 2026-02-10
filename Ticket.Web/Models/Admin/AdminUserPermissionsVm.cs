namespace Ticket.Web.Models.Admin;

public sealed class AdminUserPermissionsVm
{
    public Guid SelectedUserId { get; set; }
    public string SelectedUserName { get; set; } = "";

    public List<UserOptionVm> Users { get; set; } = new();
    public List<PermissionRowVm> Permissions { get; set; } = new();
}

public sealed class UserOptionVm
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
}


