using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Normal;
using VimCoreTest.Utils;
using Moq;
using Vim;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;

namespace VimCoreTest
{
    [TestFixture]
    public class Normal_DefaultOperationsTests
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _view;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;

        private void Create(params string[] lines)
        {
            Create(null, null, null, lines);
        }

        private void Create(
            IEditorOperations editorOpts = null,
            ITextSearchService searchService = null,
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

            searchService = searchService ?? EditorUtil.FactoryService.textSearchService;
            baseNav = baseNav ?? (new Mock<ITextStructureNavigator>(MockBehavior.Strict)).Object;
            var nav = TssUtil.CreateTextStructureNavigator(WordKind.NormalWord, baseNav);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _operationsRaw = new DefaultOperations(_view, editorOpts, _host.Object, _settings.Object, nav, searchService, _jumpList.Object);
            _operations = _operationsRaw;
        }

        [Test]
        public void DeleteCharacterAtCursor1()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
        }

        [Test]
        public void DeleteCharacterAtCursor2()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteCharacterAtCursor3()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(2, reg);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }
        [Test]
        public void DeleteCharacterBeforeCursor1()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var reg = new Register('c');
            _operations.DeleteCharacterBeforeCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
        }

        [Test, Description("Don't delete past the current line")]
        public void DeleteCharacterBeforeCursor2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _operations.DeleteCharacterBeforeCursor(1, new Register('c'));
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor3()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            var reg = new Register('c');
            _operations.DeleteCharacterBeforeCursor(2, reg);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", reg.StringValue);
        }

        [Test]
        public void DeleteCharacterBeforeCursor4()
        {
            Create("foo");
            _operations.DeleteCharacterBeforeCursor(2, new Register('c'));
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }
        [Test]
        public void ReplaceChar1()
        {
            Create("foo");
            _operations.ReplaceChar(InputUtil.CharToKeyInput('b'), 1);
            Assert.AreEqual("boo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar2()
        {
            Create("foo");
            _operations.ReplaceChar(InputUtil.CharToKeyInput('b'), 2);
            Assert.AreEqual("bbo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar3()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.ReplaceChar(InputUtil.VimKeyToKeyInput(VimKey.LineFeedKey), 1);
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
            Assert.IsTrue(_operations.ReplaceChar(InputUtil.VimKeyToKeyInput(VimKey.EnterKey), 2));
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
            Assert.IsFalse(_operations.ReplaceChar(InputUtil.CharToKeyInput('c'), 200));
            Assert.AreSame(tss, _view.TextSnapshot);
        }

        [Test, Description("Edit should not cause the cursor to move")]
        public void ReplaceChar6()
        {
            Create("foo");
            Assert.IsTrue(_operations.ReplaceChar(InputUtil.CharToKeyInput('u'), 1));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void YankLines1()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.YankLines(1, reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void YankLines2()
        {
            Create("foo", "bar", "jazz");
            var reg = new Register('c');
            _operations.YankLines(2, reg);
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span.GetText(), reg.StringValue);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void PasteAfter1()
        {
            Create("foo bar");
            _operations.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, false);
            Assert.AreEqual("fheyoo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Paste at end of buffer shouldn't crash")]
        public void PasteAfter2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(TssUtil.GetEndPoint(_view.TextSnapshot));
            _operations.PasteAfterCursor("hello", 1, OperationKind.CharacterWise, false);
            Assert.AreEqual("barhello", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            Create("foo", String.Empty);
            _view.Caret.MoveTo(TssUtil.GetEndPoint(_view.TextSnapshot));
            _operations.PasteAfterCursor("bar", 1, OperationKind.CharacterWise, false);
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Pasting a linewise motion should occur on the next line")]
        public void PasteAfter4()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.PasteAfterCursor("baz" + Environment.NewLine, 1, OperationKind.LineWise, moveCursorToEnd: false);
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Pasting a linewise motion should move the caret to the start of the next line")]
        public void PasteAfter7()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.PasteAfterCursor("baz" + Environment.NewLine, 1, OperationKind.LineWise, false);
            var pos = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(_view.TextSnapshot.GetLineFromLineNumber(1).Start, pos);
        }

        [Test]
        public void PasteAfter8()
        {
            Create("foo");
            _operations.PasteAfterCursor("hey", 2, OperationKind.CharacterWise, false);
            Assert.AreEqual("fheyheyoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteAfter9()
        {
            Create("foo");
            _operations.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true);
            Assert.AreEqual("fheyoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void PasteAfter10()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.PasteAfterCursor("hey", 1, OperationKind.CharacterWise, true);
            Assert.AreEqual("foohey", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteBefore1()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 1, false);
            Assert.AreEqual("heyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteBefore2()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 2, false);
            Assert.AreEqual("heyheyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }


        [Test]
        public void PasteBefore3()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 1, true);
            Assert.AreEqual("heyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(3, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void PasteBefore4()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.PasteBeforeCursor("hey", 1, true);
            Assert.AreEqual("foohey", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void InsertLiveAbove1()
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
        public void ScrollUp1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).End);
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.Scroll(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollUp2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.Scroll(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollDown1()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.Scroll(ScrollDirection.Down, 1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void DeleteLines1()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteLines(2, reg);
            Assert.AreEqual(tss.GetLineSpan(0, 1).GetText(), reg.StringValue);
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLines2()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start);
            var reg = new Register('c');
            _operations.DeleteLines(2, reg);
            Assert.AreEqual(tss.GetLineSpan(1, 2).GetText(), reg.StringValue);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLines3()
        {
            Create("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start);
            var reg = new Register('c');
            _operations.DeleteLines(3000, reg);
            Assert.AreEqual(tss.GetLineSpan(1, tss.LineCount - 1).GetText(), reg.StringValue);
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
        public void MoveToNextOccuranceOfWordAtCursor1()
        {
            Create("  foo bar baz");
            _host.Setup(x => x.UpdateStatus(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(true, 1);
            _host.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor2()
        {
            Create("foo bar", "foo");
            _operations.MoveToNextOccuranceOfWordAtCursor(true, 1);
            Assert.AreEqual(_view.GetLine(1).Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor3()
        {
            Create("foo bar", "baz foo");
            _operations.MoveToNextOccuranceOfWordAtCursor(false, 1);
            Assert.AreEqual(_view.GetLine(1).Start.Add(4), _view.Caret.Position.BufferPosition);
        }

        [Test, Description("No match shouldn't do anything")]
        public void MoveToNextOccuranceOfWordAtCursor4()
        {
            Create("fuz bar", "baz foo");
            _operations.MoveToNextOccuranceOfWordAtCursor(false, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("With a count")]
        public void MoveToNextOccuranceOfWordAtCursor5()
        {
            Create("foo bar foo", "foo");
            _operations.MoveToNextOccuranceOfWordAtCursor(false, 3);
            Assert.AreEqual(_view.GetLine(1).Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor6()
        {
            Create("foo bar baz", "foo");
            _view.MoveCaretTo(_view.GetLine(1).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(true, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor7()
        {
            Create("foo foobar baz", "foo");
            _operations.MoveToNextOccuranceOfWordAtCursor(true, 1);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
        }

        [Test]
        public void MoveToPreviousOccuranceOfWordAtCursor1()
        {
            Create("foo bar", "foo");
            _view.MoveCaretTo(_view.GetLine(1).Start.Position);
            _operations.MoveToPreviousOccuranceOfWordAtCursor(false, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToPreviousOccuranceOfWordAtCursor2()
        {
            Create("foo bar", "again foo", "foo");
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToPreviousOccuranceOfWordAtCursor(false, 3);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToPreviousOccuranceOfWordAtCursor3()
        {
            Create("foo bar", "again foo", "foo");
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToPreviousOccuranceOfWordAtCursor(true, 4);
            Assert.AreEqual(_view.GetLine(2).Start.Position, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToPreviousOccuranceOfWordAtCursor4()
        {
            Create("foo", "foobar", "foo");
            _view.MoveCaretTo(_view.GetLine(2).Start);
            _operations.MoveToPreviousOccuranceOfWordAtCursor(true, 1);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
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
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_view.TextSnapshot, 1)));
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
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
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_view.TextSnapshot, 1)));
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
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

    }
}
