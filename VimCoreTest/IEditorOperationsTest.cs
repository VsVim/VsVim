using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using NUnit.Framework;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class IEditorOperationsTest : VimTestBase
    {
        private ITextView _view;
        private ITextBuffer _buffer;
        private IEditorOperations _operations;

        public void CreateLines(params string[] lines)
        {
            _view = CreateTextView(lines);
            _buffer = _view.TextBuffer;
            _operations = EditorOperationsFactoryService.GetEditorOperations(_view);
        }

        [Test, Description("Be wary the 0 length last line")]
        public void MoveCaretDown1()
        {
            CreateLines("foo", String.Empty);
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveLineDown(false);
        }

        [Test, Description("Move caret down should maintain column")]
        public void MoveCaretDown2()
        {
            CreateLines("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(0).Start.Add(1));
            _operations.MoveLineDown(false);
            Assert.AreEqual(tss.GetLineFromLineNumber(1).Start.Add(1), _view.Caret.Position.BufferPosition);
        }
    }
}
