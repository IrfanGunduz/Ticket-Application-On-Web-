namespace Ticket.Web.Models.Admin;

public sealed class PermissionRowVm
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";

    
    public bool Assigned { get; set; }

    
    public bool Denied { get; set; }

    
    public bool Locked { get; set; }
}
