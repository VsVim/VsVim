using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for ViewUtilTest
    /// </summary>
    [TestFixture]
    public class ViewUtilTest
    {
        private IWpfTextView _view;

        public void CreateView(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
        }

        [SetUp]
        public void Init()
        {
            CreateView(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
        }

        [Test, Description("Move to the top of the file")]
        public void MoveToLine1()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            var point = ViewUtil.MoveToLineStart(_view, line);
            Assert.AreEqual(point, line.Start);
            Assert.AreEqual(point, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Make sure the caret moves")]
        public void MoveToLine2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            var point = ViewUtil.MoveToLineStart(_view, line);
            Assert.AreEqual(line.Start, point);
            Assert.AreEqual(point, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void FindCurrentFullWord1()
        {
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 3));
            var word = ViewUtil.FindCurrentFullWord(_view, WordKind.NormalWord);
            Assert.IsTrue(string.IsNullOrEmpty(word));
        }

        [Test]
        public void FindCurrentFullWord2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            var word = ViewUtil.FindCurrentFullWord(_view, WordKind.NormalWord);
            Assert.AreEqual("boy", word);
        }

        [Test, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            var line1 = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line1.End);
            ViewUtil.MoveWordForward(_view, WordKind.NormalWord);
            var line2 = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line2.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveWordForward2()
        {
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            ViewUtil.MoveWordForward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveWordBackword1()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            _view = Utils.EditorUtil.CreateView("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 5));
            Assert.AreEqual('a', _view.Caret.Position.BufferPosition.GetChar());
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            _view = Utils.EditorUtil.CreateView("foo bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            ViewUtil.MoveWordBackward(_view, WordKind.NormalWord);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

    }
}
