using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vim.UnitTest.Utilities
{
    /// <summary>
    /// This type is actually responsible for spinning up the STA context to run all of the
    /// tests. 
    /// 
    /// Overriding the <see cref="XunitTestInvoker"/> to setup the STA context is not the correct 
    /// approach. That type begins constructing types before RunAsync and hence ctors end up 
    /// running on the current thread vs. the STA ones. Just completely wrapping the invocation
    /// here is the best case. 
    /// </summary>
    public sealed class WpfTestRunner : XunitTestRunner
    {
        /// <summary>
        /// A long timeout used to avoid hangs in tests, where a test failure manifests as an operation never occurring.
        /// </summary>
        private static readonly TimeSpan HangMitigatingTimeout = TimeSpan.FromMinutes(1);

        public WpfTestSharedData SharedData { get; }

        public WpfTestRunner(
            WpfTestSharedData sharedData,
            ITest test,
            IMessageBus messageBus,
            Type testClass,
            object[] constructorArguments,
            MethodInfo testMethod,
            object[] testMethodArguments,
            string skipReason,
            IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
        {
            SharedData = sharedData;
        }

        protected override Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
        {
            SharedData.ExecutingTest(TestMethod);
            Debug.Assert(StaTestFramework.IsCreated);

            var taskScheduler = new SynchronizationContextTaskScheduler(StaContext.Default.DispatcherSynchronizationContext);
            return Task.Factory.StartNew(async () =>
            {
                Debug.Assert(SynchronizationContext.Current is DispatcherSynchronizationContext);

                using (await SharedData.TestSerializationGate.DisposableWaitAsync(CancellationToken.None))
                {
                    // Just call back into the normal xUnit dispatch process now that we are on an STA Thread with no synchronization context.
                    var invoker = new XunitTestInvoker(Test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, BeforeAfterAttributes, aggregator, CancellationTokenSource);
                    return await invoker.RunAsync();
                }
            }, CancellationTokenSource.Token, TaskCreationOptions.None, taskScheduler).Unwrap();
        }
    }
}
