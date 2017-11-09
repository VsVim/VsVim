using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vim.UnitTest.Utilities
{
    [Serializable]
    public sealed class WpfTestSharedData
    {
        internal static readonly WpfTestSharedData Instance = new WpfTestSharedData();

        /// <summary>
        /// The name of a <see cref="Semaphore"/> used to ensure that only a single
        /// <see cref="WpfFactAttributeAttribute"/>-attributed test runs at once. This requirement must be made because,
        /// currently, <see cref="WpfTestCase"/>'s logic sets various static state before a method runs. If two tests
        /// run interleaved on the same scheduler (i.e. if one yields with an await) then all bets are off.
        /// </summary>
        internal static readonly Guid TestSerializationGateName = Guid.NewGuid();

        /// <summary>
        /// Holds the last 10 test cases executed: more recent test cases will occur later in the 
        /// list. Useful for debugging deadlocks that occur because state leak between runs. 
        /// </summary>
        private readonly List<string> _recentTestCases = new List<string>();

        public Semaphore TestSerializationgate = new Semaphore(1, 1, TestSerializationGateName.ToString("N"));

        private WpfTestSharedData()
        {
            MonitorTestableSynchronizationContext();
        }

        public void ExecutingTest(ITestMethod testMethod)
        {
            var name = $"{testMethod.TestClass.Class.Name}::{testMethod.Method.Name}";
            lock (_recentTestCases)
            {
                _recentTestCases.Add(name);
            }
        }

        public void ExecutingTest(MethodInfo testMethod)
        {
            var name = $"{testMethod.DeclaringType.Name}::{testMethod.Name}";
            lock (_recentTestCases)
            {
                _recentTestCases.Add(name);
            }
        }

        private void MonitorTestableSynchronizationContext()
        {
            TestableSynchronizationContext.Created += (sender, e) =>
            {
                MonitorTestableSynchronizationContext(e.TestableSynchronizationContext);
            };
        }

        /// <summary>
        /// When a <see cref="TestableSynchronizationContext"/> instance is used in a <see cref="WpfFactAttribute"/>
        /// test it can cause a deadlock. This happens when there are posted actions that are not run and the test
        /// case is non-async. 
        /// 
        /// The xunit framework monitors all calls to the active <see cref="SynchronizationContext"/> and it will 
        /// wait on them to complete before finishing a test. Hence if anything is posted but not run the test will
        /// deadlock forever waiting for this to happen.
        /// 
        /// This code monitors the use of our custom <see cref="TestableSynchronizationContext"/> and attempts to 
        /// detect this situation and actively fail the test when it happens. The code is a hueristic and hence 
        /// imprecise. But is effective in finding these problmes.
        /// </summary>
        private void MonitorTestableSynchronizationContext(TestableSynchronizationContext testContext)
        {
            if (!StaTaskScheduler.DefaultSta.IsRunningInScheduler)
            {
                return;
            }

            // To cause the test to fail we need to post an action ot the AsyncTestContext. The xunit framework 
            // wraps such delegates in a try / catch and fails the test if any exception occurs. This is best
            // captured at the point a posted action occurs. 
            AsyncTestSyncContext asyncContext = null;
            testContext.PostedCallback += (sender, e) =>
            {
                if (SynchronizationContext.Current is AsyncTestSyncContext c)
                {
                    asyncContext = c;
                }
            };

            var startTime = DateTime.UtcNow;
            void checkForBad()
            {
                try
                {
                    var span = DateTime.UtcNow - startTime;
                    if (!testContext.IsDisposed)
                    {
                        if (testContext.PostedCallbackCount > 0 && span > TimeSpan.FromSeconds(30))
                        {
                            asyncContext?.Post(_ => throw new Exception("Unfulfilled TestableSynchronizationContext detected"), null);
                            testContext.RunAll();
                        }
                        else
                        {
                            var timer = new Task(() =>
                            {
                                Thread.Sleep(TimeSpan.FromSeconds(2));
                                queueCheckForBad();

                            });
                            timer.Start(TaskScheduler.Default);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Fail($"Exception monitoring {nameof(TestableSynchronizationContext)}: {ex.Message}");
                }
            }

            void queueCheckForBad()
            {
                var task = new Task((Action)checkForBad);
                task.Start(StaTaskScheduler.DefaultSta);
            }

            queueCheckForBad();
        }

    }
}
