namespace Ticket.Infrastructure.Identity;

public sealed class UserPermissionGrant
{
    public Guid UserId { get; set; }
    public string PermissionKey { get; set; } = "";
    public AppUser? User { get; set; }
}
