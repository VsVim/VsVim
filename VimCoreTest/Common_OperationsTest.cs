using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCore.Modes.Common;

namespace VimCoreTest
{
    [TestClass]
    public class Common_OperationsTest
    {
        private IWpfTextView _view;
        private ITextBuffer _buffer;

        public void CreateLines(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _buffer = _view.TextBuffer;
        }

        [TestMethod]
        public void Join1()
        {
            CreateLines("foo","bar");
            Assert.IsTrue(Operations.Join(_view, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod,Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            CreateLines("foo", "   bar");
            Assert.IsTrue(Operations.Join(_view, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Join with a count")]
        public void Join3()
        {
            CreateLines("foo", "bar", "baz");
            Assert.IsTrue(Operations.Join(_view, 3));
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(7, _view.Caret.Position.BufferPosition.Position);
        }

        [TestMethod, Description("Join with a single count, should be no different")]
        public void Join4()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(Operations.Join(_view, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

    }
}
