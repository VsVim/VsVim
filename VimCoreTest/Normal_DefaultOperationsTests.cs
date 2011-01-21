using System;
using System.Collections.Generic;
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
using Vim.Modes.Normal;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class Normal_DefaultOperationsTests
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _textView;
        private Mock<IEditorOptions> _bufferOptions;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IEditorOptions> _options;
        private Mock<IRegisterMap> _registerMap;
        private ISearchService _searchService;
        private IVimData _vimData;
        private Mock<IStatusUtil> _statusUtil;

        private void Create(params string[] lines)
        {
            Create(null, null, lines);
        }

        private void Create(
            IEditorOperations editorOpts = null,
            ITextStructureNavigator baseNav = null,
            params string[] lines)
        {
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            if (editorOpts == null)
            {
                var tuple = EditorUtil.CreateViewAndOperations(lines);
                _textView = tuple.Item1;
                editorOpts = tuple.Item2;
            }
            else
            {
                _textView = EditorUtil.CreateView(lines);
            }

            var editorOptions = EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(_textView);
            baseNav = baseNav ?? (new Mock<ITextStructureNavigator>(MockBehavior.Strict)).Object;
            var nav = TssUtil.CreateTextStructureNavigator(WordKind.NormalWord, baseNav);
            _vimData = new VimData();
            _bufferOptions = new Mock<IEditorOptions>(MockBehavior.Strict);
            _bufferOptions.Setup(x => x.GetOptionValue(DefaultOptions.TabSizeOptionId)).Returns(4);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _globalSettings.SetupGet(x => x.IgnoreCase).Returns(true);
            _globalSettings.SetupGet(x => x.SmartCase).Returns(false);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _options = new Mock<IEditorOptions>(MockBehavior.Strict);
            _options.Setup(x => x.GetOptionValue<int>(It.IsAny<string>())).Throws(new ArgumentException());
            _options.Setup(x => x.GetOptionValue<int>(It.IsAny<EditorOptionKey<int>>())).Throws(new ArgumentException());
            _options.Setup(x => x.IsOptionDefined<int>(It.IsAny<EditorOptionKey<int>>(), false)).Returns(true);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _searchService = new SearchService(EditorUtil.FactoryService.TextSearchService, _globalSettings.Object);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _undoRedoOperations = new Mock<IUndoRedoOperations>(MockBehavior.Strict);
            _undoRedoOperations.Setup(x => x.CreateUndoTransaction(It.IsAny<string>())).Returns<string>(name => new Vim.UndoTransaction(FSharpOption.Create(EditorUtil.GetUndoHistory(_textView.TextBuffer).CreateTransaction(name))));
            _registerMap = MockObjectFactory.CreateRegisterMap();

            var data = new OperationsData(
                vimData: _vimData,
                vimHost: _host.Object,
                textView: _textView,
                editorOperations: editorOpts,
                outliningManager: FSharpOption.Create(_outlining.Object),
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                registerMap: _registerMap.Object,
                keyMap: null,
                undoRedoOperations: _undoRedoOperations.Object,
                editorOptions: _options.Object,
                navigator: null,
                foldManager: null,
                searchService: _searchService,
                smartIndentationService: EditorUtil.FactoryService.SmartIndentationService);

            _operationsRaw = new DefaultOperations(data);
            _operations = _operationsRaw;
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

        [Test]
        public void DeleteCharacterAtCursor1()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            var span = _operations.DeleteCharacterAtCursor(1);
            Assert.AreEqual("oo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void DeleteCharacterAtCursor2()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            var span = _operations.DeleteCharacterAtCursor(1);
            Assert.AreEqual("oo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void DeleteCharacterAtCursor3()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            var span = _operations.DeleteCharacterAtCursor(2);
            Assert.AreEqual("o", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void DeleteCharacterAtCursor_LastCharOnLine()
        {
            Create("cat", "dog");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            _textView.MoveCaretTo(2);
            var span = _operations.DeleteCharacterAtCursor(1);
            Assert.AreEqual("t", span.GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteCharacterBeforeCursor1()
        {
            Create("foo");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            var span = _operations.DeleteCharacterBeforeCursor(1);
            Assert.AreEqual("oo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test, Description("Don't delete past the current line")]
        public void DeleteCharacterBeforeCursor2()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(1).Start);
            var span = _operations.DeleteCharacterBeforeCursor(1);
            Assert.AreEqual("bar", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("foo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor3()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            var span = _operations.DeleteCharacterBeforeCursor(2);
            Assert.AreEqual("o", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor4()
        {
            Create("foo");
            var span = _operations.DeleteCharacterBeforeCursor(2);
            Assert.AreEqual("foo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }
        [Test]
        public void ReplaceChar1()
        {
            Create("foo");
            _operations.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 1);
            Assert.AreEqual("boo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar2()
        {
            Create("foo");
            _operations.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 2);
            Assert.AreEqual("bbo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar3()
        {
            Create("foo");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _operations.ReplaceChar(KeyInputUtil.EnterKey, 1);
            var tss = _textView.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("o", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar4()
        {
            Create("food");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            Assert.IsTrue(_operations.ReplaceChar(KeyInputUtil.EnterKey, 2));
            var tss = _textView.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("d", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar5()
        {
            Create("food");
            var tss = _textView.TextSnapshot;
            Assert.IsFalse(_operations.ReplaceChar(KeyInputUtil.CharToKeyInput('c'), 200));
            Assert.AreSame(tss, _textView.TextSnapshot);
        }

        [Test, Description("Edit should not cause the cursor to move")]
        public void ReplaceChar6()
        {
            Create("foo");
            Assert.IsTrue(_operations.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 1));
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }



        [Test]
        public void JumpNext1()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MoveNext()).Callback(() => { count++; }).Returns(false);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void JumpNext2()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MoveNext()).Callback(() => { count++; }).Returns(() => count == 1);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(2);
            _host.Verify();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void JumpNext3()
        {
            Create("foo", "bar");
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption<SnapshotPoint>.None);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
        }

        [Test]
        public void JumpNext4()
        {
            Create("foo", "bar");
            AllowOutlineExpansion(verify: true);
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_textView.TextSnapshot, 1)));
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
            _outlining.Verify();
        }

        [Test]
        public void JumpNext5()
        {
            Create("foo", "bar");
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(point));
            _host.Setup(x => x.NavigateTo(It.IsAny<VirtualSnapshotPoint>())).Returns(true).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
        }

        [Test]
        public void JumpPrevious1()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MovePrevious()).Callback(() => { count++; }).Returns(false);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void JumpPrevious2()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MovePrevious()).Callback(() => { count++; }).Returns(() => count == 1);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(2);
            _host.Verify();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void JumpPrevious3()
        {
            Create("foo", "bar");
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption<SnapshotPoint>.None);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
        }

        [Test]
        public void JumpPrevious4()
        {
            Create("foo", "bar");
            AllowOutlineExpansion(verify: true);
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_textView.TextSnapshot, 1)));
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
            _outlining.Verify();
        }

        [Test]
        public void JumpPrevious5()
        {
            Create("foo", "bar");
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(point));
            _host.Setup(x => x.NavigateTo(It.IsAny<VirtualSnapshotPoint>())).Returns(true).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
        }

        [Test]
        public void GoToMatch1()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToMatch()).Returns(true).Verifiable();
            Assert.IsTrue(_operations.GoToMatch());
            _host.Verify();
        }

        [Test]
        public void GoToMatch2()
        {
            Create("foo bar");
            _host.Setup(x => x.GoToMatch()).Returns(false).Verifiable();
            Assert.IsFalse(_operations.GoToMatch());
            _host.Verify();
        }

        [Test]
        public void GoToLineOrFirst1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption.Create(1));
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption.Create(42));
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption<int>.None);
            Assert.AreEqual(0, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test, Description("0 goes to the last line surprisingly and not the first")]
        public void GoToLineOrLast1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(0));
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(1));
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption<int>.None);
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void InsertText1()
        {
            Create("foo");
            _operations.InsertText("a", 1);
            Assert.AreEqual("afoo", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText2()
        {
            Create("bar");
            _operations.InsertText("a", 3);
            Assert.AreEqual("aaabar", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText3()
        {
            Create("bar");
            _textView.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual("bheyar", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("Caret needs to be moved to the last letter of the insert")]
        public void InsertText4()
        {
            Create("bar");
            _textView.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor1()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(1);
            Assert.AreEqual("Bar", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor2()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(2);
            Assert.AreEqual("BAr", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor3()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(300);
            Assert.AreEqual("BAR", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretForAppend1()
        {
            Create("foo", "bar");
            _operations.MoveCaretForAppend();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretForAppend2()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(0).End.Subtract(1));
            _operations.MoveCaretForAppend();
            Assert.AreEqual(_textView.GetLine(0).End, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveCaretForAppend3()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(0).End);
            _operations.MoveCaretForAppend();
            Assert.AreEqual(_textView.GetLine(0).End, _textView.GetCaretPoint());
        }

        [Test]
        public void MoveCaretForAppend4()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(SnapshotUtil.GetEndPoint(_textView.TextSnapshot));
            _operations.MoveCaretForAppend();
            Assert.AreEqual(SnapshotUtil.GetEndPoint(_textView.TextSnapshot), _textView.GetCaretPoint());
        }

    }
}
