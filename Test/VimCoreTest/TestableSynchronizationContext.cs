using System;
using System.Collections.Generic;
using System.Threading;
using EditorUtils;

namespace Vim.UnitTest
{
    public sealed class TestableSynchronizationContext : SynchronizationContext
    {
        private SynchronizationContext _oldSynchronizationContext;
        private bool _isSet;
        private readonly List<Action> _list = new List<Action>();

        public bool IsEmpty
        {
            get { return 0 == _list.Count; }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                throw new ArgumentException(nameof(d));
            }

            _list.Add(() => d(state));
        }

        public void RunOne()
        {
            if (_list.Count == 0)
            {
                throw new InvalidOperationException();
            }

            _list[0]();
            _list.RemoveAt(0);
        }

        public void RunAll()
        {
            while (_list.Count > 0)
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
            SynchronizationContext.SetSynchronizationContext(this);
            _isSet = true;
        }

        public void Uninstall()
        {
            if (!_isSet)
            {
                throw new InvalidOperationException();
            }

            SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
            _oldSynchronizationContext = null;
            _isSet = false;
        }
    }
}
