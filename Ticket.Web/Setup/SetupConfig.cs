namespace Ticket.Web.Setup
{
    public sealed class SetupConfig
    {
        public string EncryptedConnectionString { get; set; } = null!;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
