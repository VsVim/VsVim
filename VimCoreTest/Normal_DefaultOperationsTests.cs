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
        private ITextView _view;
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
                _view = tuple.Item1;
                editorOpts = tuple.Item2;
            }
            else
            {
                _view = EditorUtil.CreateView(lines);
            }

            var editorOptions = EditorUtil.FactoryService.editorOptionsFactory.GetOptions(_view);
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
            _searchService = new SearchService(EditorUtil.FactoryService.textSearchService, _globalSettings.Object);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _undoRedoOperations = new Mock<IUndoRedoOperations>(MockBehavior.Strict);
            _undoRedoOperations.Setup(x => x.CreateUndoTransaction(It.IsAny<string>())).Returns<string>(name => new Vim.UndoTransaction(FSharpOption.Create(EditorUtil.GetUndoHistory(_view.TextBuffer).CreateTransaction(name))));
            _registerMap = MockObjectFactory.CreateRegisterMap();

            var data = new OperationsData(
                vimData: _vimData,
                vimHost: _host.Object,
                textView: _view,
                editorOperations: editorOpts,
                outliningManager: _outlining.Object,
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                registerMap: _registerMap.Object,
                keyMap: null,
                undoRedoOperations: _undoRedoOperations.Object,
                editorOptions: _options.Object,
                navigator: null,
                foldManager: null,
                searchService: _searchService);

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
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void DeleteCharacterAtCursor2()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            var span = _operations.DeleteCharacterAtCursor(1);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void DeleteCharacterAtCursor3()
        {
            Create("foo", "bar");
            _globalSettings.SetupGet(x => x.IsVirtualEditOneMore).Returns(false);
            var span = _operations.DeleteCharacterAtCursor(2);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor1()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var span = _operations.DeleteCharacterBeforeCursor(1);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", span.GetText());
        }

        [Test, Description("Don't delete past the current line")]
        public void DeleteCharacterBeforeCursor2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            var span = _operations.DeleteCharacterBeforeCursor(1);
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor3()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            var span = _operations.DeleteCharacterBeforeCursor(2);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor4()
        {
            Create("foo");
            var span = _operations.DeleteCharacterBeforeCursor(2);
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }
        [Test]
        public void ReplaceChar1()
        {
            Create("foo");
            _operations.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 1);
            Assert.AreEqual("boo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar2()
        {
            Create("foo");
            _operations.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 2);
            Assert.AreEqual("bbo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar3()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.ReplaceChar(KeyInputUtil.EnterKey, 1);
            var tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("o", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar4()
        {
            Create("food");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            Assert.IsTrue(_operations.ReplaceChar(KeyInputUtil.EnterKey, 2));
            var tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("d", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar5()
        {
            Create("food");
            var tss = _view.TextSnapshot;
            Assert.IsFalse(_operations.ReplaceChar(KeyInputUtil.CharToKeyInput('c'), 200));
            Assert.AreSame(tss, _view.TextSnapshot);
        }

        [Test, Description("Edit should not cause the cursor to move")]
        public void ReplaceChar6()
        {
            Create("foo");
            Assert.IsTrue(_operations.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 1));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void InsertLineAbove1()
        {
            Create("foo");
            _operations.InsertLineAbove();
            var point = _view.Caret.Position.VirtualBufferPosition;
            Assert.IsFalse(point.IsInVirtualSpace);
            Assert.AreEqual(0, point.Position.Position);
        }

        [Test]
        public void InsertLineAbove2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _operations.InsertLineAbove();
            var point = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(1, point.GetContainingLine().LineNumber);
            Assert.AreEqual(String.Empty, point.GetContainingLine().GetText());
        }

        [Test]
        public void InsertLineAbove3()
        {
            // Verify that insert line below behaves properly in the face of edits happening in response to our edit
            Create("foo bar", "baz");

            bool didEdit = false;

            _view.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _view.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _operations.InsertLineAbove();
            var buffer = _view.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a ", line.GetText());
        }

        [Test]
        public void InsertLineBelow()
        {
            Create("foo", "bar", "baz");
            var newLine = _operations.InsertLineBelow();
            Assert.AreEqual(1, newLine.LineNumber);
            Assert.AreEqual(String.Empty, newLine.GetText());

        }

        [Test, Description("New line at end of buffer")]
        public void InsertLineBelow2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(_view.TextSnapshot.LineCount - 1).Start);
            var newLine = _operations.InsertLineBelow();
            Assert.IsTrue(String.IsNullOrEmpty(newLine.GetText()));
        }

        [Test, Description("Make sure the new is actually a newline")]
        public void InsertLineBelow3()
        {
            Create("foo");
            var newLine = _operations.InsertLineBelow();
            Assert.AreEqual(Environment.NewLine, _view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetLineBreakText());
            Assert.AreEqual(String.Empty, _view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).GetLineBreakText());
        }

        [Test, Description("Make sure line inserted in the middle has correct text")]
        public void InsertLineBelow4()
        {
            Create("foo", "bar");
            _operations.InsertLineBelow();
            var count = _view.TextSnapshot.LineCount;
            foreach (var line in _view.TextSnapshot.Lines.Take(count - 1))
            {
                Assert.AreEqual(Environment.NewLine, line.GetLineBreakText());
            }
        }

        [Test]
        public void InsertLineBelow5()
        {
            Create("foo bar", "baz");
            _operations.InsertLineBelow();
            var buffer = _view.TextBuffer;
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
        public void InsertLineBelow6()
        {
            // Verify that insert line below behaves properly in the face of edits happening in response to our edit
            Create("foo bar", "baz");

            bool didEdit = false;

            _view.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _view.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _operations.InsertLineBelow();
            var buffer = _view.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a foo bar", line.GetText());
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
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_view.TextSnapshot, 1)));
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
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
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_view.TextSnapshot, 1)));
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
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
            Assert.AreEqual(1, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption.Create(42));
            Assert.AreEqual(2, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption<int>.None);
            Assert.AreEqual(0, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test, Description("0 goes to the last line surprisingly and not the first")]
        public void GoToLineOrLast1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(0));
            Assert.AreEqual(2, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(1));
            Assert.AreEqual(1, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption<int>.None);
            Assert.AreEqual(2, _view.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void InsertText1()
        {
            Create("foo");
            _operations.InsertText("a", 1);
            Assert.AreEqual("afoo", _view.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText2()
        {
            Create("bar");
            _operations.InsertText("a", 3);
            Assert.AreEqual("aaabar", _view.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText3()
        {
            Create("bar");
            _view.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual("bheyar", _view.TextSnapshot.GetText());
        }

        [Test]
        [Description("Caret needs to be moved to the last letter of the insert")]
        public void InsertText4()
        {
            Create("bar");
            _view.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual(3, _view.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor1()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(1);
            Assert.AreEqual("Bar", _view.GetLineRange(0).GetText());
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor2()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(2);
            Assert.AreEqual("BAr", _view.GetLineRange(0).GetText());
            Assert.AreEqual(2, _view.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor3()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(300);
            Assert.AreEqual("BAR", _view.GetLineRange(0).GetText());
            Assert.AreEqual(2, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretForAppend1()
        {
            Create("foo", "bar");
            _operations.MoveCaretForAppend();
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveCaretForAppend2()
        {
            Create("foo", "bar");
            _view.MoveCaretTo(_view.GetLine(0).End.Subtract(1));
            _operations.MoveCaretForAppend();
            Assert.AreEqual(_view.GetLine(0).End, _view.GetCaretPoint());
        }

        [Test]
        public void MoveCaretForAppend3()
        {
            Create("foo", "bar");
            _view.MoveCaretTo(_view.GetLine(0).End);
            _operations.MoveCaretForAppend();
            Assert.AreEqual(_view.GetLine(0).End, _view.GetCaretPoint());
        }

        [Test]
        public void MoveCaretForAppend4()
        {
            Create("foo", "bar");
            _view.MoveCaretTo(SnapshotUtil.GetEndPoint(_view.TextSnapshot));
            _operations.MoveCaretForAppend();
            Assert.AreEqual(SnapshotUtil.GetEndPoint(_view.TextSnapshot), _view.GetCaretPoint());
        }

    }
}
