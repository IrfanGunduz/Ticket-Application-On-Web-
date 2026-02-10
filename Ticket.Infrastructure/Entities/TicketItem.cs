using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Entities
{
    public enum TicketStatus { New, InProgress, WaitingCustomer, Closed, Canceled }
    public enum TicketChannel { Manual, Email, Phone }
    public class TicketItem
    {
        public Guid TicketItemId { get; set; }
        public string TicketNo { get; set; } = null!;

        public Guid? CustomerId { get; set; }
        public Guid? ProblemId { get; set; }

        public string Subject { get; set; } = null!;
        public TicketStatus Status { get; set; }
        public TicketChannel Channel { get; set; }

        public Guid? AssignedToUserId { get; set; }      
        public Guid? CreatedByUserId { get; set; }       
        public DateTime CreatedAt { get; set; }

        public Customer? Customer { get; set; }
        public Problem? Problem { get; set; }

        public List<TicketActivity> Activities { get; set; } = new();
        public List<TicketMessage> Messages { get; set; } = new();
    }
}
