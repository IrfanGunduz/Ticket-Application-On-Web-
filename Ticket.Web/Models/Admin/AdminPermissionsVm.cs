namespace Ticket.Web.Models.Admin;

public sealed class AdminPermissionsVm
{
    public Guid SelectedRoleId { get; set; }
    public bool IsRoleLocked { get; set; }
    public string? LockReason { get; set; }

    public string SelectedRoleName { get; set; } = "";

    public List<RoleOptionVm> Roles { get; set; } = new();
    public List<PermissionRowVm> Permissions { get; set; } = new();
}

public sealed class RoleOptionVm
{
    public Guid RoleId { get; set; }
    public string Name { get; set; } = "";
}


