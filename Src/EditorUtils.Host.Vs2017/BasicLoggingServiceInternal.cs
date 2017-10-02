using Microsoft.VisualStudio.Telemetry;
using Microsoft.VisualStudio.Text.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorUtils.Vs2017
{
    [Export(typeof(ILoggingServiceInternal))]
    internal sealed class BasicLoggingServiceInternal : ILoggingServiceInternal
    {
        void ILoggingServiceInternal.AdjustCounter(string key, string name, int delta)
        {

        }

        void ILoggingServiceInternal.PostCounters()
        {

        }

        void ILoggingServiceInternal.PostEvent(string key, params object[] namesAndProperties)
        {

        }

        void ILoggingServiceInternal.PostEvent(string key, IReadOnlyList<object> namesAndProperties)
        {

        }
    }
}
