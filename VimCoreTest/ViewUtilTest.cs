using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for ViewUtilTest
    /// </summary>
    [TestClass]
    public class ViewUtilTest
    {
        private IWpfTextView _view;

        public void CreateView(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
        }

        [TestInitialize]
        public void Init()
        {
            CreateView(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
        }

        [TestMethod, Description("Move to the top of the file")]
        public void MoveToLine1()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            var point = ViewUtil.MoveToLineStart(_view, line);
            Assert.AreEqual(point, line.Start);
            Assert.AreEqual(point, _view.Caret.Position.BufferPosition);
        }

        [TestMethod, Description("Make sure the caret moves")]
        public void MoveToLine2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            var point = ViewUtil.MoveToLineStart(_view, line);
            Assert.AreEqual(line.Start, point);
            Assert.AreEqual(point, _view.Caret.Position.BufferPosition);
        }

        [TestMethod]
        public void FindCurrentFullWord1()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 3));
            var word = ViewUtil.FindCurrentFullWord(_view, WordKind.NormalWord);
            Assert.IsTrue(string.IsNullOrEmpty(word));
        }

        [TestMethod]
        public void FindCurrentFullWord2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            var word = ViewUtil.FindCurrentFullWord(_view, WordKind.NormalWord);
            Assert.AreEqual("boy", word);
        }

        [TestMethod, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            var line1 = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line1.End);
            ViewUtil.MoveWordForward(_view, WordKind.NormalWord);
            var line2 = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line2.Start, _view.Caret.Position.BufferPosition);
        }

        [TestMethod]
        public void MoveWordForward2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            ViewUtil.MoveWordForward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod]
        public void MoveWordBackword1()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 5));
            Assert.AreEqual('a', _view.Caret.Position.BufferPosition.GetChar());
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            _view = Utils.EditorUtil.CreateView("foo bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Don't crash going off the buffer")]
        public void MoveCaretRight1()
        {
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.End);
            var res = ViewUtil.MoveCaretRight(_view);
            Assert.AreEqual(last.End, res);
        }

        [TestMethod, Description("Don't go off the end of the current line")]
        public void MoveCaretRight2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            var res = ViewUtil.MoveCaretRight(_view);
            Assert.AreEqual(res, line.End);

        }

        [TestMethod]
        public void MoveCaretLeft1()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start.Add(1));
            var res = ViewUtil.MoveCaretLeft(_view);
            Assert.AreEqual(line.Start, res);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [TestMethod, Description("Left at the start of the line should not go further")]
        public void MoveCaretLeft2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            var res = ViewUtil.MoveCaretLeft(_view);
            Assert.AreEqual(line.Start, res);
        }

        [TestMethod, Description("Move caret down should fail if the caret is at the end of the buffer")]
        public void MoveCaretDown1()
        {
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start);
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreEqual(last.Start, res);
        }

        [TestMethod, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown2()
        {
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 2);
            _view.Caret.MoveTo(line.Start);
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreNotEqual(line.Start, res);
        }

        [TestMethod, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown3()
        {
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 1);
            _view.Caret.MoveTo(line.Start);
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreEqual(line.Start, res);
        }

        [TestMethod, Description("Be wary the 0 length last line")]
        public void MoveCaretDown4()
        {
            CreateView("foo", String.Empty);
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreNotEqual(line.Start, res);
        }

        [TestMethod, Description("Move caret down should maintain column")]
        public void MoveCaretDown5()
        {
            CreateView("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(0).Start.Add(1));
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreEqual(res, tss.GetLineFromLineNumber(1).Start.Add(1));
        }

        [TestMethod, Description("Move caret down should maintain column")]
        public void MoveCaretDown6()
        {
            CreateView("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(0).End);
            var res = ViewUtil.MoveCaretDown(_view);
            Assert.AreEqual(res, tss.GetLineFromLineNumber(1).End);
        }

        [TestMethod, Description("Move caret up past the begining of the buffer should fail if it's already at the top")]
        public void MoveCaretUp1()
        {
            var first = _view.TextSnapshot.Lines.First();
            _view.Caret.MoveTo(first.End);
            var res = ViewUtil.MoveCaretUp(_view);
            Assert.AreEqual(first.End, res);
        }

        [TestMethod, Description("Move caret up should respect column positions")]
        public void MoveCaretUp2()
        {
            CreateView("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start.Add(1));
            var res = ViewUtil.MoveCaretUp(_view);
            Assert.AreEqual(res, tss.GetLineFromLineNumber(0).Start.Add(1));
        }
    }
}
