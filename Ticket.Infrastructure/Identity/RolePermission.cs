using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Identity
{
    public class RolePermission
    {
        public Guid RoleId { get; set; }  
        public string PermissionKey { get; set; } = "";

        public AppRole? Role { get; set; }
    }
}
