using System.Threading;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class VisualModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IVimBufferData _vimBufferData;
        private IVimTextBuffer _vimTextBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IRegisterMap _registerMap;
        private IVimGlobalSettings _globalSettings;
        private TestableSynchronizationContext _context;

        internal Register TestRegister
        {
            get { return _vimBuffer.RegisterMap.GetRegister('c'); }
        }

        protected virtual void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBufferData = _vimBuffer.VimBufferData;
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _registerMap = _vimBuffer.RegisterMap;
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            Assert.True(_context.IsEmpty);

            // Need to make sure it's focused so macro recording will work
            ((MockVimHost)_vimBuffer.Vim.VimHost).FocusedTextView = _textView;
        }

        protected void EnterMode(SnapshotSpan span)
        {
            var characterSpan = CharacterSpan.CreateForSpan(span);
            var visualSelection = VisualSelection.NewCharacter(characterSpan, Path.Forward);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.False(_context.IsEmpty);
            _context.RunAll();
            Assert.True(_context.IsEmpty);
        }

        protected void EnterMode(ModeKind kind, SnapshotSpan span)
        {
            EnterMode(span);
            _vimBuffer.SwitchMode(kind, ModeArgument.None);
        }

        protected void EnterBlock(BlockSpan blockSpan)
        {
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.False(_context.IsEmpty);
            _context.RunAll();
            Assert.True(_context.IsEmpty);
            _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
        }

        public sealed class ExclusiveSelection : VisualModeIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.Selection = "exclusive";
            }

            /// <summary>
            /// The caret position should be on the next character for a move right
            /// </summary>
            [Fact]
            public void CaretPosition_Right()
            {
                Create("the dog");
                _vimBuffer.Process("vl");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The caret position should be on the start of the next word after leaving visual mode
            /// </summary>
            [Fact]
            public void CaretPosition_Word()
            {
                Create("the dog");
                _vimBuffer.Process("vw");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure the 'e' motion still goes one character extra during a line wise movement
            /// </summary>
            [Fact]
            public void CaretPosition_EndOfWordLineWise()
            {
                Create("the dog. the cat");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("Ve");
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'e' motion should result in a selection that encompasses the entire word
            /// </summary>
            [Fact]
            public void Delete_EndOfWord()
            {
                Create("the dog. cat");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("vex");
                Assert.Equal("dog", UnnamedRegister.StringValue);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'e' motion should result in a selection that encompasses the entire word
            /// </summary>
            [Fact]
            public void Delete_EndOfWord_Block()
            {
                Create("the dog. end", "the cat. end", "the fish. end");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                _vimBuffer.Process("jex");
                Assert.Equal("the . end", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the . end", _textBuffer.GetLine(1).GetText());
                Assert.Equal("the fish. end", _textBuffer.GetLine(2).GetText());
            }

            /// <summary>
            /// The 'w' motion should result in a selection that encompasses the entire word
            /// </summary>
            [Fact]
            public void Delete_Word()
            {
                Create("the dog. cat");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("vwx");
                Assert.Equal("dog", UnnamedRegister.StringValue);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The $ movement should put the caret past the end of the line
            /// </summary>
            [Fact]
            public void MoveEndOfLine_Dollar()
            {
                Create("cat", "dog");
                _vimBuffer.Process("v$");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'l' movement should put the caret past the end of the line 
            /// </summary>
            [Fact]
            public void MoveEndOfLine_Right()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vlll");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The entire word should be selected 
            /// </summary>
            [Fact]
            public void InnerWord()
            {
                Create("cat   dog");
                _vimBuffer.Process("viw");
                Assert.Equal("cat", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The entire word plus the trailing white space should be selected
            /// </summary>
            [Fact]
            public void AllWord()
            {
                Create("cat   dog");
                _vimBuffer.Process("vaw");
                Assert.Equal("cat   ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class BlockInsert : VisualModeIntegrationTest
        {
            /// <summary>
            /// The block insert should add the text to every column
            /// </summary>
            [Fact]
            public void Simple()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
                Assert.Equal("the dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the cat", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// The caret should be positioned at the start of the block span when the insertion
            /// starts
            /// </summary>
            [Fact]
            public void CaretPosition()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>jl<S-i>");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The block insert shouldn't add text to any of the columns which didn't extend into 
            /// the original selection
            /// </summary>
            [Fact]
            public void EmptyColumn()
            {
                Create("dog", "", "fish");
                _vimBuffer.ProcessNotation("l<C-q>jjl<S-i> the <Esc>");
                Assert.Equal("d the og", _textBuffer.GetLine(0).GetText());
                Assert.Equal("", _textBuffer.GetLine(1).GetText());
                Assert.Equal("f the ish", _textBuffer.GetLine(2).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The undo of a block insert should undo all of the inserts
            /// </summary>
            [Fact]
            public void Undo()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
                Assert.Equal("the dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the cat", _textBuffer.GetLine(1).GetText());
                _vimBuffer.Process('u');
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Delete actions aren't repeated
            /// </summary>
            [Fact]
            public void DontRepeatDelete()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>j<S-i><Del><Esc>");
                Assert.Equal("og", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
            }
        }

        public sealed class BlockChange : VisualModeIntegrationTest
        {
            /// <summary>
            /// The block insert should add the text to every column
            /// </summary>
            [Fact]
            public void Simple()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>jcthe <Esc>");
                Assert.Equal("the og", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the at", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure an undo of a block edit goes back to the original text and replaces
            /// the cursor at the start of the block
            /// </summary>
            [Fact]
            public void Undo()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>jcthe <Esc>u");
                Assert.Equal(
                    new[] { "dog", "cat", "fish" },
                    _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void RenameFunction()
            {
                Create("foo()", "foo()");
                _vimBuffer.ProcessNotation("<C-q>jllcbar<Esc>");
                Assert.Equal(
                    new[] { "bar()", "bar()" },
                    _textBuffer.GetLines());
            }
        }

        public sealed class Move : VisualModeIntegrationTest
        {
            /// <summary>
            /// Jump to a mark and make sure that the selection correctly updates
            /// </summary>
            [Fact]
            public void JumpMarkLine_Character()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.MarkMap.SetLocalMark('b', _vimBufferData, 1, 1);
                _vimBuffer.Process("v'b");
                Assert.Equal("at\r\nd", _textView.GetSelectionSpan().GetText());
            }

            /// <summary>
            /// Jump to a mark and make sure that the selection correctly updates
            /// </summary>
            [Fact]
            public void JumpMark_Character()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.MarkMap.SetLocalMark('b', _vimBufferData, 1, 1);
                _vimBuffer.Process("v`b");
                Assert.Equal("at\r\ndo", _textView.GetSelectionSpan().GetText());
            }
        }

        public sealed class Misc : VisualModeIntegrationTest
        {
            /// <summary>
            /// When changing a line wise selection one blank line should be left remaining in the ITextBuffer
            /// </summary>
            [Fact]
            public void Change_LineWise()
            {
                Create("cat", "  dog", "  bear", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(1, 2).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("c");
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("", _textView.GetLine(1).GetText());
                Assert.Equal("tree", _textView.GetLine(2).GetText());
                Assert.Equal(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// When changing a word we just delete it all and put the caret at the start of the deleted
            /// selection
            /// </summary>
            [Fact]
            public void Change_Word()
            {
                Create("cat chases the ball");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 4));
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("c");
                Assert.Equal("chases the ball", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure we handle the virtual spaces properly here.  The 'C' command should leave the caret
            /// in virtual space due to the previous indent and escape should cause the caret to jump back to 
            /// real spaces when leaving insert mode
            /// </summary>
            [Fact]
            public void ChangeLineSelection_VirtualSpaceHandling()
            {
                Create("  cat", "dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 2, 2));
                _vimBuffer.Process('C');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.False(_textView.GetCaretVirtualPoint().IsInVirtualSpace);
            }

            /// <summary>
            /// When an entire line is selected in character wise mode and then deleted
            /// it should not be a line delete but instead delete the contents of the 
            /// line.
            /// </summary>
            [Fact]
            public void Delete_CharacterWise_LineContents()
            {
                Create("cat", "dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 3));
                _vimBuffer.Process("x");
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// If the character wise selection extents into the line break then the 
            /// entire line should be deleted
            /// </summary>
            [Fact]
            public void Delete_CharacterWise_LineContentsFromBreak()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).ExtentIncludingLineBreak);
                _vimBuffer.Process("x");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// The 'e' motion should select up to and including the end of the word
            ///
            /// https://github.com/jaredpar/VsVim/issues/568
            /// </summary>
            [Fact]
            public void Delete_EndOfWordMotion()
            {
                Create("ThisIsALongWord. ThisIsAnotherLongWord!");
                _vimBuffer.Process("vex");
                Assert.Equal(". ThisIsAnotherLongWord!", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Verify that Shift-V enters Visual Line Mode
            /// </summary>
            [Fact]
            public void EnterVisualLine()
            {
                Create("hello", "world");
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
            }

            [Fact]
            public void Repeat1()
            {
                Create("dog again", "cat again", "chicken");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
                _vimBuffer.Process(">.");
                Assert.Equal("    dog again", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void Repeat2()
            {
                Create("dog again", "cat again", "chicken");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
                _vimBuffer.Process(">..");
                Assert.Equal("      dog again", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void ResetCaretFromShiftLeft1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLineRange(0, 1).Extent);
                _vimBuffer.Process("<");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ResetCaretFromShiftLeft2()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLineRange(0, 1).Extent);
                _vimBuffer.Process("<");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ResetCaretFromYank1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                _vimBuffer.Process("y");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Moving the caret which resets the selection should go to normal mode
            /// </summary>
            [Fact]
            public void SelectionChange1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(
                    new SnapshotSpan(_textView.GetLine(1).Start, 0),
                    false);
                _context.RunAll();
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Moving the caret which resets the selection should go visual if there is still a selection
            /// </summary>
            [Fact]
            public void SelectionChange2()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(
                    new SnapshotSpan(_textView.GetLine(1).Start, 1),
                    false);
                _context.RunAll();
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure we reset the span we need
            /// </summary>
            [Fact]
            public void SelectionChange3()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLine(0).Extent);
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(_textView.GetLine(1).Extent, false);
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('y'));
                _context.RunAll();
                Assert.Equal("  world", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// Make sure we reset the span we need
            /// </summary>
            [Fact]
            public void SelectionChange4()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLine(0).Extent);
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.SelectAndMoveCaret(new SnapshotSpan(_textView.GetLine(1).Start, 3));
                _context.RunAll();
                _vimBuffer.Process("ly");
                Assert.Equal("  wo", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// Make sure the CTRL-Q command causes the block selection to start out as a single width
            /// column
            /// </summary>
            [Fact]
            public void Select_Block_InitialState()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("<C-Q>");
                Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 1, 1);
                Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
            }

            /// <summary>
            /// Make sure the CTRL-Q command causes the block selection to start out as a single width 
            /// column from places other than the start of the document
            /// </summary>
            [Fact]
            public void Select_Block_InitialNonStartPoint()
            {
                Create("big cats", "big dogs", "big trees");
                var point = _textBuffer.GetPointInLine(1, 3);
                _textView.MoveCaretTo(point);
                _vimBuffer.ProcessNotation("<C-Q>");
                Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                var blockSpan = new BlockSpan(point, 1, 1);
                Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
            }

            /// <summary>
            /// A left movement in block selection should move the selection to the left
            /// </summary>
            [Fact]
            public void Select_Block_Backwards()
            {
                Create("big cats", "big dogs");
                _textView.MoveCaretTo(2);
                _vimBuffer.ProcessNotation("<C-Q>jh");
                Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                var blockSpan = new BlockSpan(_textView.GetPoint(1), 2, 2);
                Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
            }

            /// <summary>
            /// When selection is exclusive there should still be a single column selected in block
            /// mode even if the original width is 1
            /// </summary>
            [Fact]
            public void Select_Exclusive_OneWidthBlock()
            {
                Create("the dog", "the cat");
                _textView.MoveCaretTo(1);
                _globalSettings.Selection = "exclusive";
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                _vimBuffer.Process('j');
                var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
                Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
                Assert.Equal(_textView.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When selection is exclusive block selection should shrink by one in width
            /// </summary>
            [Fact]
            public void Select_Exclusive_TwoWidthBlock()
            {
                Create("the dog", "the cat");
                _textView.MoveCaretTo(1);
                _globalSettings.Selection = "exclusive";
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                _vimBuffer.Process("jl");
                var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
                Assert.Equal(blockSpan, _textView.GetSelectionBlockSpan());
                Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that LastVisualSelection is set to the SnapshotSpan before the shift right
            /// command is executed
            /// </summary>
            [Fact]
            public void ShiftLinesRight_LastVisualSelection()
            {
                Create("cat", "dog", "fish");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, _textView.GetLine(1).Start.Add(1)));
                _vimBuffer.Process('>');
                var visualSelection = VisualSelection.NewCharacter(
                    new CharacterSpan(_textView.GetLine(0).Start, 2, 1),
                    Path.Forward);
                Assert.True(_vimTextBuffer.LastVisualSelection.IsSome());
                Assert.Equal(visualSelection, _vimTextBuffer.LastVisualSelection.Value);
            }

            /// <summary>
            /// Even though a text span is selected, substitute should operate on the line
            /// </summary>
            [Fact]
            public void Substitute1()
            {
                Create("the boy hit the cat", "bat");
                EnterMode(new SnapshotSpan(_textView.TextSnapshot, 0, 2));
                _vimBuffer.Process(":s/a/o", enter: true);
                Assert.Equal("the boy hit the cot", _textView.GetLine(0).GetText());
                Assert.Equal("bat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Muliline selection should cause a replace per line
            /// </summary>
            [Fact]
            public void Substitute2()
            {
                Create("the boy hit the cat", "bat");
                EnterMode(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(":s/a/o", enter: true);
                Assert.Equal("the boy hit the cot", _textView.GetLine(0).GetText());
                Assert.Equal("bot", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Switching to command mode shouldn't clear the selection
            /// </summary>
            [Fact]
            public void Switch_ToCommandShouldNotClearSelection()
            {
                Create("cat", "dog", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(":");
                Assert.False(_textView.GetSelectionSpan().IsEmpty);
            }

            /// <summary>
            /// Switching to normal mode should clear the selection
            /// </summary>
            [Fact]
            public void Switch_ToNormalShouldClearSelection()
            {
                Create("cat", "dog", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(VimKey.Escape);
                Assert.True(_textView.GetSelectionSpan().IsEmpty);
            }

            [Fact]
            public void Handle_D_BlockMode()
            {
                Create("dog", "cat", "tree");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                _vimBuffer.Process("D");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("c", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void IncrementalSearch_LineModeShouldSelectFullLine()
            {
                Create("dog", "cat", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process("/c");
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            [Fact]
            public void IncrementalSearch_LineModeShouldSelectFullLineAcrossBlanks()
            {
                Create("dog", "", "cat", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process("/ca");
                Assert.Equal(_textView.GetLineRange(0, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            [Fact]
            public void IncrementalSearch_CharModeShouldExtendToSearchResult()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, 1));
                _vimBuffer.Process("/o");
                Assert.Equal(new SnapshotSpan(_textView.GetLine(0).Start, 2), _textView.GetSelectionSpan());
            }

            /// <summary>
            /// An incremental search operation shouldn't change the location of the caret until the search is
            /// completed
            /// </summary>
            [Fact]
            public void IncrementalSearch_DontChangeCaret()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("v/do");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that Escape will properly exit the incremental search and return us to the previous
            /// visual mode state (with the same selection)
            /// </summary>
            [Fact]
            public void IncrementalSearch_EscapeShouldExitSearch()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.ProcessNotation("vl/dog<Esc>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimBuffer.IncrementalSearch.InSearch);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
            }

            /// <summary>
            /// Make sure that enter completes the search which includes updating the caret
            /// </summary>
            [Fact]
            public void IncrementalSearch_EnterShouldCompleteSearch()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.ProcessNotation("vl/dog<Enter>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimBuffer.IncrementalSearch.InSearch);
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a character span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Character()
            {
                Create("dogs", "cats");

                var visualSpan = VimUtil.CreateVisualSpanCharacter(_textBuffer.GetSpan(1, 2));
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive));
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a line span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Line()
            {
                Create("dogs", "cats", "fish");

                var lineRange = _textView.GetLineRange(0, 1);
                var visualSelection = VisualSelection.NewLine(lineRange, Path.Forward, 1);
                _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive));
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a block span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Block()
            {
                Create("dogs", "cats", "fish");

                var blockSpan = _textView.GetBlockSpan(1, 2, 0, 2);
                var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomLeft);
                _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive));
            }

            /// <summary>
            /// Record a macro which delets selected text.  When the macro is played back it should
            /// just run the delete against unselected text.  In other words it's just the raw keystrokes
            /// which are saved not the selection state
            /// </summary>
            [Fact]
            public void Macro_RecordDeleteSelectedText()
            {
                Create("the cat chased the dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                _vimBuffer.Process("qcxq");
                Assert.Equal(" cat chased the dog", _textView.GetLine(0).GetText());
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("@c");
                Assert.Equal(" at chased the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Run the macro to delete the selected text
            /// </summary>
            [Fact]
            public void Macro_RunDeleteSelectedText()
            {
                Create("the cat chased the dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                TestRegister.UpdateValue("x");
                _vimBuffer.Process("@c");
                Assert.Equal(" cat chased the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When the final line of the ITextBuffer is an empty line make sure that we can
            /// move up off of it when in Visual Line Mode.  
            /// 
            /// Issue #769
            /// </summary>
            [Fact]
            public void Move_Line_FromBottom()
            {
                Create("cat", "dog", "");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("Vk");
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            /// <summary>
            /// Make sure that we can use 'j' to go over an empty line in Visual Character 
            /// mode
            /// 
            /// Issue #758
            /// </summary>
            [Fact]
            public void Move_Character_OverEmptyLine()
            {
                Create("cat", "", "dog");
                _vimBuffer.Process("vjj");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Test the movement of the caret over a shorter line and then back to a line long
            /// enough
            /// </summary>
            [Fact]
            public void Move_Block_OverShortLine()
            {
                Create("really long line", "short", "really long line");
                _textView.MoveCaretTo(7);
                _vimBuffer.ProcessNotation("<C-v>lll");
                Assert.Equal("long", _textView.Selection.SelectedSpans[0].GetText());
                _vimBuffer.ProcessNotation("jj");
                var spans = _textView.Selection.SelectedSpans;
                Assert.Equal(3, spans.Count);
                Assert.Equal("long", spans[0].GetText());
                Assert.Equal("", spans[1].GetText());
                Assert.Equal("long", spans[2].GetText());
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithSingleCharacterWise()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("dcatg", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                Assert.Equal("o", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithSingleCharacterWiseAndCaretMove()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dcatg", _textView.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                Assert.Equal("o", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the inserted line
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithLineWise()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal("g", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the first line after the inserted
            /// lines
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithLineWiseAndCaretMove()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal("g", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the start of the first line in the
            /// block 
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithBlock()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbat", _textView.GetLine(1).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned after the line character in the last 
            /// line of the inserted block
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When doing a put over selection the text being deleted should be put into
            /// the unnamed register.
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_NamedRegisters()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                _registerMap.GetRegister('c').UpdateValue("pig");
                _vimBuffer.Process("\"cp");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// When doing a put over selection the text being deleted should be put into
            /// the unnamed register.  If the put came from the unnamed register then the 
            /// original put value is overwritten
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_UnnamedRegisters()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                UnnamedRegister.UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithCharcterWise()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithCharacterWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithLineWise()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithLineWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted value
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithBlock()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("aa", _textView.GetLine(0).GetText());
                Assert.Equal("bb", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the first character after the inserted
            /// text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("aa", _textView.GetLine(0).GetText());
                Assert.Equal("bb", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted value
            /// </summary>
            [Fact]
            public void PutOver_Block_WithCharacterWise()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("dfishg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Character should be positioned after the last character after the inserted
            /// text
            /// </summary>
            [Fact]
            public void PutOver_Block_WithCharacterWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dfishg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Character should be positioned at the start of the inserted line
            /// </summary>
            [Fact]
            public void PutOver_Block_WithLineWise()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("dg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal("fish", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Caret should be positioned at the start of the line which follows the
            /// inserted lines
            /// </summary>
            [Fact]
            public void PutOver_Block_WithLineWiseAndCaretMove()
            {
                Create("dog", "cat", "bear");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal("fish", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted string
            /// from the block
            /// </summary>
            [Fact]
            public void PutOver_Block_WithBlock()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbt", _textView.GetLine(1).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned at the first character after the last inserted
            /// charecter of the last string in the block
            /// </summary>
            [Fact]
            public void PutOver_Block_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbt", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            [Fact]
            public void PutOver_Legacy1()
            {
                Create("dog", "cat", "bear", "tree");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 0, 2));
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("pigg", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void PutOver_Legacy2()
            {
                Create("dog", "cat", "bear", "tree");
                var span = new SnapshotSpan(
                    _textView.GetLine(0).Start.Add(1),
                    _textView.GetLine(1).Start.Add(2));
                EnterMode(ModeKind.VisualCharacter, span);
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("dpigt", _textView.GetLine(0).GetText());
                Assert.Equal("bear", _textView.GetLine(1).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void PutBefore_Legacy1()
            {
                Create("dog", "cat", "bear", "tree");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("P");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Put with indent commands are another odd ball item in Vim.  It's the one put command
            /// which doesn't delete the selection when putting the text into the buffer.  Instead 
            /// it just continues on in visual mode after the put
            /// </summary>
            [Fact]
            public void PutAfterWithIndent_VisualLine()
            {
                Create("  dog", "  cat", "bear");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("bear", OperationKind.LineWise);
                _vimBuffer.Process("]p");
                Assert.Equal("  dog", _textView.GetLine(0).GetText());
                Assert.Equal("  bear", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Simple inner word selection on visual mode
            /// </summary>
            [Fact]
            public void TextObject_InnerWord()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("viw");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When a 'iw' text selection occurs and extends the selection backwards it should reset
            /// the visual caret start point.  This can be demonstrated jumping back and forth between
            /// character and line mode
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_ResetVisualStartPoint()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("viwVv");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simple inner word selection from the middle of a word.  Should still select the entire
            /// word
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromMiddle()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("viw");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This behavior isn't documented.  But if iw begins on a single white space character 
            /// then repeated iw shouldn't change anything.  It should select the single space and 
            /// go from there
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromSingleWhiteSpace()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process('v');
                for (var i = 0; i < 10; i++)
                {
                    _vimBuffer.Process("iw");
                    Assert.Equal(" ", _textView.GetSelectionSpan().GetText());
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }
            }

            /// <summary>
            /// From a non-single white space the inner word motion should select
            /// the entire white space
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromMultipleWhiteSpace()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("viw");
                Assert.Equal("  ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The non initial selection from white space should extend to the 
            /// next word
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_MultipleWhiteSpace_Second()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("viwiw");
                Assert.Equal("  dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simple all word selection
            /// </summary>
            [Fact]
            public void TextObject_AllWord()
            {
                Create("cat dog fish");
                _vimBuffer.Process("vaw");
                Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Unlike the 'iw' motion the 'aw' motion doesn't have truly odd behavior from
            /// a single white space
            /// </summary>
            [Fact]
            public void TextObject_AllWord_FromSingleWhiteSpace()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("vaw");
                Assert.Equal(" dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Ensure the ab motion includes the parens and puts the caret on the last 
            /// character
            /// </summary>
            [Fact]
            public void TextObject_AllParen_MiddleOfWord()
            {
                Create("cat (dog) fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vab");
                Assert.Equal("(dog)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Unlike non-block selections multiple calls to ab won't extend the selection
            /// to a sibling block
            /// </summary>
            [Fact]
            public void TextObject_AllParen_Multiple()
            {
                Create("cat (dog) (bear)");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vabababab");
                Assert.Equal("(dog)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Text object selections will extend to outer blocks
            /// </summary>
            [Fact]
            public void TextObject_AllParen_ExpandOutward()
            {
                Create("cat (fo(bad)od) bear");
                _textView.MoveCaretTo(9);
                _vimBuffer.Process("vab");
                Assert.Equal("(bad)", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ab");
                Assert.Equal("(fo(bad)od)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If we've already selected the inner block at the caret then move outward 
            /// and select the containing block
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_ExpandOutward()
            {
                Create("a (fo(tree)od) b");
                _textView.MoveCaretTo(7);
                _vimBuffer.Process("vib");
                Assert.Equal("tree", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ib");
                Assert.Equal("fo(tree)od", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If the entire inner block is not yet selected then go ahead and select it 
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_ExpandToFullBlock()
            {
                Create("a (fo(tree)od) b");
                _textView.MoveCaretTo(8);
                _vimBuffer.Process("vl");
                Assert.Equal("ee", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ib");
            }

            /// <summary>
            /// Ensure the ib motion excludes the parens and puts the caret on the last 
            /// character
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_MiddleOfWord()
            {
                Create("cat (dog) fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vib");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// All white space and the following word should be selecetd
            /// </summary>
            [Fact]
            public void TextObject_AllWord_FromMultipleWhiteSpace()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("vaw");
                Assert.Equal("  dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The yank selection command should exit visual mode after the operation
            /// </summary>
            [Fact]
            public void YankSelection_ShouldExitVisualMode()
            {
                Create("cat", "dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
                _vimBuffer.Process("y");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }

            /// <summary>
            /// Ensure that after yanking and leaving Visual Mode that the proper value is
            /// maintained for LastVisualSelection.  It should be the selection before the command
            /// was executed
            /// </summary>
            [Fact]
            public void YankSelection_LastVisualSelection()
            {
                Create("cat", "dog", "fish");
                var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
                EnterMode(ModeKind.VisualLine, span);
                _vimBuffer.Process('y');
                Assert.True(_vimTextBuffer.LastVisualSelection.IsSome());
                Assert.Equal(span, _vimTextBuffer.LastVisualSelection.Value.VisualSpan.EditSpan.OverarchingSpan);
            }

            /// <summary>
            /// The yank line selection command should exit visual mode after the operation
            /// </summary>
            [Fact]
            public void YankLineSelection_ShouldExitVisualMode()
            {
                Create("cat", "dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
                _vimBuffer.Process("Y");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.True(_textView.Selection.IsEmpty);
            }
        }
    }
}
