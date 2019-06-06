using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Vim.EditorHost;
using Vim.UnitTest.Utilities;

namespace Vim.UnitTest
{
    public sealed class TestableSynchronizationContext : SynchronizationContext, IDisposable
    {
        private SynchronizationContext _oldSynchronizationContext;
        private bool _isSet;
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly int _mainThreadId;

        /// <summary>
        /// Is execution currently on thi main thread for this context? 
        /// </summary>
        public bool InMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public bool IsEmpty => PostedCallbackCount == 0;
        public bool IsDisposed { get; private set; }
        public int PostedCallbackCount
        {
            get
            {
                CheckMainThread();
                lock (_queue)
                {
                    return _queue.Count;
                }
            }
        }

        public TestableSynchronizationContext(Thread mainThread = null)
        {
            _mainThreadId = mainThread?.ManagedThreadId ?? Thread.CurrentThread.ManagedThreadId;
            _oldSynchronizationContext = SynchronizationContext.Current;
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
            if (d == null)
            {
                throw new ArgumentException(nameof(d));
            }

            CheckDisposed();

            lock (_queue)
            {
                _queue.Enqueue(() => d(state));
            }
        }

        public void RunOne()
        {
            CheckDisposed();
            CheckMainThread();

            Action action;
            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    throw new InvalidOperationException();
                }

                action = _queue.Dequeue();
            }

            action();
        }

        public void RunAll()
        {
            CheckDisposed();
            CheckMainThread();

            while (!IsEmpty)
            {
                RunOne();
            }
        }

        private void Uninstall()
        {
            if (!_isSet)
            {
                throw new InvalidOperationException("Not installed");
            }

            if (PostedCallbackCount > 0)
            {
                throw new InvalidOperationException($"Cannot uninstall because {nameof(PostedCallbackCount)} is {PostedCallbackCount}");
            }

            CheckDisposed();
            SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
            _oldSynchronizationContext = null;
            _isSet = false;
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
