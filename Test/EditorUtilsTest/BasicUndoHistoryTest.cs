using Xunit;
using Microsoft.VisualStudio.Text.Editor;
using System;
using EditorUtils.Implementation.BasicUndo;
using Moq;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils.UnitTest
{
    public abstract class BasicUndoHistoryTest
    {
        private readonly object _context;
        private readonly IBasicUndoHistory _basicUndoHistory;
        private readonly MockRepository _factory;
        internal readonly BasicUndoHistory _basicUndoHistoryRaw;

        public BasicUndoHistoryTest()
        {
            _context = new object();
            _basicUndoHistoryRaw = new BasicUndoHistory(_context);
            _basicUndoHistory = _basicUndoHistoryRaw;
            _factory = new MockRepository(MockBehavior.Loose);
        }

        public sealed class ClearTest : BasicUndoHistoryTest
        {
            /// <summary>
            /// The IEditorOperations implementation likes to put ITextView instances into 
            /// the ITextUndoHistory implementation in order to implement caret undo / redo
            /// operations.  The Clear method clears the undo stack and also should remove
            /// this value to make memory leak testing sane
            /// </summary>
            [Fact]
            public void RemoveTextView()
            {
                _basicUndoHistory.Properties[typeof(ITextView)] = 42;
                _basicUndoHistory.Clear();
                Assert.False(_basicUndoHistory.Properties.ContainsProperty(typeof(ITextView)));
            }

            [Fact]
            public void ClearTransactions()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    transaction.Complete();
                }

                _basicUndoHistory.Clear();
                Assert.Equal(0, _basicUndoHistoryRaw.UndoStack.Count);
                Assert.Equal(0, _basicUndoHistoryRaw.RedoStack.Count);
            }

            /// <summary>
            /// Can't perform a clear in the middle of a transaction
            /// </summary>
            [Fact]
            public void OpenTransaction()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    Assert.Throws<InvalidOperationException>(() => _basicUndoHistory.Clear());
                    transaction.Complete();
                }
            }
        }

        public sealed class CreateTransactionTest : BasicUndoHistoryTest
        {
            [Fact]
            public void UpdatesUndoStack()
            {
                var transaction = _basicUndoHistory.CreateTransaction("Test");
                transaction.Complete();
                Assert.Equal(1, _basicUndoHistoryRaw.UndoStack.Count);
                Assert.Same(transaction, _basicUndoHistoryRaw.UndoStack.Peek());
            }

            [Fact]
            public void UpdatesCurrentTransaction()
            {
                using (var transaction = _basicUndoHistory.CreateTransaction("Test"))
                {
                    Assert.Same(transaction, _basicUndoHistory.CurrentTransaction);
                    transaction.Complete();
                }

                Assert.Null(_basicUndoHistory.CurrentTransaction);

            }
        }

        public sealed class UndoRedoTest : BasicUndoHistoryTest
        {
            /// <summary>
            /// The implementation should match the WPF editor and not throw when Undo called when there
            /// are no Undo operations
            /// </summary>
            [Fact]
            public void EmptyUndo()
            {
                _basicUndoHistory.Undo(1);
                _basicUndoHistory.Undo(100);
            }

            [Fact]
            public void SimpleUndo()
            {
                var undoCount = 0;
                using (var transaction = _basicUndoHistory.CreateTransaction("test"))
                {
                    var primitive = _factory.Create<ITextUndoPrimitive>(MockBehavior.Strict);
                    primitive.Setup(x => x.Undo()).Callback(() => { undoCount++; });
                    transaction.AddUndo(primitive.Object);
                    transaction.Complete();
                }

                _basicUndoHistory.Undo(1);
                Assert.Equal(1, undoCount);
            }

            [Fact]
            public void EmptyRedo()
            {
                _basicUndoHistory.Redo(1);
                _basicUndoHistory.Redo(100);
            }

            [Fact]
            public void SimpleRedo()
            {
                var doCount = 0;
                using (var transaction = _basicUndoHistory.CreateTransaction("test"))
                {
                    var primitive = _factory.Create<ITextUndoPrimitive>(MockBehavior.Strict);
                    primitive.Setup(x => x.Undo());
                    primitive.Setup(x => x.Do()).Callback(() => { doCount++; });
                    transaction.AddUndo(primitive.Object);
                    transaction.Complete();
                }

                _basicUndoHistory.Undo(1);
                _basicUndoHistory.Redo(1);
                Assert.Equal(1, doCount);
            }
        }
    }
}
