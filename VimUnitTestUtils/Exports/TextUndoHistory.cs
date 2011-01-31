using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest.Exports
{
    internal sealed class TextUndoHistory : ITextUndoHistory
    {
        private ITextUndoTransaction _currentTransaction;

        public bool CanRedo
        {
            get { return false; }
        }

        public bool CanUndo
        {
            get { return false; }
        }

        public ITextUndoTransaction CreateTransaction(string description)
        {
            if (_currentTransaction != null)
            {
                return new TextUndoTransaction(this, _currentTransaction);
            }
            else
            {
                _currentTransaction = new TextUndoTransaction(this);
                return _currentTransaction;
            }
        }

        public ITextUndoTransaction CurrentTransaction
        {
            get { return _currentTransaction; }
            internal set { _currentTransaction = value; }
        }

        public ITextUndoTransaction LastRedoTransaction
        {
            get { throw new NotImplementedException(); }
        }

        public ITextUndoTransaction LastUndoTransaction
        {
            get { throw new NotImplementedException(); }
        }

        public void Redo(int count)
        {
            throw new NotImplementedException();
        }

        public string RedoDescription
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<ITextUndoTransaction> RedoStack
        {
            get { throw new NotImplementedException(); }
        }

        public TextUndoHistoryState State
        {
            [DebuggerNonUserCode]
            get { return TextUndoHistoryState.Idle; }
        }

        public void Undo(int count)
        {
            throw new NotImplementedException();
        }

        public string UndoDescription
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<ITextUndoTransaction> UndoStack
        {
            get { throw new NotImplementedException(); }
        }

#pragma warning disable 67
        public event EventHandler<TextUndoRedoEventArgs> UndoRedoHappened;
        public event EventHandler<TextUndoTransactionCompletedEventArgs> UndoTransactionCompleted;
#pragma warning restore 67

        private PropertyCollection _properties = new PropertyCollection();

        public PropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
