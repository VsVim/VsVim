#if VS2017 || VS2019
using Microsoft.VisualStudio.Telemetry;
using Microsoft.VisualStudio.Text.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.EditorHost.Implementation.Misc
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

        void ILoggingServiceInternal.PostEvent(DataModelEventType eventType, string eventName, TelemetryResult result, params (string name, object property)[] namesAndProperties)
        {
        }

        void ILoggingServiceInternal.PostEvent(DataModelEventType eventType, string eventName, TelemetryResult result, IReadOnlyList<(string name, object property)> namesAndProperties)
        {
        }

        void ILoggingServiceInternal.PostFault(string eventName, string description, Exception exceptionObject, string additionalErrorInfo, bool? isIncludedInWatsonSample)
        {
        }
    }
}
#endif
