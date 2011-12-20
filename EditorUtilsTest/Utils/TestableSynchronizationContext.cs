using System;
using System.Collections.Generic;
using System.Threading;

namespace EditorUtils.UnitTest.Utils
{
    public sealed class TestableSynchronizationContext : SynchronizationContext
    {
        private SynchronizationContext _oldSynchronizationContext;
        private List<Action> _list = new List<Action>();
        public bool IsEmpty
        {
            get { return 0 == _list.Count; }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _list.Add(() => d(state));
        }

        public void RunAll()
        {
            foreach (var cur in _list)
            {
                cur();
            }
            _list.Clear();
        }

        public void Install()
        {
            _oldSynchronizationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);
        }

        public void Uninstall()
        {
            if (_oldSynchronizationContext != null)
            {
                SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
                _oldSynchronizationContext = null;
            }
        }
    }
}
