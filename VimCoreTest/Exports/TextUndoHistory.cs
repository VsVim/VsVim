using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using NUnit.Framework;

namespace VimCore.Test.Exports
{
    internal sealed class TextUndoHistory : ITextUndoHistory
    {
        private ITextUndoTransaction _currentTransaction = null;

        public bool CanRedo
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanUndo
        {
            get { throw new NotImplementedException(); }
        }

        public ITextUndoTransaction CreateTransaction(string description)
        {
            _currentTransaction = new TextUndoTransaction(this);
            return _currentTransaction;
        }

        public ITextUndoTransaction CurrentTransaction
        {
            get
            {
                Assert.NotNull(_currentTransaction);
                return _currentTransaction;
            }
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
            get { throw new NotImplementedException(); }
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
