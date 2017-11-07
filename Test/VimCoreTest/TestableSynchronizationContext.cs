using System;
using System.Collections.Generic;
using System.Threading;
using Vim.EditorHost;
using Vim.UnitTest.Utilities;

namespace Vim.UnitTest
{
    public sealed class TestableSynchronizationContext : SynchronizationContext, IDisposable
    {
        private SynchronizationContext _oldSynchronizationContext;
        private bool _isSet;
        private bool _isTracked;
        private readonly Queue<Action> _queue = new Queue<Action>();

        public bool IsEmpty => 0 == _queue.Count;
        public int PostedActionCount => _queue.Count;

        public TestableSynchronizationContext(bool install = true)
        {
            if (install)
            {
                Install();
            }
        }

        public void Dispose()
        {
            if (_isSet)
            {
                Uninstall();
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                throw new ArgumentException(nameof(d));
            }

            if (!_isTracked)
            {
                WpfTestSharedData.Instance.PostingAction(this);
                _isTracked = true;
            }

            _queue.Enqueue(() => d(state));
        }

        public void RunOne()
        {
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException();
            }

            var action = _queue.Dequeue();
            action();
        }

        public void RunAll()
        {
            while (_queue.Count > 0)
            {
                RunOne();
            }
        }

        public void Install()
        {
            if (_isSet)
            {
                throw new InvalidOperationException();
            }

            _oldSynchronizationContext = SynchronizationContext.Current;
            if (_oldSynchronizationContext != null && _oldSynchronizationContext.GetType() == typeof(TestableSynchronizationContext))
            {
                throw new InvalidOperationException();
            }

            SynchronizationContext.SetSynchronizationContext(this);
            _isSet = true;
        }

        public void Uninstall()
        {
            if (!_isSet)
            {
                throw new InvalidOperationException();
            }

            if (PostedActionCount > 0)
            {
                throw new InvalidOperationException();
            }

            SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
            _oldSynchronizationContext = null;
            _isSet = false;
        }
    }
}
