using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;

namespace EditorUtils.Implementation.BasicUndo
{
    /// <summary>
    /// Provides a very simple ITextUndoHistory implementation.  Sufficient for us to test
    /// simple undo scenarios inside the unit tests
    /// </summary>
    internal sealed class BasicUndoHistory : IBasicUndoHistory
    {
        private readonly object _context;
        private readonly Stack<BasicUndoTransaction> _openTransactionStack = new Stack<BasicUndoTransaction>();
        private readonly Stack<ITextUndoTransaction> _undoStack = new Stack<ITextUndoTransaction>();
        private readonly Stack<ITextUndoTransaction> _redoStack = new Stack<ITextUndoTransaction>();
        private readonly PropertyCollection _properties = new PropertyCollection();
        private TextUndoHistoryState _state = TextUndoHistoryState.Idle;
        private event EventHandler<TextUndoRedoEventArgs> _undoRedoHappened;
        private event EventHandler<TextUndoTransactionCompletedEventArgs> _undoTransactionCompleted;

        internal ITextUndoTransaction CurrentTransaction
        {
            get { return _openTransactionStack.Count > 0 ? _openTransactionStack.Peek() : null; }
        }

        internal Stack<ITextUndoTransaction> UndoStack
        {
            get { return _undoStack; }
        }

        internal Stack<ITextUndoTransaction> RedoStack
        {
            get { return _redoStack; }
        }

        internal object Context
        {
            get { return _context; }
        }

        internal BasicUndoHistory(object context)
        {
            _context = context;
        }

        internal ITextUndoTransaction CreateTransaction(string description)
        {
            _openTransactionStack.Push(new BasicUndoTransaction(this, description));
            return _openTransactionStack.Peek();
        }

        internal void Redo(int count)
        {
            try
            {
                count = Math.Min(_redoStack.Count, count);
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

        internal void Undo(int count)
        {
            try
            {
                count = Math.Min(_undoStack.Count, count);
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

        internal void OnTransactionClosed(BasicUndoTransaction transaction, bool didComplete)
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
                var list = _undoTransactionCompleted;
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

        internal void Clear()
        {
            if (_state != TextUndoHistoryState.Idle || CurrentTransaction != null)
            {
                throw new InvalidOperationException("Can't clear with an open transaction or in undo / redo");
            }

            _undoStack.Clear();
            _redoStack.Clear();

            // The IEditorOperations AddAfterTextBufferChangePrimitive and AddBeforeTextBufferChangePrimitive
            // implementations store an ITextView in the Property of the associated ITextUndoHistory.  It's
            // necessary to keep this value present so long as the primitives are in the undo / redo stack
            // as their implementation depends on it.  Once the stack is cleared we can safely remove 
            // the value.
            //
            // This is in fact necessary for sane testing.  Without this removal it's impossible to have 
            // an ITextView disconnect and be collected from it's underlying ITextBuffer.  The ITextUndoHistory
            // is associated with an ITextBuffer and through it's undo stack will keep the ITextView alive
            // indefinitely
            _properties.RemoveProperty(typeof(ITextView));
        }

        private void RaiseUndoRedoHappened()
        {
            var list = _undoRedoHappened;
            if (list != null)
            {
                // Note: Passing null here as this is what Visual Studio does
                list(this, new TextUndoRedoEventArgs(_state, null));
            }
        }

        #region ITextUndoHistory

        /// <summary>
        /// Return 'true' here instead of actually looking at the redo stack count because
        /// this is the behavior of the standard Visual Studio undo manager
        /// </summary>
        bool ITextUndoHistory.CanRedo
        {
            get { return true; }
        }

        /// <summary>
        /// Return 'true' here instead of actually looking at the redo stack count because
        /// this is the behavior of the standard Visual Studio undo manager
        /// </summary>
        bool ITextUndoHistory.CanUndo
        {
            get { return true; }
        }

        ITextUndoTransaction ITextUndoHistory.CreateTransaction(string description)
        {
            return CreateTransaction(description);
        }

        ITextUndoTransaction ITextUndoHistory.CurrentTransaction
        {
            get { return CurrentTransaction; }
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        ITextUndoTransaction ITextUndoHistory.LastRedoTransaction
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        ITextUndoTransaction ITextUndoHistory.LastUndoTransaction
        {
            get { throw new NotSupportedException(); }
        }

        void ITextUndoHistory.Redo(int count)
        {
            Redo(count);
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        string ITextUndoHistory.RedoDescription
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        IEnumerable<ITextUndoTransaction> ITextUndoHistory.RedoStack
        {
            get { throw new NotSupportedException(); }
        }

        TextUndoHistoryState ITextUndoHistory.State
        {
            [DebuggerNonUserCode]
            get { return _state; }
        }

        void ITextUndoHistory.Undo(int count)
        {
            Undo(count);
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        string ITextUndoHistory.UndoDescription
        {
            get { throw new NotSupportedException(); }
        }

        event EventHandler<TextUndoRedoEventArgs> ITextUndoHistory.UndoRedoHappened
        {
            add { _undoRedoHappened += value; }
            remove { _undoRedoHappened -= value; }
        }

        /// <summary>
        /// Easy to implement but not supported by Visual Studio
        /// </summary>
        IEnumerable<ITextUndoTransaction> ITextUndoHistory.UndoStack
        {
            get { throw new NotSupportedException(); }
        }

        event EventHandler<TextUndoTransactionCompletedEventArgs> ITextUndoHistory.UndoTransactionCompleted
        {
            add { _undoTransactionCompleted += value; }
            remove { _undoTransactionCompleted -= value; }
        }

        PropertyCollection IPropertyOwner.Properties
        {
            get { return _properties; }
        }

        #endregion

        #region IBasicUndoHistory

        void IBasicUndoHistory.Clear()
        {
            Clear();
        }

        #endregion
    }
}
