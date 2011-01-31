using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Operations;

namespace Vim.UnitTest.Exports
{
    internal sealed class TextUndoTransaction : ITextUndoTransaction
    {
        private sealed class Policy : IMergeTextUndoTransactionPolicy
        {
            public bool CanMerge(ITextUndoTransaction newerTransaction, ITextUndoTransaction olderTransaction)
            {
                return false;
            }

            public void PerformTransactionMerge(ITextUndoTransaction existingTransaction, ITextUndoTransaction newTransaction)
            {
                throw new NotImplementedException();
            }

            public bool TestCompatiblePolicy(IMergeTextUndoTransactionPolicy other)
            {
                return false;
            }
        }

        private readonly ITextUndoTransaction _parent;
        private readonly TextUndoHistory _textUndoHistory;
        private readonly List<ITextUndoPrimitive> _primitiveList = new List<ITextUndoPrimitive>();
        private UndoTransactionState _state;

        public TextUndoTransaction(TextUndoHistory textUndoHistory) : this(textUndoHistory, null)
        {
        }

        public TextUndoTransaction(TextUndoHistory textUndoHistory, ITextUndoTransaction parent)
        {
            _textUndoHistory = textUndoHistory;
            _state = UndoTransactionState.Open;
            _parent = parent;
            MergePolicy = new Policy();
        }

        public void AddUndo(ITextUndoPrimitive undo)
        {
            _primitiveList.Add(undo);
        }

        public bool CanRedo
        {
            get { return false; }
        }

        public bool CanUndo
        {
            get { return false; }
        }

        public void Cancel()
        {
            _state = UndoTransactionState.Canceled;
            MaybeClearCurrent();
        }

        public void Complete()
        {
            _state = UndoTransactionState.Completed;
            MaybeClearCurrent();
        }

        public string Description
        {
            get;
            set;
        }

        public void Do()
        {
            throw new NotImplementedException();
        }

        public ITextUndoHistory History
        {
            get { return _textUndoHistory; }
        }

        public IMergeTextUndoTransactionPolicy MergePolicy
        {
            get;
            set;
        }

        public ITextUndoTransaction Parent
        {
            get { return _parent; }
        }

        public UndoTransactionState State
        {
            get { return _state; }
        }

        public void Undo()
        {
            throw new NotImplementedException();
        }

        public IList<ITextUndoPrimitive> UndoPrimitives
        {
            get { return _primitiveList; }
        }

        public void Dispose()
        {
            MaybeClearCurrent();
        }

        private void MaybeClearCurrent()
        {
            if (_textUndoHistory.CurrentTransaction == this)
            {
                _textUndoHistory.CurrentTransaction = null;
            }
        }
    }
}
