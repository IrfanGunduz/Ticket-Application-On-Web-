using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Infrastructure.Entities
{

    public enum MessageDirection { Inbound, Outbound, InternalNote }
    public class TicketMessage
    {
        public Guid TicketMessageId { get; set; }
        public Guid TicketItemId { get; set; }

        public MessageDirection Direction { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Subject { get; set; }
        public string Body { get; set; } = null!;

        // Email'den gelirse aynı maili 2 kez ticket'lamamak için:
        public string? ExternalMessageId { get; set; }

        public DateTime CreatedAt { get; set; }

        public TicketItem TicketItem { get; set; } = null!;
        // RFC Message-Id (threading için)
        public string? InternetMessageId { get; set; }
        public string? InReplyToInternetMessageId { get; set; }


    }
}
