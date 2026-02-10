using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket.Application.Abstractions
{
    public interface ISetupState
    {
        bool IsConfigured { get; }
    }
}
