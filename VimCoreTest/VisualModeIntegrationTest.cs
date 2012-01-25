using System.Threading;
using EditorUtils.UnitTest;
using EditorUtils.UnitTest.Utils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class VisualModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IVimTextBuffer _vimTextBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IRegisterMap _registerMap;
        private IVimGlobalSettings _globalSettings;
        private TestableSynchronizationContext _context;

        internal Register UnnamedRegister
        {
            get { return _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed); }
        }

        internal Register TestRegister
        {
            get { return _vimBuffer.RegisterMap.GetRegister('c'); }
        }

        public void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _registerMap = _vimBuffer.RegisterMap;
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            Assert.IsTrue(_context.IsEmpty);

            // Need to make sure it's focused so macro recording will work
            ((MockVimHost)_vimBuffer.Vim.VimHost).FocusedTextView = _textView;
        }

        private void EnterMode(SnapshotSpan span)
        {
            var characterSpan = CharacterSpan.CreateForSpan(span);
            var visualSelection = VisualSelection.NewCharacter(characterSpan, Path.Forward);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
        }

        private void EnterMode(ModeKind kind, SnapshotSpan span)
        {
            EnterMode(span);
            _vimBuffer.SwitchMode(kind, ModeArgument.None);
        }

        private void EnterBlock(BlockSpan blockSpan)
        {
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
            _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
        }

        /// <summary>
        /// The block insert should add the text to every column
        /// </summary>
        [Test]
        public void BlockInsert_Simple()
        {
            Create("dog", "cat", "fish");
            _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
            Assert.AreEqual("the dog", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("the cat", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// The caret should be positioned at the start of the block span when the insertion
        /// starts
        /// </summary>
        [Test]
        public void BlockInsert_CaretPosition()
        {
            Create("dog", "cat", "fish");
            _vimBuffer.ProcessNotation("<C-q>jl<S-i>");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// The block insert shouldn't add text to any of the columns which didn't extend into 
        /// the original selection
        /// </summary>
        [Test]
        public void BlockInsert_EmptyColumn()
        {
            Create("dog", "", "fish");
            _vimBuffer.ProcessNotation("l<C-q>jjl<S-i> the <Esc>");
            Assert.AreEqual("d the og", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("", _textBuffer.GetLine(1).GetText());
            Assert.AreEqual("f the ish", _textBuffer.GetLine(2).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The undo of a block insert should undo all of the inserts
        /// </summary>
        [Test]
        public void BlockInsert_Undo()
        {
            Create("dog", "cat", "fish");
            _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
            Assert.AreEqual("the dog", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("the cat", _textBuffer.GetLine(1).GetText());
            _vimBuffer.Process('u');
            Assert.AreEqual("dog", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("cat", _textBuffer.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete actions aren't repeated
        /// </summary>
        [Test]
        public void BlockInsert_DontRepeatDelete()
        {
            Create("dog", "cat", "fish");
            _vimBuffer.ProcessNotation("<C-q>j<S-i><Del><Esc>");
            Assert.AreEqual("og", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// When changing a line wise selection one blank line should be left remaining in the ITextBuffer
        /// </summary>
        [Test]
        public void Change_LineWise()
        {
            Create("cat", "  dog", "  bear", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(1, 2).ExtentIncludingLineBreak);
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("c");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// When changing a word we just delete it all and put the caret at the start of the deleted
        /// selection
        /// </summary>
        [Test]
        public void Change_Word()
        {
            Create("cat chases the ball");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 4));
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("c");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Make sure we handle the virtual spaces properly here.  The 'C' command should leave the caret
        /// in virtual space due to the previous indent and escape should cause the caret to jump back to 
        /// real spaces when leaving insert mode
        /// </summary>
        [Test]
        public void ChangeLineSelection_VirtualSpaceHandling()
        {
            Create("  cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 2, 2));
            _vimBuffer.Process('C');
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.IsFalse(_textView.GetCaretVirtualPoint().IsInVirtualSpace);
        }

        /// <summary>
        /// When an entire line is selected in character wise mode and then deleted
        /// it should not be a line delete but instead delete the contents of the 
        /// line.
        /// </summary>
        [Test]
        public void Delete_CharacterWise_LineContents()
        {
            Create("cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
            _vimBuffer.Process("x");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// If the character wise selection extents into the line break then the 
        /// entire line should be deleted
        /// </summary>
        [Test]
        public void Delete_CharacterWise_LineContentsFromBreak()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = "onemore";
            EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).ExtentIncludingLineBreak);
            _vimBuffer.Process("x");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// The 'e' motion should select up to and including the end of the word
        ///
        /// https://github.com/jaredpar/VsVim/issues/568
        /// </summary>
        [Test]
        public void Delete_EndOfWordMotion()
        {
            Create("ThisIsALongWord. ThisIsAnotherLongWord!");
            _vimBuffer.Process("vex");
            Assert.AreEqual(". ThisIsAnotherLongWord!", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Verify that Shift-V enters Visual Line Mode
        /// </summary>
        [Test]
        public void EnterVisualLine()
        {
            Create("hello", "world");
            _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// The caret position should be on the next character for a move right
        /// </summary>
        [Test]
        public void Exclusive_CaretPosition_Right()
        {
            Create("the dog");
            _globalSettings.Selection = "exclusive";
            _vimBuffer.Process("vl");
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The caret position should be on the start of the next word after leaving visual mode
        /// </summary>
        [Test]
        public void Exclusive_CaretPosition_Word()
        {
            Create("the dog");
            _globalSettings.Selection = "exclusive";
            _vimBuffer.Process("vw");
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the 'e' motion still goes one character extra during a line wise movement
        /// </summary>
        [Test]
        public void Exclusive_CaretPosition_EndOfWordLineWise()
        {
            Create("the dog. the cat");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(4);
            _vimBuffer.Process("Ve");
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'e' motion should result in a selection that encompasses the entire word
        /// </summary>
        [Test]
        public void Exclusive_Delete_EndOfWord()
        {
            Create("the dog. cat");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(4);
            _vimBuffer.Process("vex");
            Assert.AreEqual("dog", UnnamedRegister.StringValue);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'e' motion should result in a selection that encompasses the entire word
        /// </summary>
        [Test]
        public void Exclusive_Delete_EndOfWord_Block()
        {
            Create("the dog. end", "the cat. end", "the fish. end");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            _vimBuffer.Process("jex");
            Assert.AreEqual("the . end", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("the . end", _textBuffer.GetLine(1).GetText());
            Assert.AreEqual("the fish. end", _textBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// The 'w' motion should result in a selection that encompasses the entire word
        /// </summary>
        [Test]
        public void Exclusive_Delete_Word()
        {
            Create("the dog. cat");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(4);
            _vimBuffer.Process("vwx");
            Assert.AreEqual("dog", UnnamedRegister.StringValue);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Repeat1()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
            _vimBuffer.Process(">.");
            Assert.AreEqual("    dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat2()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
            _vimBuffer.Process(">..");
            Assert.AreEqual("      dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ResetCaretFromShiftLeft1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _vimBuffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromShiftLeft2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _vimBuffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromYank1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            _vimBuffer.Process("y");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go to normal mode")]
        public void SelectionChange1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 0),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go visual if there is still a selection")]
        public void SelectionChange2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 1),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange3()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _textView.Selection.Select(_textView.GetLine(1).Extent, false);
            _vimBuffer.Process(KeyInputUtil.CharToKeyInput('y'));
            _context.RunAll();
            Assert.AreEqual("  world", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange4()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _textView.SelectAndMoveCaret(new SnapshotSpan(_textView.GetLine(1).Start, 3));
            _context.RunAll();
            _vimBuffer.Process("ly");
            Assert.AreEqual("  wo", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        /// <summary>
        /// Make sure the CTRL-Q command causes the block selection to start out as a single width
        /// column
        /// </summary>
        [Test]
        public void Select_Block_InitialState()
        {
            Create("hello world");
            _vimBuffer.ProcessNotation("<C-Q>");
            Assert.AreEqual(ModeKind.VisualBlock, _vimBuffer.ModeKind);
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 1, 1);
            Assert.AreEqual(blockSpan, _textView.GetSelectionBlockSpan());
        }

        /// <summary>
        /// Make sure the CTRL-Q command causes the block selection to start out as a single width 
        /// column from places other than the start of the document
        /// </summary>
        [Test]
        public void Select_Block_InitialNonStartPoint()
        {
            Create("big cats", "big dogs", "big trees");
            var point = _textBuffer.GetPointInLine(1, 3);
            _textView.MoveCaretTo(point);
            _vimBuffer.ProcessNotation("<C-Q>");
            Assert.AreEqual(ModeKind.VisualBlock, _vimBuffer.ModeKind);
            var blockSpan = new BlockSpan(point, 1, 1);
            Assert.AreEqual(blockSpan, _textView.GetSelectionBlockSpan());
        }

        /// <summary>
        /// When selection is exclusive there should still be a single column selected in block
        /// mode even if the original width is 1
        /// </summary>
        [Test]
        public void Select_Exclusive_OneWidthBlock()
        {
            Create("the dog", "the cat");
            _textView.MoveCaretTo(1);
            _globalSettings.Selection = "exclusive";
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            _vimBuffer.Process('j');
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            Assert.AreEqual(blockSpan, _textView.GetSelectionBlockSpan());
            Assert.AreEqual(_textView.GetPointInLine(1, 1), _textView.GetCaretPoint());
        }

        /// <summary>
        /// When selection is exclusive block selection should shrink by one in width
        /// </summary>
        [Test]
        public void Select_Exclusive_TwoWidthBlock()
        {
            Create("the dog", "the cat");
            _textView.MoveCaretTo(1);
            _globalSettings.Selection = "exclusive";
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            _vimBuffer.Process("jl");
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            Assert.AreEqual(blockSpan, _textView.GetSelectionBlockSpan());
            Assert.AreEqual(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure that LastVisualSelection is set to the SnapshotSpan before the shift right
        /// command is executed
        /// </summary>
        [Test]
        public void ShiftLinesRight_LastVisualSelection()
        {
            Create("cat", "dog", "fish");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, _textView.GetLine(1).Start.Add(1)));
            _vimBuffer.Process('>');
            var visualSelection = VisualSelection.NewCharacter(
                new CharacterSpan(_textView.GetLine(0).Start, 2, 1),
                Path.Forward);
            Assert.IsTrue(_vimTextBuffer.LastVisualSelection.IsSome());
            Assert.AreEqual(visualSelection, _vimTextBuffer.LastVisualSelection.Value);
        }

        [Test]
        public void SwitchToCommandModeShouldPreserveSelection()
        {
            Create("dog", "pig", "chicken");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _vimBuffer.Process(':');
            Assert.IsFalse(_textView.Selection.IsEmpty);
        }

        /// <summary>
        /// Switch the character mode to line mode and ensure we maintain the forward 
        /// direction
        /// </summary>
        [Test]
        public void Switch_CharacterToLine()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var span = new SnapshotSpan(
                _textBuffer.GetPoint(1),
                _textBuffer.GetPointInLine(1, 2));
            EnterMode(ModeKind.VisualCharacter, span);
            _vimBuffer.Process('V');
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Switch the character mode to line mode and ensure we maintain the backwards 
        /// direction
        /// </summary>
        [Test]
        public void Switch_CharacterToLineBackwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            _textView.MoveCaretToLine(1, 2);
            _vimBuffer.Process("vkh");
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _vimBuffer.Process('V');
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Switch the line mode to character mode and ensure we maintain the forward 
        /// direction
        /// </summary>
        [Test]
        public void Switch_LineToCharacter()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            _textView.MoveCaretTo(1);
            _vimBuffer.Process("Vj");
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
            _vimBuffer.Process('v');
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);

            var span = new SnapshotSpan(
                _textBuffer.GetPoint(1),
                _textBuffer.GetPointInLine(1, 2));
            Assert.AreEqual(span, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Switch the character mode to block mode and ensure we maintain the forward
        /// direction
        /// </summary>
        [Test]
        public void Switch_CharacterToBlock()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            _textView.MoveCaretTo(1);
            _vimBuffer.Process("vjl");
            Assert.AreEqual(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
            Assert.AreEqual(ModeKind.VisualBlock, _vimBuffer.ModeKind);

            var tuple = _vimBuffer.VisualBlockMode.VisualSelection.AsBlock();
            var blockSpan = new BlockSpan(
                _textBuffer.GetPoint(1),
                2,
                2);
            Assert.AreEqual(blockSpan, tuple.Item1);
            Assert.AreEqual(BlockCaretLocation.BottomRight, tuple.Item2);
        }

        [Test]
        [Description("Even though a text span is selected, substitute should operate on the line")]
        public void Substitute1()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _vimBuffer.Process(":s/a/o", enter: true);
            Assert.AreEqual("the boy hit the cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bat", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Muliline selection should cause a replace per line")]
        public void Substitute2()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.Process(":s/a/o", enter: true);
            Assert.AreEqual("the boy hit the cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bot", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Switching to command mode shouldn't clear the selection
        /// </summary>
        [Test]
        public void Switch_ToCommandShouldNotClearSelection()
        {
            Create("cat", "dog", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.Process(":");
            Assert.IsFalse(_textView.GetSelectionSpan().IsEmpty);
        }

        /// <summary>
        /// Switching to normal mode should clear the selection
        /// </summary>
        [Test]
        public void Switch_ToNormalShouldClearSelection()
        {
            Create("cat", "dog", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.Process(VimKey.Escape);
            Assert.IsTrue(_textView.GetSelectionSpan().IsEmpty);
        }

        [Test]
        public void Handle_D_BlockMode()
        {
            Create("dog", "cat", "tree");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            _vimBuffer.Process("D");
            Assert.AreEqual("d", _textView.GetLine(0).GetText());
            Assert.AreEqual("c", _textView.GetLine(1).GetText());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLine()
        {
            Create("dog", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.Process("/c");
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLineAcrossBlanks()
        {
            Create("dog", "", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _vimBuffer.Process("/ca");
            Assert.AreEqual(_textView.GetLineRange(0, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void IncrementalSearch_CharModeShouldExtendToSearchResult()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, 1));
            _vimBuffer.Process("/o");
            Assert.AreEqual(new SnapshotSpan(_textView.GetLine(0).Start, 2), _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Enter visual mode with the InitialVisualSelection argument which is a character span
        /// </summary>
        [Test]
        public void InitialVisualSelection_Character()
        {
            Create("dogs", "cats");

            var visualSpan = VimUtil.CreateVisualSpanCharacter(_textBuffer.GetSpan(1, 2));
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
            _context.RunAll();
            Assert.AreEqual(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive));
        }

        /// <summary>
        /// Enter visual mode with the InitialVisualSelection argument which is a line span
        /// </summary>
        [Test]
        public void InitialVisualSelection_Line()
        {
            Create("dogs", "cats", "fish");

            var lineRange = _textView.GetLineRange(0, 1);
            var visualSelection = VisualSelection.NewLine(lineRange, Path.Forward, 1);
            _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
            _context.RunAll();
            Assert.AreEqual(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive));
        }

        /// <summary>
        /// Enter visual mode with the InitialVisualSelection argument which is a block span
        /// </summary>
        [Test]
        public void InitialVisualSelection_Block()
        {
            Create("dogs", "cats", "fish");

            var blockSpan = _textView.GetBlockSpan(1, 2, 0, 2);
            var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomLeft);
            _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
            _context.RunAll();
            Assert.AreEqual(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive));
        }

        /// <summary>
        /// Record a macro which delets selected text.  When the macro is played back it should
        /// just run the delete against unselected text.  In other words it's just the raw keystrokes
        /// which are saved not the selection state
        /// </summary>
        [Test]
        public void Macro_RecordDeleteSelectedText()
        {
            Create("the cat chased the dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
            _vimBuffer.Process("qcxq");
            Assert.AreEqual(" cat chased the dog", _textView.GetLine(0).GetText());
            _textView.MoveCaretTo(1);
            _vimBuffer.Process("@c");
            Assert.AreEqual(" at chased the dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Run the macro to delete the selected text
        /// </summary>
        [Test]
        public void Macro_RunDeleteSelectedText()
        {
            Create("the cat chased the dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
            TestRegister.UpdateValue("x");
            _vimBuffer.Process("@c");
            Assert.AreEqual(" cat chased the dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Character should be positioned at the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithSingleCharacterWise()
        {
            Create("dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("dcatg", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
            Assert.AreEqual("o", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned after the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithSingleCharacterWiseAndCaretMove()
        {
            Create("dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("dcatg", _textView.GetLine(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            Assert.AreEqual("o", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the start of the inserted line
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithLineWise()
        {
            Create("dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("d", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("g", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be positioned at the first line after the inserted
        /// lines
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithLineWiseAndCaretMove()
        {
            Create("dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("d", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("g", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be positioned at the start of the first line in the
        /// block 
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithBlock()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("p");
            Assert.AreEqual("daag", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbat", _textView.GetLine(1).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned after the line character in the last 
        /// line of the inserted block
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_WithBlockAndCaretMove()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("gp");
            Assert.AreEqual("daag", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbat", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
        }

        /// <summary>
        /// When doing a put over selection the text being deleted should be put into
        /// the unnamed register.
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_NamedRegisters()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
            _registerMap.GetRegister('c').UpdateValue("pig");
            _vimBuffer.Process("\"cp");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// When doing a put over selection the text being deleted should be put into
        /// the unnamed register.  If the put came from the unnamed register then the 
        /// original put value is overwritten
        /// </summary>
        [Test]
        public void PutOver_CharacterWise_UnnamedRegisters()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
            UnnamedRegister.UpdateValue("pig");
            _vimBuffer.Process("p");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithCharcterWise()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("fish", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned after the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithCharacterWiseAndCaretMove()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("fish", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithLineWise()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("fish", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned after the end of the inserted text
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithLineWiseAndCaretMove()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("fish", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the start of the first inserted value
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithBlock()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("p");
            Assert.AreEqual("aa", _textView.GetLine(0).GetText());
            Assert.AreEqual("bb", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the first character after the inserted
        /// text
        /// </summary>
        [Test]
        public void PutOver_LineWise_WithBlockAndCaretMove()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("gp");
            Assert.AreEqual("aa", _textView.GetLine(0).GetText());
            Assert.AreEqual("bb", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            Assert.AreEqual("dog\r\n", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Character should be positioned at the start of the first inserted value
        /// </summary>
        [Test]
        public void PutOver_Block_WithCharacterWise()
        {
            Create("dog", "cat");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("dfishg", _textView.GetLine(0).GetText());
            Assert.AreEqual("ct", _textView.GetLine(1).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Character should be positioned after the last character after the inserted
        /// text
        /// </summary>
        [Test]
        public void PutOver_Block_WithCharacterWiseAndCaretMove()
        {
            Create("dog", "cat");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("dfishg", _textView.GetLine(0).GetText());
            Assert.AreEqual("ct", _textView.GetLine(1).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Character should be positioned at the start of the inserted line
        /// </summary>
        [Test]
        public void PutOver_Block_WithLineWise()
        {
            Create("dog", "cat");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _vimBuffer.Process("p");
            Assert.AreEqual("dg", _textView.GetLine(0).GetText());
            Assert.AreEqual("ct", _textView.GetLine(1).GetText());
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should be positioned at the start of the line which follows the
        /// inserted lines
        /// </summary>
        [Test]
        public void PutOver_Block_WithLineWiseAndCaretMove()
        {
            Create("dog", "cat", "bear");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _vimBuffer.Process("gp");
            Assert.AreEqual("dg", _textView.GetLine(0).GetText());
            Assert.AreEqual("ct", _textView.GetLine(1).GetText());
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be positioned at the start of the first inserted string
        /// from the block
        /// </summary>
        [Test]
        public void PutOver_Block_WithBlock()
        {
            Create("dog", "cat");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("p");
            Assert.AreEqual("daag", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbt", _textView.GetLine(1).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned at the first character after the last inserted
        /// charecter of the last string in the block
        /// </summary>
        [Test]
        public void PutOver_Block_WithBlockAndCaretMove()
        {
            Create("dog", "cat");
            EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _vimBuffer.Process("gp");
            Assert.AreEqual("daag", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbt", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
        }

        [Test]
        public void PutOver_Legacy1()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _vimBuffer.Process("p");
            Assert.AreEqual("pigg", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void PutOver_Legacy2()
        {
            Create("dog", "cat", "bear", "tree");
            var span = new SnapshotSpan(
                _textView.GetLine(0).Start.Add(1),
                _textView.GetLine(1).Start.Add(2));
            EnterMode(ModeKind.VisualCharacter, span);
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _vimBuffer.Process("p");
            Assert.AreEqual("dpigt", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void PutBefore_Legacy1()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _vimBuffer.Process("P");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Put with indent commands are another odd ball item in Vim.  It's the one put command
        /// which doesn't delete the selection when putting the text into the buffer.  Instead 
        /// it just continues on in visual mode after the put
        /// </summary>
        [Test]
        public void PutAfterWithIndent_VisualLine()
        {
            Create("  dog", "  cat", "bear");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
            UnnamedRegister.UpdateValue("bear", OperationKind.LineWise);
            _vimBuffer.Process("]p");
            Assert.AreEqual("  dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Simple inner word selection on visual mode
        /// </summary>
        [Test]
        public void TextObject_InnerWord()
        {
            Create("cat dog fish");
            _textView.MoveCaretTo(4);
            _vimBuffer.Process("viw");
            Assert.AreEqual("dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When a 'iw' text selection occurs and extends the selection backwards it should reset
        /// the visual caret start point.  This can be demonstrated jumping back and forth between
        /// character and line mode
        /// </summary>
        [Test]
        public void TextObject_InnerWord_ResetVisualStartPoint()
        {
            Create("cat dog fish");
            _textView.MoveCaretTo(5);
            _vimBuffer.Process("viwVv");
            Assert.AreEqual("dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Simple inner word selection from the middle of a word.  Should still select the entire
        /// word
        /// </summary>
        [Test]
        public void TextObject_InnerWord_FromMiddle()
        {
            Create("cat dog fish");
            _textView.MoveCaretTo(5);
            _vimBuffer.Process("viw");
            Assert.AreEqual("dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This behavior isn't documented.  But if iw begins on a single white space character 
        /// then repeated iw shouldn't change anything.  It should select the single space and 
        /// go from there
        /// </summary>
        [Test]
        public void TextObject_InnerWord_FromSingleWhiteSpace()
        {
            Create("cat dog fish");
            _textView.MoveCaretTo(3);
            _vimBuffer.Process('v');
            for (var i = 0; i < 10; i++)
            {
                _vimBuffer.Process("iw");
                Assert.AreEqual(" ", _textView.GetSelectionSpan().GetText());
                Assert.AreEqual(3, _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// From a non-single white space the inner word motion should select
        /// the entire white space
        /// </summary>
        [Test]
        public void TextObject_InnerWord_FromMultipleWhiteSpace()
        {
            Create("cat  dog fish");
            _textView.MoveCaretTo(3);
            _vimBuffer.Process("viw");
            Assert.AreEqual("  ", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The non initial selection from white space should extend to the 
        /// next word
        /// </summary>
        [Test]
        public void TextObject_InnerWord_MultipleWhiteSpace_Second()
        {
            Create("cat  dog fish");
            _textView.MoveCaretTo(3);
            _vimBuffer.Process("viwiw");
            Assert.AreEqual("  dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Simple all word selection
        /// </summary>
        [Test]
        public void TextObject_AllWord()
        {
            Create("cat dog fish");
            _vimBuffer.Process("vaw");
            Assert.AreEqual("cat ", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Unlike the 'iw' motion the 'aw' motion doesn't have truly odd behavior from
        /// a single white space
        /// </summary>
        [Test]
        public void TextObject_AllWord_FromSingleWhiteSpace()
        {
            Create("cat dog fish");
            _textView.MoveCaretTo(3);
            _vimBuffer.Process("vaw");
            Assert.AreEqual(" dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// All white space and the following word should be selecetd
        /// </summary>
        [Test]
        public void TextObject_AllWord_FromMultipleWhiteSpace()
        {
            Create("cat  dog fish");
            _textView.MoveCaretTo(3);
            _vimBuffer.Process("vaw");
            Assert.AreEqual("  dog", _textView.GetSelectionSpan().GetText());
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The yank selection command should exit visual mode after the operation
        /// </summary>
        [Test]
        public void YankSelection_ShouldExitVisualMode()
        {
            Create("cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
            _vimBuffer.Process("y");
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }

        /// <summary>
        /// Ensure that after yanking and leaving Visual Mode that the proper value is
        /// maintained for LastVisualSelection.  It should be the selection before the command
        /// was executed
        /// </summary>
        [Test]
        public void YankSelection_LastVisualSelection()
        {
            Create("cat", "dog", "fish");
            var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
            EnterMode(ModeKind.VisualLine, span);
            _vimBuffer.Process('y');
            Assert.IsTrue(_vimTextBuffer.LastVisualSelection.IsSome());
            Assert.AreEqual(span, _vimTextBuffer.LastVisualSelection.Value.GetEditSpan(SelectionKind.Inclusive).OverarchingSpan);
        }

        /// <summary>
        /// The yank line selection command should exit visual mode after the operation
        /// </summary>
        [Test]
        public void YankLineSelection_ShouldExitVisualMode()
        {
            Create("cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
            _vimBuffer.Process("Y");
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }

    }
}
