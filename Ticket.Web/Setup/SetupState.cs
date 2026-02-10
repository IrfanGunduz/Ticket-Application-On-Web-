using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Ticket.Application.Abstractions;

namespace Ticket.Web.Setup
{
    /// <summary>
    /// "Setup configured" demek sadece setup.json var demek değildir.
    /// Kurulum tamam sayılabilmesi için DB'ye erişilebilmesi de gerekir.
    /// </summary>
    public sealed class SetupState : ISetupState
    {
        private readonly ISetupConfigStore _store;
        private readonly IConnectionStringProvider _csProvider;
        private readonly ILogger<SetupState> _logger;

        private readonly object _gate = new();
        private DateTime _lastProbeUtc = DateTime.MinValue;
        private bool _lastResult;

        public SetupState(
            ISetupConfigStore store,
            IConnectionStringProvider csProvider,
            ILogger<SetupState> logger)
        {
            _store = store;
            _csProvider = csProvider;
            _logger = logger;
        }

        public bool IsConfigured
        {
            get
            {
                if (!_store.HasConfig())
                    return false;

                // Sadece "false" sonucu cache'le (DB silinince anında fark etsin)
                lock (_gate)
                {
                    if (_lastResult == false &&
                        DateTime.UtcNow - _lastProbeUtc < TimeSpan.FromSeconds(5))
                        return false;
                }

                var cs = _csProvider.GetConnectionStringOrNull();
                if (string.IsNullOrWhiteSpace(cs))
                    return false;

                var ok = ProbeDatabase(cs);

                lock (_gate)
                {
                    _lastProbeUtc = DateTime.UtcNow;
                    _lastResult = ok;
                }

                return ok;
            }
        }


        private bool ProbeDatabase(string cs)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(cs)
                {
                    ConnectTimeout = 3
                };

                using var con = new SqlConnection(b.ConnectionString);
                con.Open();

                using var cmd = new SqlCommand("SELECT 1;", con);
                cmd.ExecuteScalar();

                return true;
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex,
                    "Connection string var ama DB erişimi başarısız (SqlException Number={Number}). Setup moduna düşülecek.",
                    ex.Number);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Connection string var ama DB probe başarısız. Setup moduna düşülecek.");

                return false;
            }
        }

    }
}