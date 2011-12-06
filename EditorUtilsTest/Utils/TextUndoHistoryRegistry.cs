using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils.UnitTest.Utils
{
    [Export(typeof(ITextUndoHistoryRegistry))]
    public sealed class TextUndoHistoryRegistry : ITextUndoHistoryRegistry
    {
        private readonly ConditionalWeakTable<object, ITextUndoHistory> _map = new ConditionalWeakTable<object, ITextUndoHistory>();

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
            ITextUndoHistory history;
            if (!_map.TryGetValue(context, out history))
            {
                history = new TextUndoHistory();
                _map.Add(context, history);
            }
            return history;
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
