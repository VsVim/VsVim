using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class UndoRedoOperationsTest
    {
        private MockRepository _factory;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ITextUndoHistory> _textUndoHistory;
        private UndoRedoOperations _undoRedoOperationsRaw;
        private IUndoRedoOperations _undoRedoOperations;
        private int _undoCount;
        private int _redoCount;

        public void Create(bool haveHistory = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>();

            var editorOperationsFactoryService = _factory.Create<IEditorOperationsFactoryService>();
            if (haveHistory)
            {
                _textUndoHistory = _factory.Create<ITextUndoHistory>();
                _textUndoHistory.Setup(x => x.Undo(It.IsAny<int>())).Callback<int>(count => { _undoCount += count; });
                _textUndoHistory.Setup(x => x.Redo(It.IsAny<int>())).Callback<int>(count => { _redoCount += count; });
                    
                _undoRedoOperationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption.Create(_textUndoHistory.Object),
                    editorOperationsFactoryService.Object);
            }
            else
            {
                _undoRedoOperationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption<ITextUndoHistory>.None,
                    editorOperationsFactoryService.Object);
            }
            _undoRedoOperations = _undoRedoOperationsRaw;
        }

        private void RaiseUndoTransactionCompleted(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var args = new TextUndoTransactionCompletedEventArgs(null, TextUndoTransactionCompletionResult.TransactionAdded);
                _textUndoHistory.Raise(x => x.UndoTransactionCompleted += null, _textUndoHistory.Object, args);
            }
        }

        private void RaiseUndoRedoHappened(bool expected = true)
        {
            if (!expected)
            {
                _statusUtil.Setup(x => x.OnError(Resources.Common_UndoRedoUnexpected)).Verifiable();
            }

            var args = new TextUndoRedoEventArgs(TextUndoHistoryState.Undoing, null);
            _textUndoHistory.Raise(x => x.UndoRedoHappened += null, _textUndoHistory.Object, args);

            if (!expected)
            {
                _statusUtil.Verify();
            }
        }

        public sealed class LinkedUndoTest : UndoRedoOperationsTest
        {
            /// <summary>
            /// Make sure the implementation correctly handles a close of a linked transaction that has 
            /// been orphaned by a ResetState call
            /// </summary>
            [Fact]
            public void ClosedBrokenChain()
            {
                Create();
                var linkedUndoTransaction = _undoRedoOperations.CreateLinkedUndoTransaction("test");
                _undoRedoOperationsRaw.ResetState();
                Assert.Equal(0, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
                linkedUndoTransaction.Complete();
                Assert.Equal(0, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
            }

            /// <summary>
            /// Make sure an orphaned linked transaction doesn't upset any state after it 
            /// </summary>
            [Fact]
            public void ClosedBrokenChainWithNewOpen()
            {
                Create();
                var linkedUndoTransaction1 = _undoRedoOperations.CreateLinkedUndoTransaction("test1");
                _undoRedoOperationsRaw.ResetState();
                var linkedUndoTransaction2 = _undoRedoOperations.CreateLinkedUndoTransaction("test2");
                Assert.Equal(1, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
                linkedUndoTransaction1.Complete();
                Assert.Equal(1, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
                linkedUndoTransaction2.Complete();
                Assert.Equal(0, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
            }

            /// <summary>
            /// Make sure the count is properly managed here
            /// </summary>
            [Fact]
            public void Count()
            {
                Create();
                int count = 10;
                var stack = new Stack<ILinkedUndoTransaction>();
                for (int i = 0; i < count; i++)
                {
                    stack.Push(_undoRedoOperations.CreateLinkedUndoTransaction("test"));
                }

                Assert.Equal(count, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);

                while (stack.Count > 0)
                {
                    stack.Pop().Complete();
                    Assert.Equal(stack.Count, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
                }

                Assert.Equal(0, _undoRedoOperationsRaw.LinkedUndoTransactionStack.Count);
            }

            [Fact]
            public void UndoGroup()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("test"))
                {
                    RaiseUndoTransactionCompleted(count: 3);
                }

                _undoRedoOperations.Undo(1);
                Assert.Equal(3, _undoCount);
            }

            [Fact]
            public void RedoGroup()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("test"))
                {
                    RaiseUndoTransactionCompleted(count: 3);
                }

                _undoRedoOperations.Undo(1);
                Assert.Equal(3, _undoCount);
                _undoRedoOperations.Redo(1);
                Assert.Equal(3, _redoCount);
            }

            [Fact]
            public void UnexpectedUndoRedoMustClearLinkedState()
            {
                Create();
                var linkedUndoTransaction = _undoRedoOperations.CreateLinkedUndoTransaction("Test");
                RaiseUndoRedoHappened(expected: false);
                Assert.False(_undoRedoOperations.InLinkedUndoTransaction);
                linkedUndoTransaction.Complete();
                Assert.False(_undoRedoOperations.InLinkedUndoTransaction);
            }

            /// <summary>
            /// Ensure that we properly protect against manual manipulation of the undo / redo stack
            /// by another component.  If another component directly calls Undo / Redo on the 
            /// ITextUndoHistory object then we have lost all context.  We need to completely reset our 
            /// state at this point because our ability to understand the stack has been completely lost.
            /// 
            /// Don't let linked transactions persist across this event
            /// </summary>
            [Fact]
            public void Issue672()
            {
                Create();
                var linkedUndoTransaction = _undoRedoOperations.CreateLinkedUndoTransaction("Test");

                // Simulate another component manually manipulating the undo / redo stack
                RaiseUndoRedoHappened(expected: false);
                RaiseUndoTransactionCompleted(count: 3);

                linkedUndoTransaction.Complete();
                _undoRedoOperations.Undo(1);
                Assert.Equal(1, _undoCount);
            }
        }

        public sealed class UndoTest : UndoRedoOperationsTest
        {
            /// <summary>
            /// Undo without history should raise an error message saying it's not supported
            /// </summary>
            [Fact]
            public void WithoutHistory()
            {
                Create(haveHistory: false);
                _statusUtil.Setup(x => x.OnError(Resources.Internal_UndoRedoNotSupported)).Verifiable();
                _undoRedoOperationsRaw.Undo(1);
                _factory.Verify();
            }

            /// <summary>
            /// Very possible for undo to throw.  Make sure we handle it 
            /// </summary>
            [Fact]
            public void Throws()
            {
                Create();
                _statusUtil.Setup(x => x.OnError(Resources.Internal_CannotUndo)).Verifiable();
                _textUndoHistory.Setup(x => x.Undo(1)).Throws(new NotSupportedException()).Verifiable();
                _undoRedoOperationsRaw.Undo(1);
                _factory.Verify();
            }

            /// <summary>
            /// If there is no undo stack then just pass on the undo's to the 
            /// ITextUndoHistory value
            /// </summary>
            [Fact]
            public void NoStack()
            {
                Create();
                _undoRedoOperationsRaw.Undo(2);
                Assert.Equal(2, _undoCount);
            }

            /// <summary>
            /// If there is a Linked item on the top of the undo stack make sure that 
            /// the appropriate number is passed to the ITextUndoHistory instance
            /// </summary>
            [Fact]
            public void StackWithLinked()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("test"))
                {
                    RaiseUndoTransactionCompleted(count: 10);
                }

                _undoRedoOperationsRaw.Undo(1);
                Assert.Equal(10, _undoCount);
                Assert.Equal(0, _undoRedoOperationsRaw.UndoStack.Length);
            }

            /// <summary>
            /// If there is a Normal item on the top of the undo stack make sure that 
            /// just a single item is passed to undo.  It's a simple undo
            /// </summary>
            [Fact]
            public void StackWithNormal()
            {
                Create();
                RaiseUndoTransactionCompleted(count: 10);
                _undoRedoOperationsRaw.Undo(1);
                Assert.Equal(1, _undoCount);
                Assert.Equal(1, _undoRedoOperationsRaw.UndoStack.Length);
                Assert.Equal(9, _undoRedoOperationsRaw.UndoStack.Head.AsNormal().Item);
                Assert.Equal(1, _undoRedoOperationsRaw.RedoStack.Length);
                Assert.Equal(1, _undoRedoOperationsRaw.RedoStack.Head.AsNormal().Item);
            }

            /// <summary>
            /// If an undo occurs with a linked undo transaction open we should default back to Visual 
            /// Studio undo
            /// </summary>
            [Fact]
            public void WithLinkedTransactionOpen()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("Linked Undo"))
                {
                    RaiseUndoTransactionCompleted(count: 10);
                    _statusUtil.Setup(x => x.OnError(Resources.Common_UndoChainBroken)).Verifiable();
                    _undoRedoOperations.Undo(1);
                    Assert.Equal(1, _undoCount);
                    Assert.Equal(0, _undoRedoOperationsRaw.UndoStack.Length);
                    Assert.False(_undoRedoOperations.InLinkedUndoTransaction);
                }

                Assert.Equal(1, _undoCount);
                Assert.Equal(0, _undoRedoOperationsRaw.UndoStack.Length);
                Assert.False(_undoRedoOperations.InLinkedUndoTransaction);
            }
        }

        public sealed class RedoTest : UndoRedoOperationsTest
        {
            /// <summary>
            /// If there is no ITextUndoHistory associated with the operations then raise the
            /// appropriate error message
            /// </summary>
            [Fact]
            public void NoHistory()
            {
                Create(haveHistory: false);
                _statusUtil.Setup(x => x.OnError(Resources.Internal_UndoRedoNotSupported)).Verifiable();
                _undoRedoOperationsRaw.Redo(1);
                _factory.Verify();
            }

            /// <summary>
            /// Make sure that redo can also handle a throws
            /// </summary>
            [Fact]
            public void Throws()
            {
                Create();
                _statusUtil.Setup(x => x.OnError(Resources.Internal_CannotRedo)).Verifiable();
                _textUndoHistory.Setup(x => x.Redo(1)).Throws(new NotSupportedException()).Verifiable();
                _undoRedoOperationsRaw.Redo(1);
                _factory.Verify();
            }

            /// <summary>
            /// With no stack the undo's should just go straight to the ITextUndoHistory one at 
            /// a time
            /// </summary>
            [Fact]
            public void NoStack()
            {
                Create();
                _textUndoHistory.Setup(x => x.Redo(1)).Verifiable();
                _undoRedoOperationsRaw.Redo(2);
                _factory.Verify();
            }

            /// <summary>
            /// If there is a Linked item on the top of the redo stack make sure that 
            /// the appropriate number is passed to the ITextUndoHistory instance
            /// </summary>
            [Fact]
            public void StackWithLinked()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("test"))
                {
                    RaiseUndoTransactionCompleted(count: 10);
                }
                _undoRedoOperations.Undo(count: 1);
                _undoRedoOperationsRaw.Redo(1);
                Assert.Equal(10, _redoCount);
                Assert.Equal(0, _undoRedoOperationsRaw.RedoStack.Length);
                Assert.Equal(1, _undoRedoOperationsRaw.UndoStack.Length);
            }

            /// <summary>
            /// If there is a Normal item on the top of the undo stack make sure that 
            /// just a single item is passed to undo.  It's a simple undo
            /// </summary>
            [Fact]
            public void StackWithNormal()
            {
                Create();
                RaiseUndoTransactionCompleted(count: 10);
                _undoRedoOperations.Undo(count: 10);
                _undoRedoOperations.Redo(1);
                Assert.Equal(1, _redoCount);
                Assert.Equal(1, _undoRedoOperationsRaw.RedoStack.Length);
                Assert.Equal(9, _undoRedoOperationsRaw.RedoStack.Head.AsNormal().Item);
            }
        }

        public sealed class UndoRedoCompletedTest : UndoRedoOperationsTest
        {
            /// <summary>
            /// An undo transaction completing should empty the redo stack.
            /// </summary>
            [Fact]
            public void EmptyRedo()
            {
                Create();
                using (var transaction = _undoRedoOperations.CreateLinkedUndoTransaction("test"))
                {
                    RaiseUndoTransactionCompleted(count: 10);
                }
                _undoRedoOperations.Undo(1);
                RaiseUndoTransactionCompleted();
                Assert.Equal(0, _undoRedoOperationsRaw.RedoStack.Length);
            }
        }

        public sealed class MiscTest : UndoRedoOperationsTest
        {
            [Fact]
            public void CreateUndoTransaction1()
            {
                Create(haveHistory: false);
                var transaction = _undoRedoOperationsRaw.CreateUndoTransaction("foo");
                Assert.NotNull(transaction);
                _factory.Verify();
            }

            [Fact]
            public void CreateUndoTransaction2()
            {
                Create();
                var mock = _factory.Create<ITextUndoTransaction>();
                _textUndoHistory.Setup(x => x.CreateTransaction("foo")).Returns(mock.Object).Verifiable();
                var transaction = _undoRedoOperationsRaw.CreateUndoTransaction("foo");
                Assert.NotNull(transaction);
                _factory.Verify();
            }

        }
    }
}
