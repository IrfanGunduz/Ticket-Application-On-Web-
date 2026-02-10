using Microsoft.AspNetCore.DataProtection;
using Ticket.Application.Abstractions;

namespace Ticket.Web.Setup
{
    public sealed class DefaultConnectionStringProvider : IConnectionStringProvider
    {
        private const string ENV_KEY = "TICKET_CONNECTION";
        private readonly ISetupConfigStore _store;
        private readonly IDataProtector _protector;

        public DefaultConnectionStringProvider(ISetupConfigStore store, IDataProtectionProvider dp)
        {
            _store = store;
            _protector = dp.CreateProtector("Ticket.Setup.ConnectionString.v1");
        }

        //public string? GetConnectionStringOrNull()
        //{
        //    // Process -> User -> Machine (IIS senaryolarında Machine çoğu zaman daha stabil)
        //    var env =
        //        Environment.GetEnvironmentVariable(ENV_KEY, EnvironmentVariableTarget.Process) ??
        //        Environment.GetEnvironmentVariable(ENV_KEY, EnvironmentVariableTarget.User) ??
        //        Environment.GetEnvironmentVariable(ENV_KEY, EnvironmentVariableTarget.Machine);

        //    if (!string.IsNullOrWhiteSpace(env))
        //        return env;

        //    var cfg = _store.TryLoad();
        //    if (cfg is null) return null;

        //    try
        //    {
        //        return _protector.Unprotect(cfg.EncryptedConnectionString);
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        public string? GetConnectionStringOrNull()
        {
            // 1) Önce setup.json (Model A)
            var cfg = _store.TryLoad();
            if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.EncryptedConnectionString))
            {
                try { return _protector.Unprotect(cfg.EncryptedConnectionString); }
                catch { return null; }
            }

            // 2) Runtime'da ENV fallback kapalı
            return null;
        }

    }
}
