using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public static readonly WpfTestSharedData Instance = new WpfTestSharedData();

        /// <summary>
        /// Holds the last 10 test cases executed: more recent test cases will occur later in the 
        /// list. Useful for debugging deadlocks that occur because state leak between runs. 
        /// </summary>
        private readonly List<string> _recentTestCases = new List<string>();

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

        public void PostingAction(TestableSynchronizationContext testContext)
        {
            if (!StaTaskScheduler.DefaultSta.IsRunningInScheduler)
            {
                return;
            }

            var asyncContext = SynchronizationContext.Current as AsyncTestSyncContext;
            if (asyncContext == null)
            {
                return;
            }

            var startTime = DateTime.UtcNow;
            void checkForBad()
            {
                try
                {
                    var span = DateTime.UtcNow - startTime;
                    if (testContext.PostedActionCount > 0)
                    {
                        if (span > TimeSpan.FromSeconds(30))
                        {
                            asyncContext.Post(_ => throw new Exception("Unfulfilled TestableSynchronizationContext detected"), null);
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

        private void MonitorTestableSynchronizationContext()
        {
            (AsyncTestSyncContext asyncContext, TestableSynchronizationContext testableSyncContext) getCurrentContext()
            {
                switch (SynchronizationContext.Current)
                {
                    case TestableSynchronizationContext testableContext: return (null, testableContext);
                    case AsyncTestSyncContext asyncContext:
                        {
                            var fieldInfo = asyncContext.GetType().GetField(
                                "innerContext",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var field = fieldInfo.GetValue(asyncContext);
                            return (asyncContext, field as TestableSynchronizationContext);
                        }
                    default: return (null, null);
                }
            }


        }
    }
}
