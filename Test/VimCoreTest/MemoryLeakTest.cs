using System;
using System.Windows.Threading;
using Xunit;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Vim.EditorHost;
using System.Runtime.CompilerServices;

namespace Vim.UnitTest
{
    /// <summary>
    /// Used for detecting leaks in our components
    /// </summary>
    public sealed class MemoryLeakTest : VimTestBase
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
            Assert.True(BasicUndoHistoryRegistry.TryGetBasicUndoHistory(textBuffer, out IBasicUndoHistory basicUndoHistory));
            basicUndoHistory.Clear();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference DoWork(Func<WeakReference> func) => func();

        /// <summary>
        /// Ensure the undo history doesn't keep the ITextView alive due to a simple transaction
        /// </summary>
        [WpfFact]
        public void UndoTransactionSimple()
        {
            var weakReference = DoWork(
                () =>
                {
                    var textBuffer = CreateTextBuffer("");
                    var textView = TextEditorFactoryService.CreateTextView(textBuffer);
                    var weakTextView = new WeakReference(textView);
                    var undoManager = TextBufferUndoManagerProvider.GetTextBufferUndoManager(textBuffer);
                    using (var transaction = undoManager.TextBufferUndoHistory.CreateTransaction("Test Edit"))
                    {
                        textBuffer.SetText("hello world");
                        transaction.Complete();
                    }
                    textView.Close();
                    return weakTextView;
                });

            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }

        /// <summary>
        /// Ensure the undo history doesn't keep the ITextView alive after an undo primitive
        /// surrounding the caret is added to the undo stack 
        /// </summary>
        [WpfFact]
        public void UndoTransactionWithCaretPrimitive()
        {
            var weakReference = DoWork(
                () =>
                {
                    var textBuffer = CreateTextBuffer("");
                    var textView = TextEditorFactoryService.CreateTextView(textBuffer);
                    var weakTextView = new WeakReference(textView);
                    var undoManager = TextBufferUndoManagerProvider.GetTextBufferUndoManager(textBuffer);
                    using (var transaction = undoManager.TextBufferUndoHistory.CreateTransaction("Test Edit"))
                    {
                        var operations = EditorOperationsFactoryService.GetEditorOperations(textView);
                        operations.AddBeforeTextBufferChangePrimitive();
                        textBuffer.SetText("hello world");
                        transaction.Complete();
                    }
                    textView.Close();

                    // The AddBeforeTextBufferChangePrimitive put the ITextView into the undo stack 
                    // so we need to clear it out here
                    ClearUndoHistory(textBuffer);

                    return weakTextView;
                });


            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }
    }
}
