using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.UI.Undo;

namespace VimCoreTest.Utils
{
    public sealed class TestOnlyUndoHistory : UndoHistory
    {
        public override void AddUndo<P0, P1, P2, P3, P4>(UndoableOperation<P0, P1, P2, P3, P4> operation, P0 p0, P1 p1, P2 p2, P3 p3, P4 p4)
        {
            throw new NotImplementedException();
        }

        public override void AddUndo<P0, P1, P2, P3>(UndoableOperation<P0, P1, P2, P3> operation, P0 p0, P1 p1, P2 p2, P3 p3)
        {
            throw new NotImplementedException();
        }

        public override void AddUndo<P0, P1, P2>(UndoableOperation<P0, P1, P2> operation, P0 p0, P1 p1, P2 p2)
        {
            throw new NotImplementedException();
        }

        public override void AddUndo<P0, P1>(UndoableOperation<P0, P1> operation, P0 p0, P1 p1)
        {
            throw new NotImplementedException();
        }

        public override void AddUndo<P0>(UndoableOperation<P0> operation, P0 p0)
        {
            throw new NotImplementedException();
        }

        public override void AddUndo(UndoableOperationVoid operation)
        {
            throw new NotImplementedException();
        }

        public override bool CanRedo
        {
            get { return false; }
        }

        public override bool CanUndo
        {
            get { return false; }
        }

        public override void ClearMarker(UndoTransactionMarker marker)
        {
            throw new NotImplementedException();
        }

        public override UndoTransaction CreateTransaction(string description, bool isHidden)
        {
            throw new NotImplementedException();
        }

        public override UndoTransaction CreateTransaction(string description)
        {
            throw new NotImplementedException();
        }

        public override UndoTransaction CurrentTransaction
        {
            get { throw new NotImplementedException(); }
        }

        public override void Do(IUndoPrimitive undoPrimitive)
        {
            throw new NotImplementedException();
        }

        public override UndoTransaction FindMarker(UndoTransactionMarker marker)
        {
            throw new NotImplementedException();
        }

        public override long GetEstimatedSize()
        {
            throw new NotImplementedException();
        }

        public override object GetMarker(UndoTransactionMarker marker, UndoTransaction transaction)
        {
            throw new NotImplementedException();
        }

        public override UndoTransaction LastRedoTransaction
        {
            get { throw new NotImplementedException(); }
        }

        public override UndoTransaction LastUndoTransaction
        {
            get { throw new NotImplementedException(); }
        }

        public override Microsoft.VisualStudio.Utilities.PropertyCollection Properties
        {
            get { throw new NotImplementedException(); }
        }

        public override void Redo(int count)
        {
            throw new NotImplementedException();
        }

        public override string RedoDescription
        {
            get { throw new NotImplementedException(); }
        }

        public override IEnumerable<UndoTransaction> RedoStack
        {
            get { throw new NotImplementedException(); }
        }

        public override void ReplaceMarker(UndoTransactionMarker marker, UndoTransaction transaction, object value)
        {
            throw new NotImplementedException();
        }

        public override void ReplaceMarkerOnTop(UndoTransactionMarker marker, object value)
        {
            throw new NotImplementedException();
        }

        public override UndoHistoryState State
        {
            get { throw new NotImplementedException(); }
        }

        public override bool TryFindMarkerOnTop(UndoTransactionMarker marker, out object value)
        {
            throw new NotImplementedException();
        }

        public override void Undo(int count)
        {
            throw new NotImplementedException();
        }

        public override string UndoDescription
        {
            get { throw new NotImplementedException(); }
        }

#pragma warning disable 67
        public override event EventHandler<UndoRedoEventArgs> UndoRedoHappened;
        public override event EventHandler<UndoTransactionCompletedEventArgs> UndoTransactionCompleted;
#pragma warning restore 67

        public override IEnumerable<UndoTransaction> UndoStack
        {
            get { throw new NotImplementedException(); }
        }

    }
}
