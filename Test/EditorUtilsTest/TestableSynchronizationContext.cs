using System;
using System.Collections.Generic;
using System.Threading;

namespace EditorUtils.UnitTest
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

        public void RunOne()
        {
            if (_list.Count == 0)
            {
                return;
            }

            try
            {
                _list[0]();
                _list.RemoveAt(0);
            }
            catch
            {

            }
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
