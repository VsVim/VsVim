using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using Vim.EditorHost;
using Vim.UnitTest.Utilities;

namespace Vim.UnitTest
{
    /// <summary>
    /// The intent of this class is to reduce xunit deadlocks and increase async / dispatch control inside 
    /// of our unit tests. 
    ///
    /// The xunit framework monitors all calls to the active <see cref="SynchronizationContext"/> and it will 
    /// wait on them to complete before finishing a test. Hence if anything is posted but not run the test will
    /// deadlock forever waiting for this to happen.
    ///
    /// The majority of tests are synchronous in this code base. Hence if there is any unexpected post operations
    /// then they will deadlock and due to the async nature of xunit be hard to diagnose. This class rejects all
    /// dispatch ops by default and requires explicit opt in for it to work.
    /// </summary>
    public sealed class VimSynchronizationContext : SynchronizationContext, IDisposable
    {
        private DispatcherSynchronizationContext _oldSynchronizationContext;
        private bool _isSet;
        private readonly int _mainThreadId;

        /// <summary>
        /// Is execution currently on the main thread for this context? 
        /// </summary>
        public bool InMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public bool IsDisposed { get; private set; }

        public bool IsDispatchEnabled { get; set; }

        public Dispatcher Dispatcher { get; }

        public VimSynchronizationContext(bool isDispatchEnabled = false, Thread mainThread = null)
        {
            _mainThreadId = mainThread?.ManagedThreadId ?? Thread.CurrentThread.ManagedThreadId;
            _oldSynchronizationContext = (DispatcherSynchronizationContext)SynchronizationContext.Current;
            Dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(this);
            _isSet = true;
        }

        public void Dispose()
        {
            if (_isSet)
            {
                Uninstall();
            }

            IsDisposed = true;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            CheckDispatchEnabled();
            _oldSynchronizationContext.Post(d, state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            CheckDispatchEnabled();
            _oldSynchronizationContext.Send(d, state);
        }

        public void DoEvents()
        {
            CheckDispatchEnabled();
            CheckMainThread();
            Dispatcher.DoEvents();
        }

        private void Uninstall()
        {
            if (!_isSet)
            {
                throw new InvalidOperationException("Not installed");
            }

            CheckDisposed();
            SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
            _oldSynchronizationContext = null;
            _isSet = false;
        }

        private void CheckDispatchEnabled()
        {
            if (!IsDispatchEnabled)
            {
                throw new InvalidOperationException("Dispatch is disabled");
            }
        }

        private void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException("Object is disposed");
            }
        }

        private void CheckMainThread()
        {
            if (!InMainThread)
            {
                throw new InvalidOperationException("This call can only be made from the main thread");
            }
        }
    }
}
