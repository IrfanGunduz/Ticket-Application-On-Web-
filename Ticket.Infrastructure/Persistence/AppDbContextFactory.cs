using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ticket.Infrastructure.Persistence
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var cs = Environment.GetEnvironmentVariable("TICKET_CONNECTION");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("TICKET_CONNECTION env var ayarlı değil.");

            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(cs, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

            return new AppDbContext(opts);
        }
    }
}
