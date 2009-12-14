using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Vim.Modes;
using Moq;
using Vim;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class Common_ModeUtilTest
    {
        private IWpfTextView _view;
        private ITextBuffer _buffer;

        public void CreateLines(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _buffer = _view.TextBuffer;
        }

        [Test]
        public void Join1()
        {
            CreateLines("foo","bar");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test,Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            CreateLines("foo", "   bar");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a count")]
        public void Join3()
        {
            CreateLines("foo", "bar", "baz");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 3));
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a single count, should be no different")]
        public void Join4()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(),JoinKind.RemoveEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Join5()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foobar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void Join6()
        {
            CreateLines("foo", " bar");
            Assert.IsTrue(ModeUtil.Join(_view, _view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void GoToDefinition1()
        {
            CreateLines("foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(true);
            var res = ModeUtil.GoToDefinition(_view, host.Object);
            Assert.IsTrue(res.IsSucceeded);
        }

        [Test]
        public void GoToDefinition2()
        {
            CreateLines("foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = ModeUtil.GoToDefinition(_view, host.Object);
            Assert.IsTrue(res.IsFailed);
            Assert.IsTrue(((ModeUtil.Result.Failed)res).Item.Contains("foo"));
        }

        [Test, Description("Make sure we don't crash when nothing is under the cursor")]
        public void GoToDefinition3()
        {
            CreateLines("      foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = ModeUtil.GoToDefinition(_view, host.Object);
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void SetMark1()
        {
            CreateLines("foo");
            var map = new MarkMap();
            var res = ModeUtil.SetMark(map, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, 'a');
            Assert.IsTrue(res.IsSucceeded);
            Assert.IsTrue(map.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test,Description("Invalid mark character")]
        public void SetMark2()
        {
            CreateLines("bar");
            var map = new MarkMap();
            var res = ModeUtil.SetMark(map, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, ';');
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void JumpToMark1()
        {
            var view = Utils.EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap();
            map.SetMark(new SnapshotPoint(view.TextSnapshot, 0), 'a');
            var res = ModeUtil.JumpToMark(map, view, 'a');
            Assert.IsTrue(res.IsSucceeded);
        }

        [Test]
        public void JumpToMark2()
        {
            var view = Utils.EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap();
            var res = ModeUtil.JumpToMark(map, view, 'b');
            Assert.IsTrue(res.IsFailed);
        }

        [Test, Description("Global marks aren't supported yet")]
        public void JumpToMark3()
        {
            var view = Utils.EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap();
            map.SetMark(new SnapshotPoint(view.TextSnapshot, 0), 'B');
            var res = ModeUtil.JumpToMark(map, view, 'B');
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void PasteAfter1()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = ModeUtil.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay", OperationKind.LineWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yaybar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter2()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = ModeUtil.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("fyayoo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = ModeUtil.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual(3, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yay", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(2).GetText());
        }

        [Test]
        public void PasteAfter4()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = ModeUtil.PasteAfter(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test]
        public void PasteAfter5()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = ModeUtil.PasteAfter(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.LineWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test,Description("Character wise paste at the end of the line should go on that line")]
        public void PasteAfter6()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var point = buffer.CurrentSnapshot.GetLineFromLineNumber(0).End;
            ModeUtil.PasteAfter(point, "yay", OperationKind.CharacterWise);
            Assert.AreEqual("fooyay", buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteBefore1()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = ModeUtil.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay");
            Assert.AreEqual("yay", span.GetText());
            Assert.AreEqual("yayfoo", span.Snapshot.GetLineFromLineNumber(0).GetText());
        }
    }
}
