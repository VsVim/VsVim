using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest.Utils
{
    [Export(typeof(ITextUndoHistoryRegistry))]
    public sealed class TextUndoHistoryRegistry : ITextUndoHistoryRegistry
    {
        private readonly Dictionary<object, ITextUndoHistory> _map = new Dictionary<object, ITextUndoHistory>();

        public void AttachHistory(object context, ITextUndoHistory history)
        {
            _map.Add(context, history);
        }

        public ITextUndoHistory GetHistory(object context)
        {
            ITextUndoHistory history;
            _map.TryGetValue(context, out history);
            return history;
        }

        public ITextUndoHistory RegisterHistory(object context)
        {
            return null;
        }

        public void RemoveHistory(ITextUndoHistory history)
        {
            throw new NotImplementedException();
        }

        public bool TryGetHistory(object context, out ITextUndoHistory history)
        {
            return _map.TryGetValue(context, out history);
        }
    }
}
