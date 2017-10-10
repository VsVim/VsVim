using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils.Implementation.BasicUndo
{
    internal sealed class BasicUndoTransaction : ITextUndoTransaction
    {
        private readonly BasicUndoHistory _textUndoHistory;
        private readonly List<ITextUndoPrimitive> _primitiveList = new List<ITextUndoPrimitive>();

        internal string Description
        {
            get;
            set;
        }

        internal List<ITextUndoPrimitive> UndoPrimitives
        {
            get { return _primitiveList; }
        }

        internal BasicUndoTransaction(BasicUndoHistory textUndoHistory, string description)
        {
            _textUndoHistory = textUndoHistory;
            Description = description;
        }

        #region ITextUndoTransaction

        void ITextUndoTransaction.AddUndo(ITextUndoPrimitive undo)
        {
            _primitiveList.Add(undo);
        }

        /// <summary>
        /// Visual Studio implementation throw so duplicate here
        /// </summary>
        bool ITextUndoTransaction.CanRedo
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Visual Studio implementation throw so duplicate here
        /// </summary>
        bool ITextUndoTransaction.CanUndo
        {
            get { throw new NotSupportedException(); }
        }

        ITextUndoHistory ITextUndoTransaction.History
        {
            get { return _textUndoHistory; }
        }

        IMergeTextUndoTransactionPolicy ITextUndoTransaction.MergePolicy
        {
            get;
            set;
        }

        ITextUndoTransaction ITextUndoTransaction.Parent
        {
            get { throw new NotSupportedException(); }
        }

        IList<ITextUndoPrimitive> ITextUndoTransaction.UndoPrimitives
        {
            get { return UndoPrimitives; }
        }

        UndoTransactionState ITextUndoTransaction.State
        {
            get { throw new NotSupportedException(); }
        }

        string ITextUndoTransaction.Description
        {
            get { return Description; }
            set { Description = value; }
        }

        void ITextUndoTransaction.Cancel()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: false);
        }

        void ITextUndoTransaction.Complete()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: true);
        }

        void ITextUndoTransaction.Do()
        {
            for (var i = 0; i < _primitiveList.Count; i++)
            {
                _primitiveList[i].Do();
            }
        }

        void ITextUndoTransaction.Undo()
        {
            for (var i = _primitiveList.Count - 1; i >= 0; i--)
            {
                _primitiveList[i].Undo();
            }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            _textUndoHistory.OnTransactionClosed(this, didComplete: false);
        }

        #endregion
    }
}
