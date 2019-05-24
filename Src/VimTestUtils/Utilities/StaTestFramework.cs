using Xunit.Sdk;
using Xunit.Abstractions;
using System.Windows.Data;
using System;
using System.Linq;

namespace Vim.UnitTest.Utilities
{
    public sealed class StaTestFramework : XunitTestFramework, IDisposable
    {
        public static bool IsCreated { get; private set; }

        public StaTestFramework() : this(null)
        {
        }

        public StaTestFramework(IMessageSink messageSink) : base(messageSink)
        {
            IsCreated = true;
        }

        void IDisposable.Dispose()
        {
            StaContext.Default.Dispose();
            base.Dispose();
        }
    }
}
