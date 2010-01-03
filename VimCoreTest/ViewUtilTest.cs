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

    }
}
