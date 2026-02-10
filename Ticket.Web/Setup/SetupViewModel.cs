using System.ComponentModel.DataAnnotations;
namespace Ticket.Web.Setup
{
    public class SetupViewModel
    {
        [Required]
        public string Server { get; set; } = "localhost";

        [Required]
        public string Database { get; set; } = "TicketDatabase";

        
        public bool UseSqlAuth { get; set; } = false;

        public string? UserName { get; set; }
        public string? Password { get; set; }

        
        public bool Encrypt { get; set; } = true;
        public bool TrustServerCertificate { get; set; } = true;

        //public bool RememberMe { get; set; } = true;
    }
}
