using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio
{
    public interface ITelemetry
    {
        void WriteEvent(string eventName);
    }

    public interface ITelemetryProvider
    {
        ITelemetry GetOrCreate(IVimApplicationSettings vimApplicationSettings, _DTE dte);
    }
}
