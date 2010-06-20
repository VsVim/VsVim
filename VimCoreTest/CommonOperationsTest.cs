using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class CommonOperationsTest
    {

        private class OperationsImpl : CommonOperations
        {
            internal OperationsImpl(OperationsData data) : base(data) { }
        }

        private IWpfTextView _view;
        private ITextBuffer _buffer;
        private MockFactory _factory;
        private Mock<IEditorOperations> _editorOpts;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private ICommonOperations _operations;
        private CommonOperations _operationsRaw;

        public void Create(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _buffer = _view.TextBuffer;
            _factory = new MockFactory(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>();
            _jumpList = _factory.Create<IJumpList>();
            _editorOpts = _factory.Create<IEditorOperations>();
            _settings = _factory.Create<IVimLocalSettings>();
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _outlining = _factory.Create<IOutliningManager>();
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(2);
            _settings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();

            var data = new OperationsData(
                vimHost:_host.Object,
                editorOperations:_editorOpts.Object,
                textView:_view,
                outliningManager:_outlining.Object,
                jumpList:_jumpList.Object,
                localSettings:_settings.Object,
                undoRedoOperations:_undoRedoOperations.Object,
                editorOptions:null,
                keyMap:null,
                navigator:null,
                statusUtil:null);
                
            _operationsRaw = new OperationsImpl(data);
            _operations = _operationsRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _operations = null;
            _operationsRaw = null;
        }

        [Test]
        public void Join1()
        {
            Create("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            Create("foo", "   bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a count")]
        public void Join3()
        {
            Create("foo", "bar", "baz");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 3));
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a single count, should be no different")]
        public void Join4()
        {
            Create("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Join5()
        {
            Create("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foobar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void Join6()
        {
            Create("foo", " bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void GoToDefinition1()
        {
            Create("foo");
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _host.Setup(x => x.GoToDefinition()).Returns(true);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
        }

        [Test]
        public void GoToDefinition2()
        {
            Create("foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.IsTrue(((Result.Failed)res).Item.Contains("foo"));
        }

        [Test, Description("Make sure we don't crash when nothing is under the cursor")]
        public void GoToDefinition3()
        {
            Create("      foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void GoToDefinition4()
        {
            Create("  foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefNoWordUnderCursor, res.AsFailed().Item);
        }

        [Test]
        public void GoToDefinition5()
        {
            Create("foo bar baz");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefFailed("foo"), res.AsFailed().Item);
        }

        [Test]
        public void SetMark1()
        {
            Create("foo");
            var map = new MarkMap(new TrackingLineColumnService());
            var vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            vimBuffer.SetupGet(x => x.MarkMap).Returns(map);
            vimBuffer.SetupGet(x => x.TextBuffer).Returns(_buffer);
            var res = _operations.SetMark(vimBuffer.Object, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, 'a');
            Assert.IsTrue(res.IsSucceeded);
            Assert.IsTrue(map.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test, Description("Invalid mark character")]
        public void SetMark2()
        {
            Create("bar"); 
            var map = new MarkMap(new TrackingLineColumnService());
            var vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            vimBuffer.SetupGet(x => x.MarkMap).Returns(map);
            var res = _operations.SetMark(vimBuffer.Object, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, ';');
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test]
        public void JumpToMark1()
        {
            Create("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(new SnapshotPoint(_view.TextSnapshot, 0), 'a');
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_view.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsed>>(null)
                .Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            var res = _operations.JumpToMark('a', map);
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
            _outlining.Verify();
        }

        [Test]
        public void JumpToMark2()
        {
            Create("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            var res = _operations.JumpToMark('b', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test, Description("Jump to global mark")]
        public void JumpToMark3()
        {
            Create("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetMark(new SnapshotPoint(_view.TextSnapshot, 0), 'A');
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(_view.TextSnapshot,0))).Returns(true);
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_view.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsed>>(null)
                .Verifiable();
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
            _outlining.Verify();
        }

        [Test, Description("Jump to global mark and jump fails")]
        public void JumpToMark4()
        {
            Create();
            var view = EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetMark(new SnapshotPoint(view.TextSnapshot, 0), 'A');
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(view.TextSnapshot,0))).Returns(false);
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test, Description("Jump to global mark that does not exist")]
        public void JumpToMark5()
        {
            Create("foo", "bar");
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            buffer.SetupGet(x => x.TextBuffer).Returns(_view.TextBuffer);
            buffer.SetupGet(x => x.Name).Returns("foo");
            var map = new MarkMap(new TrackingLineColumnService());
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test]
        public void PasteAfter1()
        {
            Create("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.LineWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yaybar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter2()
        {
            Create("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.CharacterWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("fyayoo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            Create("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual(3, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yay", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(2).GetText());
        }

        [Test]
        public void PasteAfter4()
        {
            Create("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test]
        public void PasteAfter5()
        {
            Create("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.LineWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test, Description("Character wise paste at the end of the line should go on that line")]
        public void PasteAfter6()
        {
            Create("foo", "bar");
            var buffer = _view.TextBuffer;
            var point = buffer.CurrentSnapshot.GetLineFromLineNumber(0).End;
            _operations.PasteAfter(point, "yay", OperationKind.CharacterWise);
            Assert.AreEqual("fooyay", buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Line wise paste at the end of the file should add a new line")]
        public void PasteAfter7()
        {
            Create("foo", "bar");
            var point = _buffer.GetLineSpan(1).Start;
            _operations.PasteAfter(point, "foo", OperationKind.LineWise);
            Assert.AreEqual(3, _buffer.CurrentSnapshot.LineCount);
            Assert.AreEqual("foo", _buffer.GetLineSpan(2).GetText());
        }

        [Test]
        public void PasteBefore1()
        {
            Create("foo", "bar");
            var buffer = _view.TextBuffer;
            var span = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
            Assert.AreEqual("yayfoo", span.Snapshot.GetLineFromLineNumber(0).GetText());
        }


        [Test]
        public void PasteBefore2()
        {
            Create("foo", "bar");
            var buffer = _view.TextBuffer;
            var snapshot = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual("yay", snapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", snapshot.GetLineFromLineNumber(1).GetText());
        }


        [Test]
        public void PasteBefore3()
        {
            Create("foo", "bar");
            var buffer = _view.TextBuffer;
            var snapshot = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 3), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual("yay", snapshot.GetLineFromLineNumber(0).Extent.GetText());
            Assert.AreEqual("foo", snapshot.GetLineFromLineNumber(1).Extent.GetText());
        }


        [Test]
        public void MoveCaretRight1()
        {
            Create("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretRight2()
        {
            Create("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.MoveCaretRight(2);
            Assert.AreEqual(2, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Don't move past the end of the line")]
        public void MoveCaretRight3()
        {
            Create("foo", "bar");
            var tss = _view.TextSnapshot;
            var endPoint = tss.GetLineFromLineNumber(0).End;
            _view.Caret.MoveTo(endPoint);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(endPoint, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't crash going off the buffer")]
        public void MoveCaretRight4()
        {
            Create("foo", "bar");
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(last.End, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't go off the end of the current line")]
        public void MoveCaretRight5()
        {
            Create("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false).Verifiable();
            _editorOpts.Setup(x => x.ResetSelection());
            _view.Caret.MoveTo(line.End.Subtract(1));
            _operations.MoveCaretRight(1);
            Assert.AreEqual(line.End.Subtract(1), _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
            _globalSettings.Verify();
        }

        [Test, Description("If already past the line, MoveCaretRight should not move the caret at all")]
        public void MoveCaretRight6()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.GetLine(0).End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(_view.GetLine(0).End, _view.GetCaretPoint());
        }

        [Test, Description("Move past the end of the line if VirtualEdit=onemore is set")]
        public void MoveCaretRight7()
        {
            Create("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _view.Caret.MoveTo(_view.GetLine(0).End.Subtract(1));
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(_view.GetLine(0).End, _view.GetCaretPoint());
            _editorOpts.Verify();
            _globalSettings.Verify();
        }

        [Test]
        public void MoveCaretLeft1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Move left on the start of the line should not go anywhere")]
        public void MoveCaretLeft2()
        {
            Create("foo", "bar");
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveCaretLeft3()
        {
            Create("foo", "bar");
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
            Create("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveCaretUp1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
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
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var first = _view.TextSnapshot.Lines.First();
            _view.Caret.MoveTo(first.End);
            _operations.MoveCaretUp(1);
            Assert.AreEqual(first.End, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Move caret up should respect column positions")]
        public void MoveCaretUp3()
        {
            Create("foo", "bar");
            var tss = _view.TextSnapshot;
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start.Add(1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp4()
        {
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
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
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(2);
            Assert.AreEqual(2, count);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp6()
        {
            Create("smaller", "foo bar baz");
            _view.MoveCaretTo(_view.GetLine(1).End);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineUp(false))
                .Callback(() => _view.MoveCaretTo(_view.GetLine(0).End))
                .Verifiable();
            _operations.MoveCaretUp(1);
            var point = _buffer.GetLine(0).End.Subtract(1);
            Assert.AreEqual(point, _view.GetCaretPoint());
        }

        [Test]
        public void MoveCaretUp7()
        {
            Create("foo bar baz", "", "smaller aoeu ao aou ");
            _view.MoveCaretTo(_view.GetLine(2).End);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineUp(false))
                .Callback(() => _view.MoveCaretTo(_view.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretUp(1);
            var point = _buffer.GetLine(1).End;
            Assert.AreEqual(point, _view.GetCaretPoint());
        }

        [Test]
        [Description("Should not reset the selection if the move is not possible")]
        public void MoveCaretUp8()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _operations.MoveCaretUp(1);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
        }

        [Test, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            Create(
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
            Create(
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
            Create("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            Create("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            Create("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 5));
            Assert.AreEqual('a', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            Create("foo bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }


        [Test]
        public void MoveCaretDown1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should fail if the caret is at the end of the buffer")]
        public void MoveCaretDown2()
        {
            Create("bar", "baz", "aeu");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start);
            _operations.MoveCaretDown(1);
            Assert.AreEqual(last.Start, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
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
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 1);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveCaretDown(1);
        }

        [Test]
        public void MoveCaretDown5()
        {
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
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
            Create("foo", "bar", "baz", "jaz");
            var count = 0;
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
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
        public void MoveCaretDown7()
        {
            Create("foo bar baz", "smaller");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => _view.MoveCaretTo(_view.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretDown(1);
            var point = _buffer.GetLine(1).End.Subtract(1);
            Assert.AreEqual(point, _view.GetCaretPoint());
        }

        [Test]
        public void MoveCaretDown8()
        {
            Create("foo bar baz", "", "smaller");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => _view.MoveCaretTo(_view.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretDown(1);
            var point = _buffer.GetLine(1).End;
            Assert.AreEqual(point, _view.GetCaretPoint());
        }

        [Test]
        public void DeleteSpan1()
        {
            Create("foo", "bar");
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
            Create("foo", "bar", "baz");
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
            Create("foo", "bar", "baz");
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
            Create("foo", "bar");
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
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var reg = new Register('c');
            _operations.Yank(span, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            Assert.AreEqual(span.GetText(), reg.StringValue);
        }

        [Test]
        public void ShiftSpanRight1()
        {
            Create("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanRight(1, span);
            Assert.AreEqual("  foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftSpanRight2()
        {
            Create("a", "b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            _operations.ShiftSpanRight(1, span);
            Assert.AreEqual("  a", _buffer.GetLine(0).GetText());
            Assert.AreEqual("b", _buffer.GetLine(1).GetText());
        }

        [Test, Description("Only shift whitespace")]
        public void ShiftSpanLeft1()
        {
            Create("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(1, span);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Don't puke on an empty line")]
        public void ShiftSpanLeft2()
        {
            Create("");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(1, span);
            Assert.AreEqual("", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftSpanLeft3()
        {
            Create("  foo", "  bar");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).End);
            _operations.ShiftSpanLeft(1, span);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", _buffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ShiftSpanLeft4()
        {
            Create("   foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(1, span);
            Assert.AreEqual(" foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftSpanLeft5()
        {
            Create("  a", "  b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            _operations.ShiftSpanLeft(1, span);
            Assert.AreEqual("a", _buffer.GetLine(0).GetText());
            Assert.AreEqual("  b", _buffer.GetLine(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft1()
        {
            Create("   foo");
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft2()
        {
            Create(" foo");
            _operations.ShiftLinesLeft(400);
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft3()
        {
            Create("   foo", "    bar");
            _operations.ShiftLinesLeft(2);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("  bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft4()
        {
            Create(" foo", "   bar");
            _view.MoveCaretTo(_buffer.GetLineSpan(1).Start.Position);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual(" bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesRight1()
        {
            Create("foo");
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("  foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesRight2()
        {
            Create("foo", " bar");
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("  foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("   bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesRight3()
        {
            Create("foo", " bar");
            _view.MoveCaretTo(_buffer.GetLineSpan(1).Start.Position);
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("   bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ScrollLines1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollLines2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollLines3()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Down, 1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollPages1()
        {
            Create("");
            _editorOpts.Setup(x => x.ScrollPageUp()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Up, 1);
            _editorOpts.Verify();
        }

        [Test]
        public void ScrollPages2()
        {
            Create("");
            var count = 0;
            _editorOpts.Setup(x => x.ScrollPageUp()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Up, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ScrollPages3()
        {
            Create("");
            _editorOpts.Setup(x => x.ScrollPageDown()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Down, 1);
            _editorOpts.Verify();
        }

        [Test]
        public void ScrollPages4()
        {
            Create("");
            var count = 0;
            _editorOpts.Setup(x => x.ScrollPageDown()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Down, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void DeleteLines1()
        {
            Create("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLines(1, reg);
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(4, _view.TextSnapshot.LineCount);
        }

        [Test, Description("Caret position should not affect this operation")]
        public void DeleteLines2()
        {
            Create("foo", "bar", "baz", "jaz");
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLines(1, reg);
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(4, _view.TextSnapshot.LineCount);
        }

        [Test, Description("Delete past the end of the buffer should not crash")]
        public void DeleteLines3()
        {
            Create("foo", "bar", "baz", "jaz");
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLines(3000, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor1()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(1, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor2()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(2, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine + "bar", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor3()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(2, reg);
            Assert.AreEqual("f", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("oo" + Environment.NewLine + "bar", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak1()
        {
            Create("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLinesIncludingLineBreak(1, reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(3, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak2()
        {
            Create("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLinesIncludingLineBreak(2, reg);
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(2, _view.TextSnapshot.LineCount);
        }

        [Test]
        [Description("Deleting the last line should change the line count")]
        public void DeleteLinesIncludingLineBreak3()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _view.MoveCaretTo(_view.GetLine(1).Start);
            _operations.DeleteLinesIncludingLineBreak(1, reg);
            Assert.AreEqual(Environment.NewLine + "bar", reg.StringValue);
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak4()
        {
            Create("foo");
            var reg = new Register('c');
            _operations.DeleteLinesIncludingLineBreak(1, reg);
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetText());
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor1()
        {
            Create("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _view.MoveCaretTo(1);
            _operations.DeleteLinesIncludingLineBreakFromCursor(1, reg);
            Assert.AreEqual("oo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("fbar", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(3, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor2()
        {
            Create("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _view.MoveCaretTo(1);
            _operations.DeleteLinesIncludingLineBreakFromCursor(2, reg);
            Assert.AreEqual("oo" + Environment.NewLine + "bar" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("fbaz", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("jaz", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(2, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void ChangeLetterCase1()
        {
            Create("foo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ChangeLetterCase2()
        {
            Create("fOo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FoO", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ChangeLetterCase3()
        {
            Create("fOo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0,1));
            Assert.AreEqual("FoO", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("BAR", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ChangeLetterCase4()
        {
            Create("f12o", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("F12O", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersLowercase1()
        {
            Create("FOO", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersLowercase2()
        {
            Create("FOO", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(1));
            Assert.AreEqual("bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void MakeLettersLowercase3()
        {
            Create("FoO123", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("foo123", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersUppercase1()
        {
            Create("foo123", "bar");
            _operations.MakeLettersUppercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO123", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersUppercase2()
        {
            Create("fOo123", "bar");
            _operations.MakeLettersUppercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO123", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        [Description("Inclusive motions need to put the caret on End-1 in most cases.  See e as an example of why")]
        public void MoveCaretToMotionData1()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 1, 2),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(2, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData2()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData3()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData4()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, 3),
                false,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
        }

        [Test, Description("Exclusive motions should go to End")]
        public void MoveCaretToMotionData5()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 1, 2),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(3, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData6()
        {
            Create("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
        }

        [Test]
        [Description("Motion to empty last line")]
        public void MoveCaretToMotionData7()
        {
            Create("foo", "bar", "");
            _editorOpts.Setup(x => x.ResetSelection());
            var data = new MotionData(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length),
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                FSharpOption<int>.None);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(2, _view.GetCaretPoint().GetContainingLine().LineNumber);
        }

        [Test]
        public void Undo1()
        {
            Create(String.Empty);
            _undoRedoOperations.Setup(x => x.Undo(1)).Verifiable();
            _operations.Undo(1);
            _undoRedoOperations.Verify();
        }

        [Test]
        public void Redo1()
        {
            Create(String.Empty);
            _undoRedoOperations.Setup(x => x.Redo(1)).Verifiable();
            _operations.Redo(1);
            _undoRedoOperations.Verify();
        }

        [Test]
        public void Beep1()
        {
            Create(String.Empty);
            _globalSettings.Setup(x => x.VisualBell).Returns(false).Verifiable();
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.Beep();
            _factory.Verify();
        }

        [Test]
        public void Beep2()
        {
            Create(String.Empty);
            _globalSettings.Setup(x => x.VisualBell).Returns(true).Verifiable();
            _operations.Beep();
            _factory.Verify();
        }
    }
}
