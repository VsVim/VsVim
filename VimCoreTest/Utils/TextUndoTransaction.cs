using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCore.Test.Utils
{
    internal sealed class TextUndoTransaction : ITextUndoTransaction
    {
        private readonly TextUndoHistory _textUndoHistory;

        public TextUndoTransaction(TextUndoHistory textUndoHistory)
        {
            _textUndoHistory = textUndoHistory;
        }

        public void AddUndo(ITextUndoPrimitive undo)
        {
        }

        public bool CanRedo
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanUndo
        {
            get { throw new NotImplementedException(); }
        }

        public void Cancel()
        {
        }

        public void Complete()
        {
        }

        public string Description
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Do()
        {
            throw new NotImplementedException();
        }

        public ITextUndoHistory History
        {
            get { throw new NotImplementedException(); }
        }

        public IMergeTextUndoTransactionPolicy MergePolicy
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public ITextUndoTransaction Parent
        {
            get { throw new NotImplementedException(); }
        }

        public UndoTransactionState State
        {
            get { throw new NotImplementedException(); }
        }

        public void Undo()
        {
            throw new NotImplementedException();
        }

        public IList<ITextUndoPrimitive> UndoPrimitives
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            _textUndoHistory.CurrentTransaction = null;
        }
    }
}
