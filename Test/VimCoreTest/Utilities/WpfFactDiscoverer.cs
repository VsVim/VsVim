using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Xunit.Extensions;
using Xunit.Abstractions;

namespace Vim.UnitTest.Utilities
{
    public class WpfFactDiscoverer : FactDiscoverer
    {
        private readonly IMessageSink _diagnosticMessageSink;
 
        /// <summary>
        /// The name of a <see cref="Semaphore"/> used to ensure that only a single
        /// <see cref="WpfFactAttributeAttribute"/>-attributed test runs at once. This requirement must be made because,
        /// currently, <see cref="WpfTestCase"/>'s logic sets various static state before a method runs. If two tests
        /// run interleaved on the same scheduler (i.e. if one yields with an await) then all bets are off.
        /// </summary>
        private readonly Guid _wpfTestSerializationGate = Guid.NewGuid();
 
        public WpfFactDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }
 
        protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
            => new WpfTestCase(_diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod, _wpfTestSerializationGate);
    }
}
