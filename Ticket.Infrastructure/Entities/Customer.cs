using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Entities
{
    public class Customer
    {
        public Guid CustomerId { get; set; }
        public string Code { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;


        public List<CustomerContact> Contacts { get; set; } = new();
    }
}
