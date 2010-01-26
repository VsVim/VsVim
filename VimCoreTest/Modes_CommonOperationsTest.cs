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
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class Modes_CommonOperationsTest
    {

        private class OperationsImpl : CommonOperations
        {
            internal OperationsImpl(ITextView view, IEditorOperations opts) : base(view, opts) { }
        }

        private IWpfTextView _view;
        private ITextBuffer _buffer;
        private Mock<IEditorOperations> _editorOpts;
        private ICommonOperations _operations;
        private CommonOperations _operationsRaw;

        public void CreateLines(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _buffer = _view.TextBuffer;
            _editorOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _operationsRaw = new OperationsImpl(_view, _editorOpts.Object);
            _operations = _operationsRaw;
        }

        [Test]
        public void Join1()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            CreateLines("foo", "   bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a count")]
        public void Join3()
        {
            CreateLines("foo", "bar", "baz");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 3));
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a single count, should be no different")]
        public void Join4()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Join5()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foobar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void Join6()
        {
            CreateLines("foo", " bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void GoToDefinition1()
        {
            CreateLines("foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(true);
            var res = _operations.GoToDefinition(host.Object);
            Assert.IsTrue(res.IsSucceeded);
        }

        [Test]
        public void GoToDefinition2()
        {
            CreateLines("foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition(host.Object);
            Assert.IsTrue(res.IsFailed);
            Assert.IsTrue(((Result.Failed)res).Item.Contains("foo"));
        }

        [Test, Description("Make sure we don't crash when nothing is under the cursor")]
        public void GoToDefinition3()
        {
            CreateLines("      foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition(host.Object);
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void GoToDefinition4()
        {
            CreateLines("  foo");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition(host.Object);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefNoWordUnderCursor, res.AsFailed().Item);
        }

        [Test]
        public void GoToDefinition5()
        {
            CreateLines("foo bar baz");
            var host = new Mock<IVimHost>(MockBehavior.Strict);
            host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition(host.Object);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefFailed("foo"), res.AsFailed().Item);
        }

        [Test]
        public void SetMark1()
        {
            CreateLines("foo");
            var map = new MarkMap();
            var res = _operations.SetMark('a', map, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);
            Assert.IsTrue(res.IsSucceeded);
            Assert.IsTrue(map.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test, Description("Invalid mark character")]
        public void SetMark2()
        {
            CreateLines("bar");
            var map = new MarkMap();
            var res = _operations.SetMark(';', map, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test]
        public void JumpToMark1()
        {
            CreateLines("foo", "bar");
            var map = new MarkMap();
            map.SetMark(new SnapshotPoint(_view.TextSnapshot, 0), 'a');
            var res = _operations.JumpToMark('a', map);
            Assert.IsTrue(res.IsSucceeded);
        }

        [Test]
        public void JumpToMark2()
        {
            var view = Utils.EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap();
            var res = _operations.JumpToMark('b', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test, Description("Global marks aren't supported yet")]
        public void JumpToMark3()
        {
            var view = Utils.EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap();
            map.SetMark(new SnapshotPoint(view.TextSnapshot, 0), 'B');
            var res = _operations.JumpToMark('B', map);
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void PasteAfter1()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay", OperationKind.LineWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yaybar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter2()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("fyayoo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            var view = EditorUtil.CreateBuffer("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(view.CurrentSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual(3, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yay", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(2).GetText());
        }

        [Test]
        public void PasteAfter4()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test]
        public void PasteAfter5()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.LineWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test, Description("Character wise paste at the end of the line should go on that line")]
        public void PasteAfter6()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var point = buffer.CurrentSnapshot.GetLineFromLineNumber(0).End;
            _operations.PasteAfter(point, "yay", OperationKind.CharacterWise);
            Assert.AreEqual("fooyay", buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteBefore1()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "bar");
            var span = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay");
            Assert.AreEqual("yay", span.GetText());
            Assert.AreEqual("yayfoo", span.Snapshot.GetLineFromLineNumber(0).GetText());
        }


        [Test]
        public void MoveCaretRight1()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretRight2()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.MoveCaretRight(2);
            Assert.AreEqual(2, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Don't move past the end of the line")]
        public void MoveCaretRight3()
        {
            CreateLines("foo", "bar");
            var tss = _view.TextSnapshot;
            var endPoint = tss.GetLineFromLineNumber(0).End;
            _view.Caret.MoveTo(endPoint);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(endPoint, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't crash going off the buffer")]
        public void MoveCaretRight4()
        {
            CreateLines("foo", "bar");
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.End);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(last.End, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Don't go off the end of the current line")]
        public void MoveCaretRight5()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _editorOpts.Setup(x => x.ResetSelection());
            _view.Caret.MoveTo(line.End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(line.End, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }


        [Test]
        public void MoveCaretLeft1()
        {
            CreateLines("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Move left on the start of the line should not go anywhere")]
        public void MoveCaretLeft2()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretLeft3()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start.Add(1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Left at the start of the line should not go further")]
        public void MoveCaretLeft4()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp1()
        {
            CreateLines("foo", "bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret up past the begining of the buffer should fail if it's already at the top")]
        public void MoveCaretUp2()
        {
            CreateLines("foo", "bar", "baz");
            var first = _view.TextSnapshot.Lines.First();
            _view.Caret.MoveTo(first.End);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretUp(1);
            Assert.AreEqual(first.End, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret up should respect column positions")]
        public void MoveCaretUp3()
        {
            CreateLines("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start.Add(1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp4()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(1);
            Assert.AreEqual(1, count);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp5()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(2);
            Assert.AreEqual(2, count);
            _editorOpts.Verify();
        }

        [Test, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            CreateLines(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
            var line1 = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line1.End);
            _operations.MoveWordForward(WordKind.NormalWord,1);
            var line2 = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line2.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveWordForward2()
        {
            CreateLines(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveWordForward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveWordBackword1()
        {
            CreateLines("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            CreateLines("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            CreateLines("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 5));
            Assert.AreEqual('a', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            CreateLines("foo bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }


        [Test]
        public void MoveCaretDown1()
        {
            CreateLines("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should fail if the caret is at the end of the buffer")]
        public void MoveCaretDown2()
        {
            CreateLines("bar", "baz", "aeu");
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDown(1);
            Assert.AreEqual(last.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown3()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 2);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown4()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDown5()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(1);
            Assert.AreEqual(1, count);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDown6()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(2);
            Assert.AreEqual(2, count);
            _editorOpts.Verify();
        }

        [Test]
        public void DeleteSpan1()
        {
            CreateLines("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                reg);
            var tss = _view.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
        }

        [Test]
        public void DeleteSpan2()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var reg = new Register('c');
            _operations.DeleteSpan(span, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            tss = _view.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(span.GetText(), reg.StringValue);
        }

        [Test]
        public void DeleteSpan3()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteSpan(tss.GetLineFromLineNumber(1).ExtentIncludingLineBreak, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(1).GetText());
        }


        [Test]
        public void Yank1()
        {
            CreateLines("foo", "bar");
            var reg = new Register('c');
            _operations.Yank(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
        }

        [Test]
        public void Yank2()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var reg = new Register('c');
            _operations.Yank(span, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            Assert.AreEqual(span.GetText(), reg.StringValue);
        }

        [Test]
        public void ShiftRight1()
        {
            CreateLines("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftRight(span, 2);
            Assert.AreEqual("  foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Only shift whitespace")]
        public void ShiftLeft1()
        {
            CreateLines("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftLeft(span, 2);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Don't puke on an empty line")]
        public void ShiftLeft2()
        {
            CreateLines("");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftLeft(span, 2);
            Assert.AreEqual("", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLeft3()
        {
            CreateLines("  foo", "  bar");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).End);
            _operations.ShiftLeft(span, 2);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", _buffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ShiftLeft4()
        {
            CreateLines("   foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftLeft(span, 2);
            Assert.AreEqual(" foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }


    }
}
