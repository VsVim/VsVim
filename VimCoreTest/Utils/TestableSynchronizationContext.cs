using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace VimCoreTest.Utils
{
    internal sealed class TestableSynchronizationContext : SynchronizationContext
    {
        private List<Action> _list = new List<Action>();

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
    }
}
