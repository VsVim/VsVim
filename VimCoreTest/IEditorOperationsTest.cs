using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest
{
    [TestFixture]
    public class IEditorOperationsTest
    {
        private ITextView _view;
        private ITextBuffer _buffer;
        private IEditorOperations _operations;

        public void CreateLines(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _view = tuple.Item1;
            _buffer = _view.TextBuffer;
            _operations = tuple.Item2;
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
