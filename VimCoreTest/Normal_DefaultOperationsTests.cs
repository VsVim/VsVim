using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Normal;
using VimCore.Test.Utils;
using Moq;
using Vim;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using Vim.Modes;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using VimCore.Test.Mock;

namespace VimCore.Test
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
        private Mock<IIncrementalSearch> _search;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
            
        private ISearchService _searchService;
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

            baseNav = baseNav ?? (new Mock<ITextStructureNavigator>(MockBehavior.Strict)).Object;
            var nav = TssUtil.CreateTextStructureNavigator(WordKind.NormalWord, baseNav);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _searchService = new SearchService(EditorUtil.FactoryService.textSearchService, _globalSettings.Object);
            _search = new Mock<IIncrementalSearch>(MockBehavior.Strict);
            _search.SetupGet(x => x.SearchService).Returns(_searchService);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _undoRedoOperations = new Mock<IUndoRedoOperations>(MockBehavior.Strict);
            _operationsRaw = new DefaultOperations(_view, editorOpts, _outlining.Object, _host.Object, _statusUtil.Object, _settings.Object, nav, _jumpList.Object, _search.Object, _undoRedoOperations.Object);
            _operations = _operationsRaw;
        }

        private void AllowOutlineExpansion(bool verify=false)
        {
            var res = 
                _outlining
                    .Setup(x => x.ExpandAll(It.IsAny<SnapshotSpan>(), It.IsAny<Predicate<ICollapsed>>()))
                    .Returns<IEnumerable<ICollapsible>>(null);
            if ( verify )
            {
                res.Verifiable();
            }
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
            _operations.ReplaceChar(InputUtil.VimKeyToKeyInput(VimKey.EnterKey), 1);
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
            _view.Caret.MoveTo(SnapshotUtil.GetEndPoint(_view.TextSnapshot));
            _operations.PasteAfterCursor("hello", 1, OperationKind.CharacterWise, false);
            Assert.AreEqual("barhello", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            Create("foo", String.Empty);
            _view.Caret.MoveTo(SnapshotUtil.GetEndPoint(_view.TextSnapshot));
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

        [Test, Description("Verify LineWise PasteAfterCursor places the caret at the beginning of non-whitespace.")]
        public void PasteAfter11()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.PasteAfterCursor("  hey" + Environment.NewLine, 1, OperationKind.LineWise, false);
            Assert.AreEqual("  hey", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            var position = _view.Caret.Position.BufferPosition;
            var line = position.GetContainingLine();
            Assert.AreEqual(2, position.Position - line.Start);
        }

        [Test, Description("Verify linewise paste at the end of the buffer works")]
        public void PasteAfter12()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.GetLineSpan(1).End);
            _operations.PasteAfterCursor("hey", 1, OperationKind.LineWise, false);
            Assert.AreEqual("hey", _view.GetLineSpan(2).GetText());
            Assert.AreEqual(_view.GetCaretPoint(), _view.GetLineSpan(2).Start);
        }

        [Test]
        public void PasteBefore1()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, false);
            Assert.AreEqual("heyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void PasteBefore2()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 2, OperationKind.CharacterWise, false);
            Assert.AreEqual("heyheyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }


        [Test]
        public void PasteBefore3()
        {
            Create("foo");
            _operations.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, true);
            Assert.AreEqual("heyfoo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(3, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void PasteBefore4()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.PasteBeforeCursor("hey", 1, OperationKind.CharacterWise, true);
            Assert.AreEqual("foohey", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Verify LineWise PasteBeforeCursor places the caret at the beginning of non-whitespace.")]
        public void PasteBefore5()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _operations.PasteBeforeCursor("  hey" + Environment.NewLine, 8, OperationKind.LineWise, false);
            Assert.AreEqual("  hey", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            var position = _view.Caret.Position.BufferPosition;
            var line = position.GetContainingLine();
            Assert.AreEqual(2, position.Position - line.Start);
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
            Assert.AreEqual(_view.GetLine(1).Start, _view.Caret.Position.BufferPosition);
            _outlining.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor3()
        {
            Create("foo bar", "baz foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_view.GetLine(1).Start.Add(4), _view.Caret.Position.BufferPosition);
        }

        [Test, Description("No match shouldn't do anything")]
        public void MoveToNextOccuranceOfWordAtCursor4()
        {
            Create("fuz bar", "baz foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("With a count")]
        public void MoveToNextOccuranceOfWordAtCursor5()
        {
            Create("foo bar foo", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 3);
            Assert.AreEqual(_view.GetLine(0).Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor6()
        {
            Create("foo bar baz", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(1).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor7()
        {
            Create("foo foobar baz", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
        }

        [Test, Description("Moving to next occurance of a word should update the LastSearch")]
        public void MoveToNextOccuranceOfWordAtCursor8()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            Assert.AreEqual(_view.GetLine(1).Start, _view.Caret.Position.BufferPosition);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _searchService.LastSearch.Text);
        }

        [Test, Description("When there is no word under the cursor, don't update the LastSearch")]
        public void MoveToNextOccuranceOfWordAtCursor9()
        {
            Create("  foo bar baz");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.ForwardWithWrap, 1);
            _statusUtil.Verify();
            Assert.AreEqual(data, _searchService.LastSearch);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor10()
        {
            Create("foo bar", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(1).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor11()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor12()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 3);
            Assert.AreEqual(_view.GetLine(2).Start.Position, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor13()
        {
            Create("foo", "foobar", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(2).Start);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(0, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor14()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _searchService.LastSearch.Text);
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor15()
        {
            Create("    foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_NoWordUnderCursor)).Verifiable();
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 1);
            Assert.AreEqual(data, _searchService.LastSearch);
            _statusUtil.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfWordAtCursor16()
        {
            Create("foo bar", "again foo", "foo");
            AllowOutlineExpansion();
            _view.MoveCaretTo(_view.GetLine(2).Start.Position);
            _operations.MoveToNextOccuranceOfWordAtCursor(SearchKind.BackwardWithWrap, 2);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(SearchText.NewWholeWord("foo"), _searchService.LastSearch.Text);
            _outlining.Verify();
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch1()
        {
            Create("foo bar baz");
            var data = new SearchData(SearchText.NewPattern("beat"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_PatternNotFound("beat"))).Verifiable();
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            _statusUtil.Verify();
        }

        [Test, Description("Should not start on the current word")]
        public void MoveToNextOccuranceOfLastSearch2()
        {
            Create("foo bar","foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch3()
        {
            Create("foo bar","foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _operations.MoveToNextOccuranceOfLastSearch(2, false);
            Assert.AreEqual(0, _view.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch4()
        {
            Create("foo bar","foo");
            AllowOutlineExpansion();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.BackwardWithWrap, SearchOptions.None);
            _searchService.LastSearch = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
        }

        [Test]
        public void MoveToNextOccuranceOfLastSearch5()
        {
            Create("foo bar","foo");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.BackwardWithWrap, SearchOptions.None);
            AllowOutlineExpansion(verify:true);
            _searchService.LastSearch = data;
            _operations.MoveToNextOccuranceOfLastSearch(1, false);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
            _outlining.Verify();
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
            AllowOutlineExpansion(verify:true);
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
            _operations.InsertText("hey",1);
            Assert.AreEqual("bheyar", _view.TextSnapshot.GetText());
        }

        [Test]
        public void ChangeLetterCaseAtCursor1()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(1);
            Assert.AreEqual("Bar", _view.GetLineSpan(0).GetText());
            Assert.AreEqual(1, _view.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor2()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(2);
            Assert.AreEqual("BAr", _view.GetLineSpan(0).GetText());
            Assert.AreEqual(2, _view.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeLetterCaseAtCursor3()
        {
            Create("bar", "baz");
            _operations.ChangeLetterCaseAtCursor(300);
            Assert.AreEqual("BAR", _view.GetLineSpan(0).GetText());
            Assert.AreEqual(2, _view.GetCaretPoint().Position);
        }

        [Test]
        public void MoveToNextOccuranceOfCharAtCursor1()
        {
            Create("dog kicked the ball");

        }
    }
}
