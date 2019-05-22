using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Vim.Extensions;
using System.Windows.Threading;
using System.Diagnostics;

namespace Vim.UnitTest.Utilities
{
    public sealed class StaContext : IDisposable
    {
        /// <summary>
        /// A long timeout used to avoid hangs in tests, where a test failure manifests as an operation never occurring.
        /// </summary>
        public static readonly TimeSpan HangMitigatingTimeout = TimeSpan.FromMinutes(1);

        /// <summary>Gets a <see cref="StaContext"/> for the current AppDomain.</summary>
        public static StaContext Default { get; } = new StaContext();

        public Thread StaThread { get; }
        public Dispatcher Dispatcher { get; }
        public DispatcherSynchronizationContext DispatcherSynchronizationContext { get; }
        public bool IsRunningInThread => StaThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId;

        public StaContext()
        {
            using (var staThreadStartedEvent = new ManualResetEventSlim(initialState: false))
            {
                DispatcherSynchronizationContext syncContext = null;
                Dispatcher dispatcher = null;
                StaThread = new Thread((ThreadStart)(() =>
                {
                    // All WPF Tests need a DispatcherSynchronizationContext and we dont want to block pending keyboard
                    // or mouse input from the user. So use background priority which is a single level below user input.
                    syncContext = new DispatcherSynchronizationContext();
                    dispatcher = Dispatcher.CurrentDispatcher;

                    // xUnit creates its own synchronization context and wraps any existing context so that messages are
                    // still pumped as necessary. So we are safe setting it here, where we are not safe setting it in test.
                    SynchronizationContext.SetSynchronizationContext(syncContext);

                    staThreadStartedEvent.Set();

                    Dispatcher.Run();
                }));

                StaThread.Name = $"Sta Thread";
                StaThread.SetApartmentState(ApartmentState.STA);
                StaThread.Start();

                staThreadStartedEvent.Wait();
                Debug.Assert(syncContext != null);
                Debug.Assert(dispatcher != null);

                Dispatcher = dispatcher;
                DispatcherSynchronizationContext = syncContext;

                AppDomain.CurrentDomain.DomainUnload += delegate { this.Dispose(); };
            }

            // Work around the WeakEventTable Shutdown race conditions
            AppContext.SetSwitch("Switch.MS.Internal.DoNotInvokeInWeakEventTableShutdownListener", isEnabled: true);
        }

        /// <summary>
        /// Cleans up the scheduler by indicating that no more tasks will be queued.
        /// This method blocks until all threads successfully shutdown.
        /// </summary>
        public void Dispose()
        {
            if (StaThread.IsAlive)
            {
                Dispatcher.InvokeShutdown();
            }
        }

        void IDisposable.Dispose() => Dispose();
    }
}
