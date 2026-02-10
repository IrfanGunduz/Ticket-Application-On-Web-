namespace Ticket.Infrastructure.Entities
{
    public class CustomerContact
    {
        public Guid CustomerContactId { get; set; }
        public Guid CustomerId { get; set; }

        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }

        
        public bool IsActive { get; set; } = true;

        
        public bool AllowEmailIngest { get; set; } = true;

        public Customer Customer { get; set; } = null!;
    }
}
