namespace Ticket.Web.Setup
{
    public interface ISetupConfigStore
    {
        bool HasConfig();
        SetupConfig? TryLoad();
        void Save(SetupConfig config);
    }
}
