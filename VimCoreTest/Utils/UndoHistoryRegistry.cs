using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.UI.Undo;
using System.ComponentModel.Composition;

namespace VimCoreTest.Utils
{
    [Export(typeof(IUndoHistoryRegistry))]
    public sealed class UndoHistoryRegistry : IUndoHistoryRegistry
    {
        private Dictionary<object, TestOnlyUndoHistory> m_map = new Dictionary<object, TestOnlyUndoHistory>();

        public void AttachHistory(object context, UndoHistory history, bool keepAlive)
        {
            
        }

        public void AttachHistory(object context, UndoHistory history)
        {
        }

        public LinkedUndoTransaction CreateLinkedUndoTransaction(string description)
        {
            return null;
        }

        public void DetachHistory(object context)
        {
        }

        public UndoHistory GetHistory(object context)
        {
            return null;
        }

        public long GetTotalHistorySize()
        {
            return 0;
        }

        public UndoProperty GetUndoProperty(string name)
        {
            return null;
        }

        public UndoTransactionMarker GetUndoTransactionMarker(string name)
        {
            throw new NotImplementedException();
        }

        public UndoHistory GlobalHistory
        {
            get { return null; }
        }

        public bool HasOpenLinkedUndoTransaction
        {
            get { return false; }
        }

        public IEnumerable<UndoHistory> Histories
        {
            get { return Enumerable.Empty<UndoHistory>(); }
        }

        public void LimitHistoryDepth(int depth)
        {
        }

        public void LimitHistorySize(long size)
        {
        }

        public void LimitTotalHistorySize(long size)
        {
        }

        public UndoHistory RegisterHistory(object context, bool keepAlive)
        {
            TestOnlyUndoHistory history = null;
            if (!m_map.TryGetValue(context, out history))
            {
                history = new TestOnlyUndoHistory();
                m_map[context] = history;
            }

            return history;
        }

        public UndoHistory RegisterHistory(object context)
        {
            return RegisterHistory(context, false);
        }

        public void RemoveHistory(UndoHistory history)
        {
        }

        public bool TryGetHistory(object context, out UndoHistory history)
        {
            history = null;
            return false;
        }

        public IEnumerable<UndoProperty> UndoProperties
        {
            get { return Enumerable.Empty<UndoProperty>(); }
        }

        public IEnumerable<UndoTransactionMarker> UndoTransactionMarkers
        {
            get { return Enumerable.Empty<UndoTransactionMarker>(); }
        }
    }
}
