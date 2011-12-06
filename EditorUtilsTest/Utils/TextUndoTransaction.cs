using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils.UnitTest.Utils
{
    internal sealed class TextUndoTransaction : ITextUndoTransaction
    {
        private readonly TextUndoHistory _textUndoHistory;
        private readonly List<ITextUndoPrimitive> _primitiveList = new List<ITextUndoPrimitive>();

        public TextUndoTransaction(TextUndoHistory textUndoHistory, string description)
        {
            _textUndoHistory = textUndoHistory;
            Description = description;
        }

        public void AddUndo(ITextUndoPrimitive undo)
        {
            _primitiveList.Add(undo);
        }

        /// <summary>
        /// Visual Studio implementation throw so duplicate here
        /// </summary>
        public bool CanRedo
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Visual Studio implementation throw so duplicate here
        /// </summary>
        public bool CanUndo
        {
            get { throw new NotSupportedException(); }
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
            get { throw new NotSupportedException(); }
        }

        public IList<ITextUndoPrimitive> UndoPrimitives
        {
            get { return _primitiveList; }
        }

        public UndoTransactionState State
        {
            get { throw new NotSupportedException(); }
        }

        public string Description { get; set; }

        public void Cancel()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: false);
        }

        public void Complete()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: true);
        }

        public void Do()
        {
            for (var i = 0; i < _primitiveList.Count; i++)
            {
                _primitiveList[i].Do();
            }
        }

        public void Undo()
        {
            for (var i = _primitiveList.Count - 1; i >= 0; i--)
            {
                _primitiveList[i].Undo();
            }
        }

        public void Dispose()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: false);
        }
    }
}
