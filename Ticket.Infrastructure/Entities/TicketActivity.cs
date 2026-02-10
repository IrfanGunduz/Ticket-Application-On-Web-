using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Entities
{
    public class TicketActivity
    {
        public Guid TicketActivityId { get; set; }
        public Guid TicketItemId { get; set; }

        public string Type { get; set; } = null!; // "Assigned", "Closed", "NoteAdded" vb.
        public string Note { get; set; } = null!;

        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public TicketItem TicketItem { get; set; } = null!;
    }
}
