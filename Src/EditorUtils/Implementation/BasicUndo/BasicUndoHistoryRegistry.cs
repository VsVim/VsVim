using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils.Implementation.BasicUndo
{
    /// <summary>
    /// This class is intended to be a very simple ITextUndoHistoryRegistry implementation for hosts that
    /// don't have a built-in undo mechanism
    /// </summary>
    internal sealed class BasicTextUndoHistoryRegistry : ITextUndoHistoryRegistry, IBasicUndoHistoryRegistry
    {
        private readonly ConditionalWeakTable<object, IBasicUndoHistory> _map = new ConditionalWeakTable<object, IBasicUndoHistory>();

        internal BasicTextUndoHistoryRegistry()
        {

        }

        private bool TryGetHistory(object context, out IBasicUndoHistory basicUndoHistory)
        {
            return _map.TryGetValue(context, out basicUndoHistory);
        }

        #region ITextUndoHistoryRegistry

        /// <summary>
        /// Easy to implement but the Visual Studio implementation throws a NotSupportedException
        /// </summary>
        void ITextUndoHistoryRegistry.AttachHistory(object context, ITextUndoHistory history)
        {
            throw new NotSupportedException();
        }

        ITextUndoHistory ITextUndoHistoryRegistry.GetHistory(object context)
        {
            IBasicUndoHistory history;
            _map.TryGetValue(context, out history);
            return history;
        }

        ITextUndoHistory ITextUndoHistoryRegistry.RegisterHistory(object context)
        {
            IBasicUndoHistory history;
            if (!_map.TryGetValue(context, out history))
            {
                history = new BasicUndoHistory(context);
                _map.Add(context, history);
            }
            return history;
        }

        void ITextUndoHistoryRegistry.RemoveHistory(ITextUndoHistory history)
        {
            var basicUndoHistory = history as BasicUndoHistory;
            if (basicUndoHistory != null)
            {
                _map.Remove(basicUndoHistory.Context);
                basicUndoHistory.Clear();
            }
        }

        bool ITextUndoHistoryRegistry.TryGetHistory(object context, out ITextUndoHistory history)
        {
            IBasicUndoHistory basicUndoHistory;
            if (TryGetHistory(context, out basicUndoHistory))
            {
                history = basicUndoHistory;
                return true;
            }

            history = null;
            return false;
        }

        #endregion

        #region IBasciUndoHistoryRegistry

        ITextUndoHistoryRegistry IBasicUndoHistoryRegistry.TextUndoHistoryRegistry
        {
            get { return this; }
        }

        bool IBasicUndoHistoryRegistry.TryGetBasicUndoHistory(object context, out IBasicUndoHistory basicUndoHistory)
        {
            return TryGetHistory(context, out basicUndoHistory);
        }

        #endregion
    }
}
