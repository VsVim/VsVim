﻿using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Operations;

namespace Vim.EditorHost.Implementation.BasicUndo
{
    /// <summary>
    /// This class is intended to be a very simple ITextUndoHistoryRegistry implementation for hosts that
    /// don't have a built-in undo mechanism
    /// </summary>
    [Export(typeof(ITextUndoHistoryRegistry))]
    [Export(typeof(IBasicUndoHistoryRegistry))]
    internal sealed class BasicTextUndoHistoryRegistry : ITextUndoHistoryRegistry, IBasicUndoHistoryRegistry
    {
        private readonly ConditionalWeakTable<object, IBasicUndoHistory> _map = new ConditionalWeakTable<object, IBasicUndoHistory>();

        [ImportingConstructor]
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
            _map.TryGetValue(context, out IBasicUndoHistory history);
            return history;
        }

        ITextUndoHistory ITextUndoHistoryRegistry.RegisterHistory(object context)
        {
            if (!_map.TryGetValue(context, out IBasicUndoHistory history))
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
            if (TryGetHistory(context, out IBasicUndoHistory basicUndoHistory))
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
