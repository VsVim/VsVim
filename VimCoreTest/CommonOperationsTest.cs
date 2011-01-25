using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class CommonOperationsTest
    {
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private MockRepository _factory;
        private Mock<IEditorOptions> _editorOptions;
        private Mock<IEditorOperations> _editorOperations;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ISmartIndentationService> _smartIndent;
        private ISearchService _searchService;
        private IRegisterMap _registerMap;
        private IVimData _vimData;
        private ICommonOperations _operations;
        private CommonOperations _operationsRaw;

        public void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _vimData = new VimData();
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _textBuffer = _textView.TextBuffer;
            _factory = new MockRepository(MockBehavior.Strict);
            _registerMap = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice(_factory).Object);
            _host = _factory.Create<IVimHost>();
            _jumpList = _factory.Create<IJumpList>();
            _editorOptions = _factory.Create<IEditorOptions>();
            _editorOptions.Setup(x => x.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId)).Returns(true);
            _editorOperations = _factory.Create<IEditorOperations>();
            _editorOperations.Setup(x => x.AddAfterTextBufferChangePrimitive());
            _editorOperations.Setup(x => x.AddBeforeTextBufferChangePrimitive());
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _globalSettings.SetupGet(x => x.SmartCase).Returns(false);
            _globalSettings.SetupGet(x => x.IgnoreCase).Returns(true);
            _globalSettings.SetupGet(x => x.UseEditorIndent).Returns(false);
            _globalSettings.SetupGet(x => x.UseEditorTabSettings).Returns(false);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object, _factory);
            _settings.SetupGet(x => x.AutoIndent).Returns(false);
            _settings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);
            _settings.SetupGet(x => x.ExpandTab).Returns(true);
            _settings.SetupGet(x => x.TabStop).Returns(4);
            _outlining = _factory.Create<IOutliningManager>();
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(2);
            _statusUtil = _factory.Create<IStatusUtil>();
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();
            _undoRedoOperations.Setup(x => x.CreateUndoTransaction(It.IsAny<string>())).Returns<string>(name => new UndoTransaction(FSharpOption.Create(EditorUtil.GetUndoHistory(_textView.TextBuffer).CreateTransaction(name))));
            _smartIndent = _factory.Create<ISmartIndentationService>();
            _searchService = new SearchService(EditorUtil.FactoryService.TextSearchService, _globalSettings.Object);

            var data = new OperationsData(
                vimData: _vimData,
                vimHost: _host.Object,
                editorOperations: _editorOperations.Object,
                textView: _textView,
                outliningManager: FSharpOption.Create(_outlining.Object),
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                undoRedoOperations: _undoRedoOperations.Object,
                registerMap: _registerMap,
                editorOptions: _editorOptions.Object,
                keyMap: null,
                navigator: null,
                statusUtil: _statusUtil.Object,
                foldManager: null,
                searchService: _searchService,
                smartIndentationService: _smartIndent.Object);

            _operationsRaw = new CommonOperations(data);
            _operations = _operationsRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _operations = null;
            _operationsRaw = null;
        }

        private void AllowOutlineExpansion(bool verify = false)
        {
            var res =
                _outlining
                    .Setup(x => x.ExpandAll(It.IsAny<SnapshotSpan>(), It.IsAny<Predicate<ICollapsed>>()))
                    .Returns<IEnumerable<ICollapsible>>(null);
            if (verify)
            {
                res.Verifiable();
            }
        }

        void AssertRegister(Register reg, string value, OperationKind kind)
        {
            Assert.AreEqual(value, reg.StringValue);
            Assert.AreEqual(kind, reg.Value.OperationKind);
        }

        void AssertRegister(RegisterName name, string value, OperationKind kind)
        {
            AssertRegister(_registerMap.GetRegister(name), value, kind);
        }

        void AssertRegister(char name, string value, OperationKind kind)
        {
            AssertRegister(_registerMap.GetRegister(name), value, kind);
        }

        [Test]
        public void Join1()
        {
            Create("foo", "bar");
            _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
            Assert.AreEqual("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        [Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            Create("foo", "   bar");
            _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
            Assert.AreEqual("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        [Description("Join more than 2 lines")]
        public void Join3()
        {
            Create("foo", "bar", "baz");
            _operations.Join(_textView.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces);
            Assert.AreEqual("foo bar baz", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual(8, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        [Description("Join an empty line")]
        public void Join4()
        {
            Create("cat", "", "dog", "tree", "rabbit");
            _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
            Assert.AreEqual("cat ", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void GoToDefinition1()
        {
            Create("foo");
            _jumpList.Setup(x => x.Add(_textView.GetCaretPoint())).Verifiable();
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
            vimBuffer.SetupGet(x => x.TextBuffer).Returns(_textBuffer);
            var res = _operations.SetMark(vimBuffer.Object, _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, 'a');
            Assert.IsTrue(res.IsSucceeded);
            Assert.IsTrue(map.GetLocalMark(_textBuffer, 'a').IsSome());
        }

        [Test, Description("Invalid mark character")]
        public void SetMark2()
        {
            Create("bar");
            var map = new MarkMap(new TrackingLineColumnService());
            var vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            vimBuffer.SetupGet(x => x.MarkMap).Returns(map);
            var res = _operations.SetMark(vimBuffer.Object, _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, ';');
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test]
        public void JumpToMark1()
        {
            Create("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(new SnapshotPoint(_textView.TextSnapshot, 0), 'a');
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_textView.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsed>>(null)
                .Verifiable();
            _jumpList.Setup(x => x.Add(_textView.GetCaretPoint())).Verifiable();
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
            map.SetMark(new SnapshotPoint(_textView.TextSnapshot, 0), 'A');
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(_textView.TextSnapshot, 0))).Returns(true);
            _jumpList.Setup(x => x.Add(_textView.GetCaretPoint())).Verifiable();
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_textView.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
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
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(view.TextSnapshot, 0))).Returns(false);
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test, Description("Jump to global mark that does not exist")]
        public void JumpToMark5()
        {
            Create("foo", "bar");
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            buffer.SetupGet(x => x.TextBuffer).Returns(_textView.TextBuffer);
            buffer.SetupGet(x => x.Name).Returns("foo");
            var map = new MarkMap(new TrackingLineColumnService());
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test]
        public void PutAt_LineWiseSingleWord()
        {
            Create("foo", "bar");
            _operations.PutAt(_textView.GetPoint(0), StringData.NewSimple("yay\n"), OperationKind.LineWise);
            Assert.AreEqual("yay", _textView.GetLine(0).GetText());
            Assert.AreEqual("foo", _textView.GetLine(1).GetText());
        }

        [Test]
        public void PutAt_CharacterWiseSingleWord()
        {
            Create("foo", "bar");
            _operations.PutAt(_textView.GetPoint(0), StringData.NewSimple("yay"), OperationKind.CharacterWise);
            Assert.AreEqual("yayfoo", _textView.GetLine(0).GetText());
        }

        [Test]
        public void PutAt_ChararterWiseNotEndOfLine()
        {
            Create("foo", "bar");
            _operations.PutAt(_textView.GetLine(0).End, StringData.NewSimple("yay"), OperationKind.CharacterWise);
            Assert.AreEqual("fooyay", _textView.GetLine(0).GetText());
        }

        [Test]
        public void PutAtCaret_SimpleString()
        {
            Create("foo bar");
            _operations.PutAtCaret(StringData.NewSimple("hey"), OperationKind.CharacterWise, PutKind.After, false);
            Assert.AreEqual("fheyoo bar", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint());
        }

        [Test]
        public void PutAtCaret_EndOfBufferShouldntCrash()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.GetEndPoint());
            _operations.PutAtCaret(StringData.NewSimple("hello"), OperationKind.CharacterWise, PutKind.After, false);
            Assert.AreEqual("barhello", _textView.GetLine(1).GetText());
        }

        [Test]
        public void PutAtCaret_LineWiseAndAfterShouldPutLineOnNextLine()
        {
            Create("foo", "bar");
            _operations.PutAtCaret(StringData.NewSimple("baz\n"), OperationKind.LineWise, PutKind.After, false);
            Assert.AreEqual("foo", _textView.GetLine(0).GetText());
            Assert.AreEqual("baz", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void PutAtCaret_LineWiseAndAfterShouldPutLineOnNextLineAfterWhitespace()
        {
            Create("foo", "bar");
            _operations.PutAtCaret(StringData.NewSimple("  baz\n"), OperationKind.LineWise, PutKind.After, false);
            Assert.AreEqual("foo", _textView.GetLine(0).GetText());
            Assert.AreEqual("  baz", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        public void PutAtCaret_LineWiseAndEndOfBufferShouldAddLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetEndPoint());
            _operations.PutAtCaret(StringData.NewSimple("hey"), OperationKind.LineWise, PutKind.After, false);
            Assert.AreEqual("hey", _textView.GetLineRange(2).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLineRange(2).Start);
        }

        [Test]
        public void PutAtCaret_CharacterWiseAndBefore()
        {
            Create("foo");
            _operations.PutAtCaret(StringData.NewSimple("hey"), OperationKind.CharacterWise, PutKind.Before, false);
            Assert.AreEqual("heyfoo", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void PutAtCaret_CharacterWiseAndBeforeAndMoveCaretAfter()
        {
            Create("foo");
            _operations.PutAtCaret(StringData.NewSimple("hey"), OperationKind.CharacterWise, PutKind.Before, true);
            Assert.AreEqual("heyfoo", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void PutAtCaret_LineWiseWithIdent()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.GetLine(0).End);
            _operations.PutAtCaret(StringData.NewSimple("  hey\n"), OperationKind.LineWise, PutKind.Before, false);
            Assert.AreEqual("  hey", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretRight1()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(1, _textView.Caret.Position.BufferPosition.Position);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretRight2()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _operations.MoveCaretRight(2);
            Assert.AreEqual(2, _textView.Caret.Position.BufferPosition.Position);
            _editorOperations.Verify();
        }

        [Test, Description("Don't move past the end of the line")]
        public void MoveCaretRight3()
        {
            Create("foo", "bar");
            var tss = _textView.TextSnapshot;
            var endPoint = tss.GetLineFromLineNumber(0).End;
            _textView.Caret.MoveTo(endPoint);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(endPoint, _textView.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't crash going off the buffer")]
        public void MoveCaretRight4()
        {
            Create("foo", "bar");
            var last = _textView.TextSnapshot.Lines.Last();
            _textView.Caret.MoveTo(last.End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(last.End, _textView.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't go off the end of the current line")]
        public void MoveCaretRight5()
        {
            Create("foo", "bar");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(0);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false).Verifiable();
            _editorOperations.Setup(x => x.ResetSelection());
            _textView.Caret.MoveTo(line.End.Subtract(1));
            _operations.MoveCaretRight(1);
            Assert.AreEqual(line.End.Subtract(1), _textView.Caret.Position.BufferPosition);
            _editorOperations.Verify();
            _globalSettings.Verify();
        }

        [Test, Description("If already past the line, MoveCaretRight should not move the caret at all")]
        public void MoveCaretRight6()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.GetLine(0).End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(_textView.GetLine(0).End, _textView.GetCaretPoint());
        }

        [Test, Description("Move past the end of the line if VirtualEdit=onemore is set")]
        public void MoveCaretRight7()
        {
            Create("foo", "bar");
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _textView.Caret.MoveTo(_textView.GetLine(0).End.Subtract(1));
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(_textView.GetLine(0).End, _textView.GetCaretPoint());
            _editorOperations.Verify();
            _globalSettings.Verify();
        }

        [Test]
        public void MoveCaretLeft1()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            _editorOperations.Verify();
        }

        [Test, Description("Move left on the start of the line should not go anywhere")]
        public void MoveCaretLeft2()
        {
            Create("foo", "bar");
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveCaretLeft3()
        {
            Create("foo", "bar");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(0);
            _textView.Caret.MoveTo(line.Start.Add(1));
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _textView.Caret.Position.BufferPosition);
            _editorOperations.Verify();
        }

        [Test, Description("Left at the start of the line should not go further")]
        public void MoveCaretLeft4()
        {
            Create("foo", "bar");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(1);
            _textView.Caret.MoveTo(line.Start);
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveCaretUp1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var line = _textView.TextSnapshot.GetLineFromLineNumber(1);
            _textView.Caret.MoveTo(line.Start);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOperations.Verify();
        }

        [Test, Description("Move caret up past the begining of the buffer should fail if it's already at the top")]
        public void MoveCaretUp2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var first = _textView.TextSnapshot.Lines.First();
            _textView.Caret.MoveTo(first.End);
            _operations.MoveCaretUp(1);
            Assert.AreEqual(first.End, _textView.Caret.Position.BufferPosition);
        }

        [Test, Description("Move caret up should respect column positions")]
        public void MoveCaretUp3()
        {
            Create("foo", "bar");
            var tss = _textView.TextSnapshot;
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _textView.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start.Add(1));
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretUp4()
        {
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(1);
            Assert.AreEqual(1, count);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretUp5()
        {
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(2);
            Assert.AreEqual(2, count);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretUp6()
        {
            Create("smaller", "foo bar baz");
            _textView.MoveCaretTo(_textView.GetLine(1).End);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineUp(false))
                .Callback(() => _textView.MoveCaretTo(_textView.GetLine(0).End))
                .Verifiable();
            _operations.MoveCaretUp(1);
            var point = _textBuffer.GetLine(0).End.Subtract(1);
            Assert.AreEqual(point, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveCaretUp7()
        {
            Create("foo bar baz", "", "smaller aoeu ao aou ");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineUp(false))
                .Callback(() => _textView.MoveCaretTo(_textView.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretUp(1);
            var point = _textBuffer.GetLine(1).End;
            Assert.AreEqual(point, _textView.GetCaretPoint());
        }

        [Test]
        [Description("Should not reset the selection if the move is not possible")]
        public void MoveCaretUp8()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _operations.MoveCaretUp(1);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            Create(
                "foo bar baz",
                "boy kick ball",
                "a big dog");
            var line1 = _textView.TextSnapshot.GetLineFromLineNumber(0);
            _textView.Caret.MoveTo(line1.End);
            _operations.MoveWordForward(WordKind.NormalWord, 1);
            var line2 = _textView.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line2.Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveWordForward2()
        {
            Create(
                "foo bar baz",
                "boy kick ball",
                "a big dog");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(0);
            _textView.Caret.MoveTo(line.Start);
            _operations.MoveWordForward(WordKind.NormalWord, 1);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveWordBackword1()
        {
            Create("foo bar");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(0);
            _textView.Caret.MoveTo(line.End);
            _operations.MoveWordBackward(WordKind.NormalWord, 1);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            Create("foo bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 4));
            Assert.AreEqual('b', _textView.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            Create("foo bar");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 5));
            Assert.AreEqual('a', _textView.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord, 1);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            Create("foo bar", "baz");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(1);
            _textView.Caret.MoveTo(line.Start);
            _operations.MoveWordBackward(WordKind.NormalWord, 1);
            Assert.AreEqual(4, _textView.Caret.Position.BufferPosition.Position);
        }


        [Test]
        public void MoveCaretDown1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOperations.Verify();
        }

        [Test, Description("Move caret down should fail if the caret is at the end of the buffer")]
        public void MoveCaretDown2()
        {
            Create("bar", "baz", "aeu");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var last = _textView.TextSnapshot.Lines.Last();
            _textView.Caret.MoveTo(last.Start);
            _operations.MoveCaretDown(1);
            Assert.AreEqual(last.Start, _textView.Caret.Position.BufferPosition);
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var tss = _textView.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 2);
            _textView.Caret.MoveTo(line.Start);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOperations.Verify();
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown4()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var tss = _textView.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 1);
            _textView.Caret.MoveTo(line.Start);
            _operations.MoveCaretDown(1);
        }

        [Test]
        public void MoveCaretDown5()
        {
            Create("foo", "bar", "baz", "jaz");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            var count = 0;
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(1);
            Assert.AreEqual(1, count);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretDown6()
        {
            Create("foo", "bar", "baz", "jaz");
            var count = 0;
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(true);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(2);
            Assert.AreEqual(2, count);
            _editorOperations.Verify();
        }

        [Test]
        public void MoveCaretDown7()
        {
            Create("foo bar baz", "smaller");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => _textView.MoveCaretTo(_textView.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretDown(1);
            var point = _textBuffer.GetLine(1).End.Subtract(1);
            Assert.AreEqual(point, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveCaretDown8()
        {
            Create("foo bar baz", "", "smaller");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _editorOperations.Setup(x => x.ResetSelection()).Verifiable();
            _editorOperations
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => _textView.MoveCaretTo(_textView.GetLine(1).End))
                .Verifiable();
            _operations.MoveCaretDown(1);
            var point = _textBuffer.GetLine(1).End;
            Assert.AreEqual(point, _textView.GetCaretPoint());
        }

        [Test]
        public void DeleteSpan1()
        {
            Create("foo", "bar");
            _operations.DeleteSpan(_textView.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak);
            var tss = _textView.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteSpan2()
        {
            Create("foo", "bar", "baz");
            var tss = _textView.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations.DeleteSpan(span);
            tss = _textView.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteSpan3()
        {
            Create("foo", "bar", "baz");
            var tss = _textView.TextSnapshot;
            _operations.DeleteSpan(tss.GetLineFromLineNumber(1).ExtentIncludingLineBreak);
            tss = _textView.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ShiftLineRangeRight1()
        {
            Create("foo");
            _operations.ShiftLineRangeRight(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual("  foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLineRangeRight2()
        {
            Create("a", "b", "c");
            _operations.ShiftLineRangeRight(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual("  a", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("b", _textBuffer.GetLine(1).GetText());
        }

        [Test, Description("Only shift whitespace")]
        public void ShiftLineRangeLeft1()
        {
            Create("foo");
            _operations.ShiftLineRangeLeft(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual("foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Don't puke on an empty line")]
        public void ShiftLineRangeLeft2()
        {
            Create("");
            _operations.ShiftLineRangeLeft(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual("", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLineRangeLeft3()
        {
            Create("  foo", "  bar");
            _operations.ShiftLineRangeLeft(1, _textBuffer.GetLineRange(0, 1));
            Assert.AreEqual("foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ShiftLineRangeLeft4()
        {
            Create("   foo");
            _operations.ShiftLineRangeLeft(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual(" foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLineRangeLeft5()
        {
            Create("  a", "  b", "c");
            _operations.ShiftLineRangeLeft(1, _textBuffer.GetLineRange(0));
            Assert.AreEqual("a", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("  b", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft1()
        {
            Create("   foo");
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft2()
        {
            Create(" foo");
            _operations.ShiftLinesLeft(400);
            Assert.AreEqual("foo", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft3()
        {
            Create("   foo", "    bar");
            _operations.ShiftLinesLeft(2);
            Assert.AreEqual(" foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft4()
        {
            Create(" foo", "   bar");
            _textView.MoveCaretTo(_textBuffer.GetLineRange(1).Start.Position);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual(" bar", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft5()
        {
            Create(" foo", "", "   bar");
            _operations.ShiftLinesLeft(3);
            Assert.AreEqual("foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("", _textBuffer.GetLineRange(1).GetText());
            Assert.AreEqual(" bar", _textBuffer.GetLineRange(2).GetText());
        }

        [Test]
        public void ShiftLinesLeft6()
        {
            Create(" foo", "   ", "   bar");
            _operations.ShiftLinesLeft(3);
            Assert.AreEqual("foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual(" ", _textBuffer.GetLineRange(1).GetText());
            Assert.AreEqual(" bar", _textBuffer.GetLineRange(2).GetText());
        }

        [Test]
        public void ShiftLinesLeft_TabStartUsingSpaces()
        {
            Create("\tcat");
            _settings.SetupGet(x => x.ExpandTab).Returns(true);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Vim will actually normalize the line and then shift")]
        public void ShiftLinesLeft_MultiTabStartUsingSpaces()
        {
            Create("\t\tcat");
            _settings.SetupGet(x => x.ExpandTab).Returns(true);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("      cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft_TabStartUsingTabs()
        {
            Create("\tcat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft_SpaceStartUsingTabs()
        {
            Create("    cat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft_TabStartFollowedBySpacesUsingTabs()
        {
            Create("\t    cat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("\t  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft_SpacesStartFollowedByTabFollowedBySpacesUsingTabs()
        {
            Create("    \t    cat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("\t\t  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft_SpacesStartFollowedByTabFollowedBySpacesUsingTabsWithModifiedTabStop()
        {
            Create("    \t    cat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _settings.SetupGet(x => x.TabStop).Returns(2);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("\t\t\t\tcat", _textView.GetLine(0).GetText());
        }
        [Test]
        public void ShiftLinesLeft_ShortSpacesStartFollowedByTabFollowedBySpacesUsingTabs()
        {
            Create("  \t    cat");
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual("\t  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesRight1()
        {
            Create("foo");
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("  foo", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ShiftLinesRight2()
        {
            Create("foo", " bar");
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("  foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("   bar", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        public void ShiftLinesRight3()
        {
            Create("foo", " bar");
            _textView.MoveCaretTo(_textBuffer.GetLineRange(1).Start.Position);
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("   bar", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        [Description("Blank lines need to expand")]
        public void ShiftLinesRight4()
        {
            Create("foo", "", "bar");
            _operations.ShiftLinesRight(3);
            Assert.AreEqual("  foo", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("  ", _textBuffer.GetLineRange(1).GetText());
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(2).GetText());
        }

        [Test]
        public void ShiftLinesRight_PreferEditorTabSetting()
        {
            Create("cat", "dog");
            _globalSettings.SetupGet(x => x.UseEditorTabSettings).Returns(true);
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(4);
            _editorOptions.Setup(x => x.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId)).Returns(false);
            _editorOptions.Setup(x => x.GetOptionValue(DefaultOptions.TabSizeOptionId)).Returns(4);
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("\tcat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesRight_NoExpandTab()
        {
            Create("cat", "dog");
            _globalSettings.SetupGet(x => x.UseEditorTabSettings).Returns(false);
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(4);
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("\tcat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesRight_NoExpandTabKeepSpacesWhenFewerThanTabStop()
        {
            Create("cat", "dog");
            _globalSettings.SetupGet(x => x.UseEditorTabSettings).Returns(false);
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(2);
            _globalSettings.SetupGet(x => x.TabStop).Returns(4);
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("  cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ShiftLinesRight_SpacesStartUsingTabs()
        {
            Create("  cat", "dog");
            _globalSettings.SetupGet(x => x.UseEditorTabSettings).Returns(false);
            _settings.SetupGet(x => x.ExpandTab).Returns(false);
            _settings.SetupGet(x => x.TabStop).Returns(2);
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("\t\tcat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ScrollLines1()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(1).End);
            _editorOperations.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.MoveCaretAndScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollLines2()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOperations.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.MoveCaretAndScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollLines3()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOperations.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.MoveCaretAndScrollLines(ScrollDirection.Down, 1);
            Assert.AreEqual(1, _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollPages1()
        {
            Create("");
            _editorOperations.Setup(x => x.ScrollPageUp()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Up, 1);
            _editorOperations.Verify();
        }

        [Test]
        public void ScrollPages2()
        {
            Create("");
            var count = 0;
            _editorOperations.Setup(x => x.ScrollPageUp()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Up, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ScrollPages3()
        {
            Create("");
            _editorOperations.Setup(x => x.ScrollPageDown()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Down, 1);
            _editorOperations.Verify();
        }

        [Test]
        public void ScrollPages4()
        {
            Create("");
            var count = 0;
            _editorOperations.Setup(x => x.ScrollPageDown()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Down, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void DeleteLines1()
        {
            Create("foo", "bar", "baz", "jaz");
            var span = _operations.DeleteLines(1);
            Assert.AreEqual("foo", span.GetText());
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual("bar", _textView.TextSnapshot.GetLineRange(1).GetText());
            Assert.AreEqual(4, _textView.TextSnapshot.LineCount);
        }

        [Test, Description("Caret position should not affect this operation")]
        public void DeleteLines2()
        {
            Create("foo", "bar", "baz", "jaz");
            _textView.MoveCaretTo(1);
            var span = _operations.DeleteLines(1);
            Assert.AreEqual("foo", span.GetText());
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual("bar", _textView.TextSnapshot.GetLineRange(1).GetText());
            Assert.AreEqual(4, _textView.TextSnapshot.LineCount);
        }

        [Test, Description("Delete past the end of the buffer should not crash")]
        public void DeleteLines3()
        {
            Create("foo", "bar", "baz", "jaz");
            _textView.MoveCaretTo(1);
            _operations.DeleteLines(3000);
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineRange(0).GetText());
        }

        [Test]
        public void DeleteLinesFromCursor1()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _textView.TextSnapshot;
            var span = _operations.DeleteLinesFromCursor(1);
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", span.GetText());
        }

        [Test]
        public void DeleteLinesFromCursor2()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _textView.TextSnapshot;
            var span = _operations.DeleteLinesFromCursor(2);
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine + "bar", span.GetText());
        }

        [Test]
        public void DeleteLinesFromCursor3()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _textView.TextSnapshot;
            _textView.MoveCaretTo(1);
            var span = _operations.DeleteLinesFromCursor(2);
            Assert.AreEqual("f", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("oo" + Environment.NewLine + "bar", span.GetText());
        }

        [Test]
        public void DeleteLinesIncludingLineBreak1()
        {
            Create("foo", "bar", "baz", "jaz");
            var span = _operations.DeleteLinesIncludingLineBreak(1);
            Assert.AreEqual("foo" + Environment.NewLine, span.GetText());
            Assert.AreEqual("bar", _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual(3, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak2()
        {
            Create("foo", "bar", "baz", "jaz");
            var span = _operations.DeleteLinesIncludingLineBreak(2);
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine, span.GetText());
            Assert.AreEqual("baz", _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.TextSnapshot.LineCount);
        }

        [Test]
        [Description("Deleting the last line should change the line count")]
        public void DeleteLinesIncludingLineBreak3()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            var span = _operations.DeleteLinesIncludingLineBreak(1);
            Assert.AreEqual(Environment.NewLine + "bar", span.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak4()
        {
            Create("foo");
            var span = _operations.DeleteLinesIncludingLineBreak(1);
            Assert.AreEqual("foo", span.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetText());
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor1()
        {
            Create("foo", "bar", "baz", "jaz");
            _textView.MoveCaretTo(1);
            var span = _operations.DeleteLinesIncludingLineBreakFromCursor(1);
            Assert.AreEqual("oo" + Environment.NewLine, span.GetText());
            Assert.AreEqual("fbar", _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual("baz", _textView.TextSnapshot.GetLineRange(1).GetText());
            Assert.AreEqual(3, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor2()
        {
            Create("foo", "bar", "baz", "jaz");
            _textView.MoveCaretTo(1);
            var span = _operations.DeleteLinesIncludingLineBreakFromCursor(2);
            Assert.AreEqual("oo" + Environment.NewLine + "bar" + Environment.NewLine, span.GetText());
            Assert.AreEqual("fbaz", _textView.TextSnapshot.GetLineRange(0).GetText());
            Assert.AreEqual("jaz", _textView.TextSnapshot.GetLineRange(1).GetText());
            Assert.AreEqual(2, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void ChangeLetterCase1()
        {
            Create("foo", "bar");
            _operations.ChangeLetterCase(_textBuffer.GetLineRange(0).Extent.ToEditSpan());
            Assert.AreEqual("FOO", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCase2()
        {
            Create("fOo", "bar");
            _operations.ChangeLetterCase(_textBuffer.GetLineRange(0).Extent.ToEditSpan());
            Assert.AreEqual("FoO", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCase3()
        {
            Create("fOo", "bar");
            _operations.ChangeLetterCase(_textBuffer.GetLineRange(0, 1).Extent.ToEditSpan());
            Assert.AreEqual("FoO", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("BAR", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        public void ChangeLetterCase4()
        {
            Create("f12o", "bar");
            _operations.ChangeLetterCase(_textBuffer.GetLineRange(0).Extent.ToEditSpan());
            Assert.AreEqual("F12O", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCaseToLower_Standard()
        {
            Create("FOO", "BAR");
            _operations.ChangeLetterCaseToLower(_textBuffer.GetLineRange(0).Extent);
            Assert.AreEqual("foo", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCaseToLower_NotFirstLine()
        {
            Create("FOO", "BAR");
            _operations.ChangeLetterCaseToLower(_textBuffer.GetLineRange(1).Extent);
            Assert.AreEqual("bar", _textBuffer.GetLineRange(1).GetText());
        }

        [Test]
        public void ChangeLetterCaseToLower_ExcludeNumbers()
        {
            Create("FoO123", "BAR");
            _operations.ChangeLetterCaseToLower(_textBuffer.GetLineRange(0).Extent);
            Assert.AreEqual("foo123", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCaseToUpper_ExcludeNumbers()
        {
            Create("foo123", "bar");
            _operations.ChangeLetterCaseToUpper(_textBuffer.GetLineRange(0).Extent);
            Assert.AreEqual("FOO123", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterCaseToUpper_ExcludeNumbersNotFirstLine()
        {
            Create("fOo123", "bar");
            _operations.ChangeLetterCaseToUpper(_textBuffer.GetLineRange(0).Extent);
            Assert.AreEqual("FOO123", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeLetterRot13_Standard()
        {
            Create("hello");
            _operations.ChangeLetterRot13(_textView.GetLine(0).Extent);
            Assert.AreEqual("uryyb", _textView.GetLine(0).GetText());
            _operations.ChangeLetterRot13(_textView.GetLine(0).Extent);
            Assert.AreEqual("hello", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Inclusive motions need to put the caret on End-1 in most cases.  See e as an example of why")]
        public void MoveCaretToMotionData1()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 1, 2),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData2()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 1),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData3()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 0),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData4()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3),
                false,
                MotionKind.Inclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test, Description("Exclusive motions should go to End")]
        public void MoveCaretToMotionData5()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 1, 2),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretToMotionData6()
        {
            Create("foo", "bar", "baz");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 1),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Motion to empty last line")]
        public void MoveCaretToMotionData7()
        {
            Create("foo", "bar", "");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length),
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(2, _textView.GetCaretPoint().GetContainingLine().LineNumber);
        }

        [Test]
        [Description("Need to respect the specified column")]
        public void MoveCaretToMotionData8()
        {
            Create("foo", "bar", "");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).Extent,
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                1);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(Tuple.Create(1, 1), SnapshotPointUtil.GetLineColumn(_textView.GetCaretPoint()));
        }

        [Test]
        [Description("Ignore column if it's past the end of the line")]
        public void MoveCaretToMotionData9()
        {
            Create("foo", "bar", "");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).Extent,
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                100);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(Tuple.Create(1, 3), SnapshotPointUtil.GetLineColumn(_textView.GetCaretPoint()));
        }

        [Test]
        [Description("Need to respect the specified column")]
        public void MoveCaretToMotionData10()
        {
            Create("foo", "bar", "");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).Extent,
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                0);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(Tuple.Create(1, 0), SnapshotPointUtil.GetLineColumn(_textView.GetCaretPoint()));
        }

        [Test]
        [Description("Reverse spans should move to the start of the span")]
        public void MoveCaretToMotionData11()
        {
            Create("dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).Extent,
                false,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                0);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(Tuple.Create(0, 0), SnapshotPointUtil.GetLineColumn(_textView.GetCaretPoint()));
        }

        [Test]
        [Description("Reverse spans should move to the start of the span and respect column")]
        public void MoveCaretToMotionData12()
        {
            Create("dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).Extent,
                false,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                2);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(Tuple.Create(0, 2), SnapshotPointUtil.GetLineColumn(_textView.GetCaretPoint()));
        }

        [Test]
        [Description("Exclusive spans going forward ending on a endline having a 0 column should position caret in the below line")]
        public void MoveCaretToMotionData13()
        {
            Create("dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0).ExtentIncludingLineBreak,
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                0);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("Exclusive spans going backward should go through normal movements")]
        public void MoveCaretToMotionData14()
        {
            Create("dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                false,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                0);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(_textBuffer.GetLine(0).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("Exclusive spans going forward ending on a endline having a 0 column and starting in the middle of a span checks")]
        public void MoveCaretToMotionData15()
        {
            Create("dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetSpan(1, _textBuffer.GetLine(1).EndIncludingLineBreak.Position),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                0);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("Used with the - motion")]
        public void MoveCaretToMotionData_ReverseLineWiseWithColumn()
        {
            Create(" dog", "cat", "bear");
            _editorOperations.Setup(x => x.ResetSelection());
            var data = VimUtil.CreateMotionData(
                span: _textView.GetLineRange(0, 1).ExtentIncludingLineBreak,
                isForward: false,
                operationKind: OperationKind.LineWise,
                column: 1);
            _operations.MoveCaretToMotionData(data);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
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

        [Test]
        public void ChangeSpan_WordSpan()
        {
            Create("foo  bar");
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetSpan(0, 3),
                isForward: true,
                isAnyWord: true,
                motionKind: MotionKind.Inclusive,
                operationKind: OperationKind.CharacterWise);
            _operations.ChangeSpan(data);
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeSpan_WordShouldSaveTrailingWhitespace()
        {
            Create("foo  bar");
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetSpan(0, 5),
                isForward: true,
                isAnyWord: true,
                motionKind: MotionKind.Inclusive,
                operationKind: OperationKind.LineWise);
            _operations.ChangeSpan(data);
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeSpan_NonWordShouldDeleteTrailingWhitespace()
        {
            Create("foo  bar");
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetSpan(0, 5),
                isForward: true,
                isAnyWord: false,
                motionKind: MotionKind.Inclusive,
                operationKind: OperationKind.LineWise);
            _operations.ChangeSpan(data);
            Assert.AreEqual("bar", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        public void ChangeSpan_LeaveWhitespaceIfBackward()
        {
            Create("cat dog tree");
            var data = VimUtil.CreateMotionData(
                _textBuffer.GetSpan(4, 4),
                false,
                MotionKind.Inclusive,
                OperationKind.CharacterWise);
            _operations.ChangeSpan(data);
            Assert.AreEqual("cat tree", _textBuffer.GetLineRange(0).GetText());
        }

        [Test]
        [Description("Delete of single line should update many registers")]
        public void UpdateRegisterForSpan1()
        {
            Create("foo bar");
            var span = _textView.GetLineRange(0).Extent;
            var reg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(
                reg,
                RegisterOperation.Delete,
                span,
                OperationKind.CharacterWise);
            AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_0), "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "foo bar", OperationKind.CharacterWise);
        }

        [Test]
        [Description("Yank of several lines")]
        public void UpdateRegisterForSpan2()
        {
            Create("foo bar");
            var span = _textView.GetLineRange(0).Extent;
            var reg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(
                reg,
                RegisterOperation.Yank,
                span,
                OperationKind.CharacterWise);
            AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_0), "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "", OperationKind.LineWise);
        }

        [Test]
        [Description("Numbered registers")]
        public void UpdateRegisterForSpan3()
        {
            Create("foo bar");
            var span1 = _textView.TextBuffer.GetSpan(0, 1);
            var span2 = _textView.TextBuffer.GetSpan(1, 1);
            var reg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(reg, RegisterOperation.Yank, span1, OperationKind.CharacterWise);
            _operations.UpdateRegisterForSpan(reg, RegisterOperation.Yank, span2, OperationKind.CharacterWise);
            AssertRegister(reg, "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_0), "o", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_1), "f", OperationKind.CharacterWise);
        }

        [Test]
        [Description("Small delete register")]
        public void UpdateRegisterForSpan4()
        {
            Create("foo", "bar");
            var span = _textView.GetLineRange(0).Extent;
            var reg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(reg, RegisterOperation.Delete, span, OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "foo", OperationKind.CharacterWise);
        }

        [Test]
        [Description("Small delete register doesn't update for multiple lines")]
        public void UpdateRegisterForSpan5()
        {
            Create("foo", "bar");
            var span = _textView.GetLineRange(0, 1).Extent;
            var reg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(reg, RegisterOperation.Delete, span, OperationKind.CharacterWise);
            AssertRegister(RegisterName.SmallDelete, "", OperationKind.LineWise);
        }

        [Test]
        [Description("Deleting to black hole register should not affect unnamed or others")]
        public void UpdateRegisterForSpan6()
        {
            Create("foo bar");
            var span = _textView.GetLineRange(0).Extent;
            var namedReg = _registerMap.GetRegister('c');
            _operations.UpdateRegisterForSpan(
                namedReg,
                RegisterOperation.Yank,
                span,
                OperationKind.CharacterWise);
            _operations.UpdateRegisterForSpan(
                _registerMap.GetRegister(RegisterName.Blackhole),
                RegisterOperation.Delete,
                span,
                OperationKind.CharacterWise);
            AssertRegister(namedReg, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.NewNumbered(NumberedRegister.Register_0), "foo bar", OperationKind.CharacterWise);
            AssertRegister(RegisterName.Blackhole, "", OperationKind.LineWise);
        }

        [Test, Description("Only once per line")]
        public void Substitute1()
        {
            Create("bar bar", "foo");
            _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.None);
            Assert.AreEqual("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Should run on every line in the span")]
        public void Substitute2()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 2))).Verifiable();
            _operations.Substitute("bar", "again", _textView.GetLineRange(0, 1), SubstituteFlags.None);
            Assert.AreEqual("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo again", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Replace all if the option is set")]
        public void Substitute3()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Ignore case")]
        public void Substitute4()
        {
            Create("bar bar", "foo bar");
            _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase);
            Assert.AreEqual("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute5()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute6()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("No matches")]
        public void Substitute7()
        {
            Create("bar bar", "foo bar");
            var pattern = "BAR";
            _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound(pattern))).Verifiable();
            _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase);
            _statusUtil.Verify();
        }

        [Test, Description("Invalid regex")]
        public void Substitute8()
        {
            Create("bar bar", "foo bar");
            var original = _textView.TextSnapshot;
            var pattern = "(foo";
            _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound(pattern))).Verifiable();
            _operations.Substitute(pattern, "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase);
            _statusUtil.Verify();
            Assert.AreSame(original, _textView.TextSnapshot);
        }

        [Test, Description("Report only shouldn't make any changes")]
        public void Substitute9()
        {
            Create("bar bar", "foo bar");
            var tss = _textView.TextSnapshot;
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.ReplaceAll | SubstituteFlags.ReportOnly);
            _statusUtil.Verify();
            Assert.AreSame(tss, _textView.TextSnapshot);
        }

        [Test, Description("No matches and report only")]
        public void Substitute10()
        {
            Create("bar bar", "foo bar");
            var tss = _textView.TextSnapshot;
            var pattern = "BAR";
            _operations.Substitute(pattern, "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase | SubstituteFlags.ReportOnly);
        }

        [Test]
        [Description("Across multiple lines one match per line should be processed")]
        public void Substitute11()
        {
            Create("cat", "bat");
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 2))).Verifiable();
            _operations.Substitute("a", "o", _textView.GetLineRange(0, 1), SubstituteFlags.None);
            Assert.AreEqual("cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bot", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Respect the magic flag")]
        public void Substitute12()
        {
            Create("cat", "bat");
            _globalSettings.SetupGet(x => x.Magic).Returns(false);
            _operations.Substitute(".", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Magic);
            Assert.AreEqual("bat", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Respect the nomagic flag")]
        public void Substitute13()
        {
            Create("cat.", "bat");
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _operations.Substitute(".", "s", _textView.GetLineRange(0, 0), SubstituteFlags.Nomagic);
            Assert.AreEqual("cats", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Don't error when the pattern is not found if SuppressErrors is passed")]
        public void Substitute14()
        {
            Create("cat", "bat");
            _operations.Substitute("z", "b", _textView.GetLineRange(0, 0), SubstituteFlags.SuppressError);
            _factory.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor1()
        {
            Create("  foo bar baz");
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            _statusUtil.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor2()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion(verify: true);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.Caret.Position.BufferPosition);
            _outlining.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor3()
        {
            Create("foo bar", "baz foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_textView.GetLine(1).Start.Add(4), _textView.Caret.Position.BufferPosition);
        }

        [Test, Description("No match shouldn't do anything")]
        public void MoveToNextOccuranceOfWordAtCursor4()
        {
            Create("fuz bar", "baz foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("With a count")]
        public void MoveToNextOccuranceOfWordAtCursor5()
        {
            Create("foo bar foo", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 3);
            Assert.AreEqual(_textView.GetLine(0).Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor6()
        {
            Create("foo bar baz", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor7()
        {
            Create("foo foobar baz", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test, Description("Moving to next occurance of a word should update the LastSearch")]
        public void MoveToNextOccuranceOfWordAtCursor8()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.Caret.Position.BufferPosition);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _vimData.LastSearchData.Text);
        }

        [Test, Description("When there is no word under the cursor, don't update the LastSearch")]
        public void MoveToNextOccuranceOfWordAtCursor9()
        {
            Create("  foo bar baz");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            _statusUtil.Verify();
            Assert.AreEqual(data, _vimData.LastSearchData);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor10()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor11()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor12()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 3);
            Assert.AreEqual(_textView.GetLine(2).Start.Position, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor13()
        {
            Create("foo", "foobar", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor14()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _vimData.LastSearchData.Text);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor15()
        {
            Create("    foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(data, _vimData.LastSearchData);
            _statusUtil.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor16()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _vimData.LastSearchData.Text);
            _outlining.Verify();
        }

        /// <summary>
        /// Regression test for issue 398.  When starting on something other
        /// than the first character make sure we don't jump over an extra 
        /// word
        /// </summary>
        [Test]
        public void MoveToNextOccuranceOfWordAtCursor_StartOnSecondChar()
        {
            Create("cat cat cat");
            AllowOutlineExpansion();
            _textView.MoveCaretTo(1);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch1()
        {
            Create("foo bar baz");
            var data = new SearchData(SearchText.NewPattern("beat"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound("beat"))).Verifiable();
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            _statusUtil.Verify();
        }

        [Test, Description("Should not start on the current word")]
        public void MoveToNextOccuranceOfLastSearch2()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch3()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _operations.MoveToNextOccuranceOfLastSearch(2, false);
            Assert.AreEqual(0, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch4()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.BackwardWithWrap, SearchOptions.None);
            _vimData.LastSearchData = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch5()
        {
            Create("foo bar", "foo");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.BackwardWithWrap, SearchOptions.None);
            AllowOutlineExpansion(verify: true);
            _vimData.LastSearchData = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _outlining.Verify();
        }

        [Test]
        public void GoToGlobalDeclaration1()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToGlobalDeclaration(_textView, "foo")).Returns(true).Verifiable();
            _operations.GoToGlobalDeclaration();
            _host.Verify();
        }

        [Test]
        public void GoToGlobalDeclaration2()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToGlobalDeclaration(_textView, "foo")).Returns(false).Verifiable();
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.GoToGlobalDeclaration();
            _host.Verify();
        }

        [Test]
        public void GoToLocalDeclaration1()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToLocalDeclaration(_textView, "foo")).Returns(true).Verifiable();
            _operations.GoToLocalDeclaration();
            _host.Verify();
        }

        [Test]
        public void GoToLocalDeclaration2()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToLocalDeclaration(_textView, "foo")).Returns(false).Verifiable();
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.GoToLocalDeclaration();
            _host.Verify();
        }

        [Test]
        public void GoToFile1()
        {
            Create("foo bar");
            _host.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            _host.Setup(x => x.LoadFileIntoExisting("foo", _textBuffer)).Returns(HostResult.Success).Verifiable();
            _operations.GoToFile();
            _host.Verify();
        }

        [Test]
        public void GoToFile2()
        {
            Create("foo bar");
            _host.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            _host.Setup(x => x.LoadFileIntoExisting("foo", _textBuffer)).Returns(HostResult.NewError("")).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_CantFindFile("foo"))).Verifiable();
            _operations.GoToFile();
            _statusUtil.Verify();
            _host.Verify();
        }

        [Test]
        public void InsertLineAbove_KeepCaretAtStartWithNoAutoIndent()
        {
            Create("foo");
            _operations.InsertLineAbove();
            var point = _textView.Caret.Position.VirtualBufferPosition;
            Assert.IsFalse(point.IsInVirtualSpace);
            Assert.AreEqual(0, point.Position.Position);
        }

        [Test]
        public void InsertLineAbove_MiddleOfLine()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(1).Start);
            _operations.InsertLineAbove();
            var point = _textView.Caret.Position.BufferPosition;
            Assert.AreEqual(1, point.GetContainingLine().LineNumber);
            Assert.AreEqual(String.Empty, point.GetContainingLine().GetText());
        }

        [Test]
        [Description("Happens when a language service formats text")]
        public void InsertLineAbove_EditInTheMiddle()
        {
            Create("foo bar", "baz");

            bool didEdit = false;

            _textView.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _textView.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _operations.InsertLineAbove();
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a ", line.GetText());
        }

        [Test]
        public void InsertLineAbove_ShouldKeepIndentWhenAutoIndentSet()
        {
            Create("  cat", "dog");
            _settings.SetupGet(x => x.AutoIndent).Returns(true);
            _operations.InsertLineAbove();
            Assert.AreEqual(2, _textView.Caret.Position.VirtualSpaces);
        }

        [Test]
        public void InsertLineBelow_InMiddleOfLine()
        {
            Create("foo", "bar", "baz");
            var newLine = _operations.InsertLineBelow();
            Assert.AreEqual(1, newLine.LineNumber);
            Assert.AreEqual(String.Empty, newLine.GetText());

        }

        [Test]
        public void InsertLineBelow_AtEndOfBuffer()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(_textView.TextSnapshot.LineCount - 1).Start);
            var newLine = _operations.InsertLineBelow();
            Assert.IsTrue(String.IsNullOrEmpty(newLine.GetText()));
        }

        [Test]
        public void InsertLineBelow_Misc()
        {
            Create("foo bar", "baz");
            _operations.InsertLineBelow();
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(Environment.NewLine, line.GetLineBreakText());
            Assert.AreEqual(2, line.LineBreakLength);
            Assert.AreEqual("foo bar", line.GetText());
            Assert.AreEqual("foo bar" + Environment.NewLine, line.GetTextIncludingLineBreak());

            line = buffer.CurrentSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(Environment.NewLine, line.GetLineBreakText());
            Assert.AreEqual(2, line.LineBreakLength);
            Assert.AreEqual(String.Empty, line.GetText());
            Assert.AreEqual(String.Empty + Environment.NewLine, line.GetTextIncludingLineBreak());

            line = buffer.CurrentSnapshot.GetLineFromLineNumber(2);
            Assert.AreEqual(String.Empty, line.GetLineBreakText());
            Assert.AreEqual(0, line.LineBreakLength);
            Assert.AreEqual("baz", line.GetText());
            Assert.AreEqual("baz", line.GetTextIncludingLineBreak());
        }

        [Test]
        public void InsertLineBelow_PreferEditorIndent()
        {
            Create("cat", "dog");
            _globalSettings.SetupGet(x => x.UseEditorIndent).Returns(true);
            _smartIndent.Setup(x => x.GetDesiredIndentation(_textView, It.IsAny<ITextSnapshotLine>())).Returns(8);
            _operations.InsertLineBelow();
            Assert.AreEqual(8, _textView.Caret.Position.VirtualSpaces);
        }

        [Test]
        public void InsertLineBelow_RevertToVimIndentIfEditorIndentFails()
        {
            Create("  cat", "  dog");
            _globalSettings.SetupGet(x => x.UseEditorIndent).Returns(true);
            _settings.SetupGet(x => x.AutoIndent).Returns(true);
            _smartIndent.Setup(x => x.GetDesiredIndentation(_textView, It.IsAny<ITextSnapshotLine>())).Returns((int?)null);
            _operations.InsertLineBelow();
            Assert.AreEqual(2, _textView.Caret.Position.VirtualSpaces);
        }

        [Test]
        [Description("Happens when a language service formats text")]
        public void InsertLineBelow_EditsInTheMiddle()
        {
            Create("foo bar", "baz");

            bool didEdit = false;

            _textView.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _textView.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _operations.InsertLineBelow();
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a foo bar", line.GetText());
        }

        [Test]
        public void InsertLineBelow_KeepIndentWhenAutoIndentSet()
        {
            Create("  cat", "dog");
            _settings.SetupGet(x => x.AutoIndent).Returns(true);
            _operations.InsertLineBelow();
            Assert.AreEqual("", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
        }
    }
}
