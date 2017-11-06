using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vim.UnitTest.Utilities
{
    public sealed class WpfTestCase : XunitTestCase
    {
        private Guid _semaphoreName;
        private Semaphore _wpfTestSerializationGate;
        private WpfTestSharedData _sharedData;
 
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public WpfTestCase()
        {
        }
 
        public WpfTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, Guid wpfTestSerializationGate, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, defaultMethodDisplay, testMethod, testMethodArguments)
        {
            _semaphoreName = wpfTestSerializationGate;
            _wpfTestSerializationGate = new Semaphore(1, 1, _semaphoreName.ToString("N"));
            _sharedData = WpfTestSharedData.Instance;
        }
 
        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            _sharedData.ExecutingTest(TestMethod);
            var sta = StaTaskScheduler.DefaultSta;
            var task = Task.Factory.StartNew(async () =>
            {
                Debug.Assert(sta.Threads.Count == 1);
                Debug.Assert(sta.Threads[0] == Thread.CurrentThread);
 
                using (await _wpfTestSerializationGate.DisposableWaitAsync(CancellationToken.None))
                {
                    try
                    {
                        // All WPF Tests need a DispatcherSynchronizationContext and we dont want to block pending keyboard
                        // or mouse input from the user. So use background priority which is a single level below user input.
                        var dispatcherSynchronizationContext = new DispatcherSynchronizationContext();
 
                        // xUnit creates its own synchronization context and wraps any existing context so that messages are
                        // still pumped as necessary. So we are safe setting it here, where we are not safe setting it in test.
                        SynchronizationContext.SetSynchronizationContext(dispatcherSynchronizationContext);
 
                        // Just call back into the normal xUnit dispatch process now that we are on an STA Thread with no synchronization context.
                        var baseTask = base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                        do
                        {
                            var delay = Task.Delay(TimeSpan.FromMilliseconds(10), cancellationTokenSource.Token);
                            var completed = await Task.WhenAny(baseTask, delay).ConfigureAwait(false);
                            if (completed == baseTask)
                            {
                                return await baseTask.ConfigureAwait(false);
                            }
                        }
                        while (true);
                    }
                    finally
                    {
                        // Cleanup the synchronization context even if the test is failing exceptionally
                        SynchronizationContext.SetSynchronizationContext(null);
                    }
                }
            }, cancellationTokenSource.Token, TaskCreationOptions.None, sta);
 
            return task.Unwrap();
        }
 
        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);
            data.AddValue(nameof(_semaphoreName), _semaphoreName.ToString("N"));
        }
 
        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);
            _semaphoreName = Guid.ParseExact(data.GetValue<string>(nameof(_semaphoreName)), "N");
            _wpfTestSerializationGate = new Semaphore(1, 1, _semaphoreName.ToString("N"));
            _sharedData = WpfTestSharedData.Instance;
        }
    }
}
