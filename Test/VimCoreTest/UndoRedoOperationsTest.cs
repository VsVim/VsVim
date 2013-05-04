using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class UndoRedoOperationsTest
    {
        private MockRepository _factory;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ITextUndoHistory> _textUndoHistory;
        private Mock<IEditorOperations> _editorOperations;
        private UndoRedoOperations _operationsRaw;
        private IUndoRedoOperations _operations;

        public void Create(bool haveHistory = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _editorOperations = _factory.Create<IEditorOperations>();
            _statusUtil = _factory.Create<IStatusUtil>();
            if (haveHistory)
            {
                _textUndoHistory = _factory.Create<ITextUndoHistory>();
                _operationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption.Create(_textUndoHistory.Object),
                    _editorOperations.Object);
            }
            else
            {
                _operationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption<ITextUndoHistory>.None,
                    _editorOperations.Object);
            }
            _operations = _operationsRaw;
        }

        /// <summary>
        /// Make sure that the implementation is resilent to the broken undo chain
        /// </summary>
        [Fact]
        public void LinkedUndoTransactionClosed_BrokenChain()
        {
            Create();
            _operationsRaw.LinkedUndoTransactionClosed();
            Assert.Equal(0, _operationsRaw._openLinkedTransactionCount);
        }

        /// <summary>
        /// Make sure the count is properly managed here
        /// </summary>
        [Fact]
        public void LinkedUndoTransactionClosed_Count()
        {
            Create();
            _operations.CreateLinkedUndoTransaction();
            _operations.CreateLinkedUndoTransaction();
            Assert.Equal(2, _operationsRaw._openLinkedTransactionCount);
            _operationsRaw.LinkedUndoTransactionClosed();
            _operationsRaw.LinkedUndoTransactionClosed();
            Assert.Equal(0, _operationsRaw._openLinkedTransactionCount);
        }

        /// <summary>
        /// Undo without history should raise an error message saying it's not supported
        /// </summary>
        [Fact]
        public void Undo_WithoutHistory()
        {
            Create(haveHistory: false);
            _statusUtil.Setup(x => x.OnError(Resources.Internal_UndoRedoNotSupported)).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
        }

        /// <summary>
        /// Very possible for undo to throw.  Make sure we handle it 
        /// </summary>
        [Fact]
        public void Undo_Throws()
        {
            Create();
            _statusUtil.Setup(x => x.OnError(Resources.Internal_CannotUndo)).Verifiable();
            _textUndoHistory.Setup(x => x.Undo(1)).Throws(new NotSupportedException()).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
        }

        /// <summary>
        /// If there is no undo stack then just pass on the undo's to the 
        /// ITextUndoHistory value
        /// </summary>
        [Fact]
        public void Undo_NoStack()
        {
            Create();
            _textUndoHistory.Setup(x => x.Undo(1)).Verifiable();
            _operationsRaw.Undo(2);
            _factory.Verify();
        }

        /// <summary>
        /// If there is a Linked item on the top of the undo stack make sure that 
        /// the appropriate number is passed to the ITextUndoHistory instance
        /// </summary>
        [Fact]
        public void Undo_StackWithLinked()
        {
            Create();
            _operationsRaw._undoStack = (new[] {UndoRedoData.NewLinked(10)}).ToFSharpList();
            _textUndoHistory.Setup(x => x.Undo(10)).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
            Assert.Equal(0, _operationsRaw._undoStack.Length);
        }

        /// <summary>
        /// If there is a Normal item on the top of the undo stack make sure that 
        /// just a single item is passed to undo.  It's a simple undo
        /// </summary>
        [Fact]
        public void Undo_StackWithNormal()
        {
            Create();
            _operationsRaw._undoStack = (new[] {UndoRedoData.NewNormal(10)}).ToFSharpList();
            _textUndoHistory.Setup(x => x.Undo(1)).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
            Assert.Equal(1, _operationsRaw._undoStack.Length);
            Assert.Equal(9, _operationsRaw._undoStack.Head.AsNormal().Item);
            Assert.Equal(1, _operationsRaw._redoStack.Length);
            Assert.Equal(1, _operationsRaw._redoStack.Head.AsNormal().Item);
        }

        /// <summary>
        /// If an undo occurs with a linked undo transaction open we should default back to Visual 
        /// Studio undo
        /// </summary>
        [Fact]
        public void Undo_WithLinkedTransactionOpen()
        {
            Create();
            _operations.CreateLinkedUndoTransaction();
            _operationsRaw._undoStack = (new[] {UndoRedoData.NewLinked(10)}).ToFSharpList();
            _textUndoHistory.Setup(x => x.Undo(1)).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_UndoChainBroken)).Verifiable();
            _operations.Undo(1);
            _factory.Verify();
            Assert.Equal(0, _operationsRaw._undoStack.Length);
            Assert.False(_operations.InLinkedUndoTransaction);
        }

        /// <summary>
        /// If there is no ITextUndoHistory associated with the operations then raise the
        /// appropriate error message
        /// </summary>
        [Fact]
        public void Redo_NoHistory()
        {
            Create(haveHistory: false);
            _statusUtil.Setup(x => x.OnError(Resources.Internal_UndoRedoNotSupported)).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
        }

        /// <summary>
        /// Make sure that redo can also handle a throws
        /// </summary>
        [Fact]
        public void Redo_Throws()
        {
            Create();
            _statusUtil.Setup(x => x.OnError(Resources.Internal_CannotRedo)).Verifiable();
            _textUndoHistory.Setup(x => x.Redo(1)).Throws(new NotSupportedException()).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
        }

        /// <summary>
        /// With no stack the undo's should just go straight to the ITextUndoHistory one at 
        /// a time
        /// </summary>
        [Fact]
        public void Redo_NoStack()
        {
            Create();
            _textUndoHistory.Setup(x => x.Redo(1)).Verifiable();
            _operationsRaw.Redo(2);
            _factory.Verify();
        }

        /// <summary>
        /// If there is a Linked item on the top of the redo stack make sure that 
        /// the appropriate number is passed to the ITextUndoHistory instance
        /// </summary>
        [Fact]
        public void Redo_StackWithLinked()
        {
            Create();
            _operationsRaw._redoStack = (new[] {UndoRedoData.NewLinked(10)}).ToFSharpList();
            _textUndoHistory.Setup(x => x.Redo(10)).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
            Assert.Equal(0, _operationsRaw._redoStack.Length);
            Assert.Equal(1, _operationsRaw._undoStack.Length);
        }

        /// <summary>
        /// If there is a Normal item on the top of the undo stack make sure that 
        /// just a single item is passed to undo.  It's a simple undo
        /// </summary>
        [Fact]
        public void Redo_StackWithNormal()
        {
            Create();
            _operationsRaw._redoStack = (new[] {UndoRedoData.NewNormal(10)}).ToFSharpList();
            _textUndoHistory.Setup(x => x.Redo(1)).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
            Assert.Equal(1, _operationsRaw._redoStack.Length);
            Assert.Equal(9, _operationsRaw._redoStack.Head.AsNormal().Item);
        }

        [Fact]
        public void CreateUndoTransaction1()
        {
            Create(haveHistory: false);
            var transaction = _operationsRaw.CreateUndoTransaction("foo");
            Assert.NotNull(transaction);
            _factory.Verify();
        }

        [Fact]
        public void CreateUndoTransaction2()
        {
            Create();
            var mock = _factory.Create<ITextUndoTransaction>();
            _textUndoHistory.Setup(x => x.CreateTransaction("foo")).Returns(mock.Object).Verifiable();
            var transaction = _operationsRaw.CreateUndoTransaction("foo");
            Assert.NotNull(transaction);
            _factory.Verify();
        }

        /// <summary>
        /// An undo transaction completing should empty the redo stack.
        /// </summary>
        [Fact]
        public void UndoTransactionCompleted_EmptyRedo()
        {
            Create();
            _operationsRaw._redoStack = (new[] {UndoRedoData.NewLinked(10)}).ToFSharpList();
            _textUndoHistory.Raise(x => x.UndoTransactionCompleted += null, new TextUndoTransactionCompletedEventArgs(null, TextUndoTransactionCompletionResult.TransactionAdded));
            Assert.Equal(0, _operationsRaw._redoStack.Length);
        }
    }
}
