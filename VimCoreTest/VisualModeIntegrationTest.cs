using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VisualModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private IRegisterMap _registerMap;
        private IVimGlobalSettings _globalSettings;
        private TestableSynchronizationContext _context;

        internal Register UnnamedRegister
        {
            get { return _buffer.RegisterMap.GetRegister(RegisterName.Unnamed); }
        }

        internal Register TestRegister
        {
            get { return _buffer.RegisterMap.GetRegister('c'); }
        }

        public void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            var tuple = EditorUtil.CreateTextViewAndEditorOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _registerMap = _buffer.RegisterMap;
            _globalSettings = _buffer.LocalSettings.GlobalSettings;
            Assert.IsTrue(_context.IsEmpty);

            // Need to make sure it's focused so macro recording will work
            ((MockVimHost)_buffer.Vim.VimHost).FocusedTextView = _textView;
        }

        private void EnterMode(SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            _textView.SelectAndUpdateCaret(span, mode);
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
        }

        private void EnterMode(ModeKind kind, SnapshotSpan span, TextSelectionMode mode = TextSelectionMode.Stream)
        {
            EnterMode(span, mode);
            _buffer.SwitchMode(kind, ModeArgument.None);
        }

        private void EnterBlock(NonEmptyCollection<SnapshotSpan> spans)
        {
            _textView.MoveCaretTo(spans.Last().End.Subtract(1));
            _textView.Selection.Mode = TextSelectionMode.Box;
            _textView.Selection.Select(
                new VirtualSnapshotPoint(spans.Head.Start),
                new VirtualSnapshotPoint(spans.Last().End));
            Assert.IsFalse(_context.IsEmpty);
            _context.RunAll();
            Assert.IsTrue(_context.IsEmpty);
            _buffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
        }

        /// <summary>
        /// When changing a line wise selection one blank line should be left remaining in the ITextBuffer
        /// </summary>
        [Test]
        public void Change_LineWise()
        {
            Create("cat", "  dog", "  bear", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(1, 2).ExtentIncludingLineBreak);
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("c");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
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
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("c");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
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
            _buffer.Process('C');
            _buffer.Process(VimKey.Escape);
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
            _buffer.Process("x");
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
            _buffer.Process("x");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat1()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">.");
            Assert.AreEqual("    dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Repeat2()
        {
            Create("dog again", "cat again", "chicken");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 2;
            _buffer.Process(">..");
            Assert.AreEqual("      dog again", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ResetCaretFromShiftLeft1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromShiftLeft2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process("<");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ResetCaretFromYank1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            _buffer.Process("y");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go to normal mode")]
        public void SelectionChange1()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 0),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        [Description("Moving the caret which resets the selection should go visual if there is still a selection")]
        public void SelectionChange2()
        {
            Create("  hello", "  world");
            EnterMode(_textView.TextBuffer.GetSpan(0, 2));
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(
                new SnapshotSpan(_textView.GetLine(1).Start, 1),
                false);
            _context.RunAll();
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange3()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.Selection.Select(_textView.GetLine(1).Extent, false);
            _buffer.Process(KeyInputUtil.CharToKeyInput('y'));
            _context.RunAll();
            Assert.AreEqual("  world", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        [Description("Make sure we reset the span we need")]
        public void SelectionChange4()
        {
            Create("  hello", "  world");
            EnterMode(_textView.GetLine(0).Extent);
            Assert.AreEqual(ModeKind.VisualCharacter, _buffer.ModeKind);
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(1).Start, 3));
            _context.RunAll();
            _buffer.Process("ly");
            Assert.AreEqual("  wo", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        [Description("Enter Visual Line Mode")]
        public void EnterVisualLine1()
        {
            Create("hello", "world");
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
            Assert.AreEqual(ModeKind.VisualLine, _buffer.ModeKind);
        }

        [Test]
        public void SwitchToCommandModeShouldPreserveSelection()
        {
            Create("dog", "pig", "chicken");
            EnterMode(_textView.GetLineRange(0, 1).Extent);
            _buffer.Process(':');
            Assert.IsFalse(_textView.Selection.IsEmpty);
        }

        [Test]
        [Description("Even though a text span is selected, substitute should operate on the line")]
        public void Substitute1()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _buffer.Process(":s/a/o", enter: true);
            Assert.AreEqual("the boy hit the cot", _textView.GetLine(0).GetText());
            Assert.AreEqual("bat", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Muliline selection should cause a replace per line")]
        public void Substitute2()
        {
            Create("the boy hit the cat", "bat");
            EnterMode(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process(":s/a/o", enter: true);
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
            _buffer.Process(":");
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
            _buffer.Process(VimKey.Escape);
            Assert.IsTrue(_textView.GetSelectionSpan().IsEmpty);
        }

        [Test]
        public void Handle_D_BlockMode()
        {
            Create("dog", "cat", "tree");
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            _buffer.Process("D");
            Assert.AreEqual("d", _textView.GetLine(0).GetText());
            Assert.AreEqual("c", _textView.GetLine(1).GetText());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLine()
        {
            Create("dog", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process("/c");
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void IncrementalSearch_LineModeShouldSelectFullLineAcrossBlanks()
        {
            Create("dog", "", "cat", "tree");
            EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
            _buffer.Process("/ca");
            Assert.AreEqual(_textView.GetLineRange(0, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        [Test]
        public void IncrementalSearch_CharModeShouldExtendToSearchResult()
        {
            Create("dog", "cat");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, 1));
            _buffer.Process("/o");
            Assert.AreEqual(new SnapshotSpan(_textView.GetLine(0).Start, 2), _textView.GetSelectionSpan());
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
            _buffer.Process("qcxq");
            Assert.AreEqual(" cat chased the dog", _textView.GetLine(0).GetText());
            _textView.MoveCaretTo(1);
            _buffer.Process("@c");
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
            _buffer.Process("@c");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            _buffer.Process("\"cp");
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
            _buffer.Process("\"cp");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            _buffer.Process("p");
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
            _buffer.Process("gp");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _buffer.Process("p");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
            _buffer.Process("gp");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _buffer.Process("p");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
            _buffer.Process("gp");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("p");
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
            EnterBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("gp");
            Assert.AreEqual("daag", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbt", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
        }

        [Test]
        public void PutOver_Legacy1()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 0, 2));
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("p");
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
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("p");
            Assert.AreEqual("dpigt", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void PutBefore_Legacy1()
        {
            Create("dog", "cat", "bear", "tree");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
            _buffer.Process("P");
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
            _buffer.Process("]p");
            Assert.AreEqual("  dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
            Assert.AreEqual(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            Assert.AreEqual(ModeKind.VisualLine, _buffer.ModeKind);
        }

        /// <summary>
        /// The yank selection command should exit visual mode after the operation
        /// </summary>
        [Test]
        public void YankSelection_ShouldExitVisualMode()
        {
            Create("cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
            _buffer.Process("y");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }

        /// <summary>
        /// The yank line selection command should exit visual mode after the operation
        /// </summary>
        [Test]
        public void YankLineSelection_ShouldExitVisualMode()
        {
            Create("cat", "dog");
            EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
            _buffer.Process("Y");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.IsTrue(_textView.Selection.IsEmpty);
        }
    }
}
