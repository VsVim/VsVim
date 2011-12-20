using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils.UnitTest.Utils
{
    /// <summary>
    /// Provides a very simple ITextUndoHistory implementation.  Sufficient for us to test
    /// simple undo scenarios inside the unit tests
    /// </summary>
    internal sealed class TextUndoHistory : ITextUndoHistory
    {
        private TextUndoHistoryState _state = TextUndoHistoryState.Idle;
        private readonly Stack<TextUndoTransaction> _openTransactionStack = new Stack<TextUndoTransaction>();
        private readonly Stack<ITextUndoTransaction> _undoStack = new Stack<ITextUndoTransaction>();
        private readonly Stack<ITextUndoTransaction> _redoStack = new Stack<ITextUndoTransaction>();
        private readonly PropertyCollection _properties = new PropertyCollection();

        /// <summary>
        /// Return 'true' here instead of actually looking at the redo stack count because
        /// this is the behavior of the standard Visual Studio undo manager
        /// </summary>
        public bool CanRedo
        {
            get { return true; }
        }

        /// <summary>
        /// Return 'true' here instead of actually looking at the redo stack count because
        /// this is the behavior of the standard Visual Studio undo manager
        /// </summary>
        public bool CanUndo
        {
            get { return true; }
        }

        public string UndoDescription
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Another case where we could easily implement but Visual Studio does not
        /// </summary>
        public IEnumerable<ITextUndoTransaction> UndoStack
        {
            get { throw new NotSupportedException(); }
        }

        public PropertyCollection Properties
        {
            get { return _properties; }
        }

        public ITextUndoTransaction CurrentTransaction
        {
            get { return _openTransactionStack.Count > 0 ? _openTransactionStack.Peek() : null; }
        }

        public ITextUndoTransaction LastRedoTransaction
        {
            get { throw new NotSupportedException(); }
        }

        public ITextUndoTransaction LastUndoTransaction
        {
            get { throw new NotSupportedException(); }
        }

        public string RedoDescription
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Another case where we could easily implement but Visual Studio does not
        /// </summary>
        public IEnumerable<ITextUndoTransaction> RedoStack
        {
            get { throw new NotSupportedException(); }
        }

        public TextUndoHistoryState State
        {
            [DebuggerNonUserCode]
            get { return _state; }
        }

        public event EventHandler<TextUndoRedoEventArgs> UndoRedoHappened;

        public event EventHandler<TextUndoTransactionCompletedEventArgs> UndoTransactionCompleted;

        public ITextUndoTransaction CreateTransaction(string description)
        {
            _openTransactionStack.Push(new TextUndoTransaction(this, description));
            return _openTransactionStack.Peek();
        }

        public void Redo(int count)
        {
            try
            {
                _state = TextUndoHistoryState.Redoing;
                for (var i = 0; i < count; i++)
                {
                    var current = _redoStack.Peek();
                    current.Do();
                    _redoStack.Pop();
                    _undoStack.Push(current);
                }

                RaiseUndoRedoHappened();
            }
            finally
            {
                _state = TextUndoHistoryState.Idle;
            }
        }

        public void Undo(int count)
        {
            try
            {
                _state = TextUndoHistoryState.Undoing;
                for (var i = 0; i < count; i++)
                {
                    var current = _undoStack.Peek();
                    current.Undo();
                    _undoStack.Pop();
                    _redoStack.Push(current);
                }

                RaiseUndoRedoHappened();
            }
            finally
            {
                _state = TextUndoHistoryState.Idle;
            }
        }

        internal void OnTransactionClosed(TextUndoTransaction transaction, bool didComplete)
        {
            if (_openTransactionStack.Count == 0 || transaction != _openTransactionStack.Peek())
            {
                // Happens in dispose after complete / cancel
                return;
            }

            _openTransactionStack.Pop();
            if (!didComplete)
            {
                return;
            }

            if (_openTransactionStack.Count == 0)
            {
                _undoStack.Push(transaction);
                var list = UndoTransactionCompleted;
                if (list != null)
                {
                    list(this, new TextUndoTransactionCompletedEventArgs(null, TextUndoTransactionCompletionResult.TransactionAdded));
                }
            }
            else
            {
                foreach (var cur in transaction.UndoPrimitives)
                {
                    _openTransactionStack.Peek().UndoPrimitives.Add(cur);
                }
            }
        }


        private void RaiseUndoRedoHappened()
        {
            var list = UndoRedoHappened;
            if (list != null)
            {
                // Note: Passing null here as this is what Visual Studio does
                list(this, new TextUndoRedoEventArgs(_state, null));
            }
        }

    }
}
