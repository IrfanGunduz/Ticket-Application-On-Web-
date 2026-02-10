using System;

namespace Ticket.Infrastructure.Identity
{
    public class UserPermission
    {
        public Guid UserId { get; set; }
        public string PermissionKey { get; set; } = "";

        public AppUser? User { get; set; }
    }
}
