using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Identity
{
    public class Permission
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";     // "Tickets.Create"
        public string Name { get; set; } = "";    // "Ticket oluştur"
    }
}
