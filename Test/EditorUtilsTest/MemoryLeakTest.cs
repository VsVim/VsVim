using System;
using System.Windows.Threading;
using Xunit;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace EditorUtils.UnitTest
{
    /// <summary>
    /// Used for detecting leaks in our components
    /// </summary>
    public sealed class MemoryLeakTest : EditorHostTest
    {
        private void RunGarbageCollector()
        {
            for (var i = 0; i < 15; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private void ClearUndoHistory(ITextBuffer textBuffer)
        {
            IBasicUndoHistory basicUndoHistory;
            Assert.True(BasicUndoHistoryRegistry.TryGetBasicUndoHistory(textBuffer, out basicUndoHistory));
            basicUndoHistory.Clear();
        }

        private void DoWork(Action action)
        {
            action();
        }

        /// <summary>
        /// Ensure the undo history doesn't keep the ITextView alive due to a simple transaction
        /// </summary>
        [Fact]
        public void UndoTransactionSimple()
        {
            var textBuffer = CreateTextBuffer("");
            var textView = TextEditorFactoryService.CreateTextView(textBuffer);
            var weakTextView = new WeakReference(textView);
            DoWork(
                () =>
                {
                    var undoManager = TextBufferUndoManagerProvider.GetTextBufferUndoManager(textBuffer);
                    using (var transaction = undoManager.TextBufferUndoHistory.CreateTransaction("Test Edit"))
                    {
                        textBuffer.SetText("hello world");
                        transaction.Complete();
                    }
                });

            textView.Close();
            textView = null;
            RunGarbageCollector();
            Assert.Null(weakTextView.Target);
        }

        /// <summary>
        /// Ensure the undo history doesn't keep the ITextView alive after an undo primitive
        /// surrounding the caret is added to the undo stack 
        /// </summary>
        [Fact]
        public void UndoTransactionWithCaretPrimitive()
        {
            var textBuffer = CreateTextBuffer("");
            var textView = TextEditorFactoryService.CreateTextView(textBuffer);
            var weakTextView = new WeakReference(textView);
            DoWork(
                () =>
                {
                    var undoManager = TextBufferUndoManagerProvider.GetTextBufferUndoManager(textBuffer);
                    using (var transaction = undoManager.TextBufferUndoHistory.CreateTransaction("Test Edit"))
                    {
                        var operations = EditorOperationsFactoryService.GetEditorOperations(textView);
                        operations.AddBeforeTextBufferChangePrimitive();
                        textBuffer.SetText("hello world");
                        transaction.Complete();
                    }
                });

            textView.Close();
            textView = null;

            // The AddBeforeTextBufferChangePrimitive put the ITextView into the undo stack 
            // so we need to clear it out here
            ClearUndoHistory(textBuffer);

            RunGarbageCollector();
            Assert.Null(weakTextView.Target);
        }
    }
}
