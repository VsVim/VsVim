using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test the functionality of the InsertUtil set of operations
    /// </summary>
    public sealed class InsertUtilTest : VimTestBase
    {
        private IVimBuffer _buffer;
        private ITextView _textView;
        private InsertUtil _insertUtilRaw;
        private IInsertUtil _insertUtil;
        private IVimLocalSettings _localSettings;
        private IVimGlobalSettings _globalSettings;

        /// <summary>
        /// Create the IVimBuffer with the given set of lines.  Note that we intentionally don't
        /// set the mode to Insert here because the given commands should work irrespective of the 
        /// mode
        /// </summary>
        /// <param name="lines"></param>
        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _buffer = Vim.CreateVimBuffer(_textView);
            _globalSettings = _buffer.GlobalSettings;
            _localSettings = _buffer.LocalSettings;

            var operations = CommonOperationsFactory.GetCommonOperations(_buffer.VimBufferData);
            _insertUtilRaw = new InsertUtil(_buffer.VimBufferData, operations);
            _insertUtil = _insertUtilRaw;
        }

        /// <summary>
        /// If the caret is in virtual space when leaving insert mode move it back to the real
        /// position.  This really only comes up in a few cases, primarily the 'C' command 
        /// which preserves indent by putting the caret in virtual space.  For example take the 
        /// following (- are spaces and ^ is caret).
        /// --cat
        ///
        /// Caret starts on the 'c' and 'autoindent' is on.  Execute the following
        ///  - cc
        ///  - Escape
        /// Now the caret is at position 0 on a blank line 
        /// </summary>
        [Fact]
        public void CompleteMode_CaretInVirtualSpace()
        {
            Create("", "hello world");
            _textView.MoveCaretTo(0, 4);
            _insertUtilRaw.CompleteMode(true);
            Assert.Equal(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// By default it needs to move the caret one to the left as insert mode does
        /// upon completion
        /// </summary>
        [Fact]
        public void CompleteMode_Standard()
        {
            Create("cat dog");
            _textView.MoveCaretTo(2);
            _insertUtilRaw.CompleteMode(true);
            Assert.Equal(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Run the command from the begining of a word
        /// </summary>
        [Fact]
        public void DeleteWordBeforeCursor_Simple()
        {
            Create("dog bear cat");
            _globalSettings.Backspace = "start";
            _textView.MoveCaretTo(9);
            _insertUtilRaw.DeleteWordBeforeCursor();
            Assert.Equal("dog cat", _textView.GetLine(0).GetText());
            Assert.Equal(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Run the command from the middle of a word
        /// </summary>
        [Fact]
        public void DeleteWordBeforeCursor_MiddleOfWord()
        {
            Create("dog bear cat");
            _globalSettings.Backspace = "start";
            _textView.MoveCaretTo(10);
            _insertUtilRaw.DeleteWordBeforeCursor();
            Assert.Equal("dog bear at", _textView.GetLine(0).GetText());
            Assert.Equal(9, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Before the first word this should delete the indent on the line
        /// </summary>
        [Fact]
        public void DeleteWordBeforeCursor_BeforeFirstWord()
        {
            Create("   dog cat");
            _globalSettings.Backspace = "start";
            _textView.MoveCaretTo(3);
            _insertUtilRaw.DeleteWordBeforeCursor();
            Assert.Equal("dog cat", _textView.GetLine(0).GetText());
            Assert.Equal(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Don't delete a line break if the eol suboption isn't set 
        /// </summary>
        [Fact]
        public void DeleteWordBeforeCursor_LineNoOption()
        {
            Create("dog", "cat");
            _globalSettings.Backspace = "start";
            _textView.MoveCaretToLine(1);
            _insertUtilRaw.DeleteWordBeforeCursor();
            Assert.Equal("dog", _textView.GetLine(0).GetText());
            Assert.Equal("cat", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// If the eol option is set then delete the line break and move the caret back a line
        /// </summary>
        [Fact]
        public void DeleteWordBeforeCursor_LineWithOption()
        {
            Create("dog", "cat");
            _globalSettings.Backspace = "start,eol";
            _textView.MoveCaretToLine(1);
            _insertUtilRaw.DeleteWordBeforeCursor();
            Assert.Equal("dogcat", _textView.GetLine(0).GetText());
            Assert.Equal(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the caret position is correct when inserting in the middle of a word
        /// </summary>
        [Fact]
        public void InsertTab_MiddleOfText()
        {
            Create("hello");
            _textView.MoveCaretTo(2);
            _localSettings.ExpandTab = true;
            _localSettings.TabStop = 3;
            _insertUtilRaw.InsertTab();
            Assert.Equal("he llo", _textView.GetLine(0).GetText());
            Assert.Equal(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure that when a tab is inserted with 'et' on a 'non-tabstop' multiple that
        /// we move it to the 'tabstop' offset
        /// </summary>
        [Fact]
        public void InsertTab_MiddleOfText_NonEvenOffset()
        {
            Create("static LPTSTRpValue");
            _textView.MoveCaretTo(13);
            _localSettings.ExpandTab = true;
            _localSettings.TabStop = 4;
            _insertUtilRaw.InsertTab();
            Assert.Equal("static LPTSTR   pValue", _textView.GetLine(0).GetText());
            Assert.Equal(16, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure that shift left functions correctly when the caret is in virtual
        /// space.  The virtual space should just be converted to spaces and processed
        /// as such
        /// </summary>
        [Fact]
        public void ShiftLeft_FromVirtualSpace()
        {
            Create("", "dog");
            _buffer.GlobalSettings.ShiftWidth = 4;
            _textView.MoveCaretTo(0, 8);

            _insertUtilRaw.ShiftLineLeft();

            Assert.Equal("    ", _textView.GetLine(0).GetText());
            Assert.Equal(_insertUtilRaw.CaretColumn, 4);
            Assert.False(_textView.Caret.InVirtualSpace);
            // probably redundant, but we just want to be sure...
            Assert.Equal(_textView.Caret.Position.VirtualSpaces, 0);
        }

        /// <summary>
        /// This is actually non-vim behavior. Vim would leave the caret where it started, just
        /// dedented 2 columns. I think we're opting for VS-ish behavior instead here.
        /// </summary>
        [Fact]
        public void ShiftLeft_CaretIsMovedToBeginningOfLineIfInVirtualSpaceAfterEndOfLine()
        {
            Create("    foo");
            _buffer.GlobalSettings.ShiftWidth = 2;
            _textView.MoveCaretTo(0, 16);

            _insertUtilRaw.ShiftLineLeft();

            Assert.Equal(_textView.GetLine(0).GetText(), "  foo");
            Assert.Equal(_insertUtilRaw.CaretColumn, 2);
            Assert.Equal(_textView.Caret.Position.VirtualSpaces, 0);
        }

        [Fact]
        public void MoveCaretByWord_Backward_FromMiddleOfWord()
        {
            Create("dogs look bad with greasy fur");
            _textView.MoveCaretTo(7);

            _insertUtilRaw.MoveCaretByWord(Direction.Left);

            Assert.Equal(_insertUtilRaw.CaretColumn, 5);
        }

        [Fact]
        public void MoveCaretByWord_Backward_Twice()
        {
            Create("dogs look bad with greasy fur");
            _textView.MoveCaretTo(7);

            _insertUtilRaw.MoveCaretByWord(Direction.Left);
            _insertUtilRaw.MoveCaretByWord(Direction.Left);

            Assert.Equal(_insertUtilRaw.CaretColumn, 0);
        }

        [Fact]
        public void MoveCaretByWord_Forward_FromMiddleOfWord_ItLandsAtBeginningOfNextWord()
        {
            Create("dogs look bad with greasy fur");
            _textView.MoveCaretTo(7);

            _insertUtilRaw.MoveCaretByWord(Direction.Right);

            Assert.Equal(_insertUtilRaw.CaretColumn, 10);
        }

        [Fact]
        public void MoveCaretByWord_Forward_Twice()
        {
            Create("dogs look bad with greasy fur");
            _textView.MoveCaretTo(7);

            _insertUtilRaw.MoveCaretByWord(Direction.Right);
            _insertUtilRaw.MoveCaretByWord(Direction.Right);

            Assert.Equal(_insertUtilRaw.CaretColumn, 14);
        }

        [Fact]
        public void MoveCaretByWord_Forward_NextLine()
        {
            Create("dogs", "look bad with greasy fur");
            _textView.MoveCaretTo(0);

            _insertUtilRaw.MoveCaretByWord(Direction.Right);

            Assert.Equal(_textView.GetCaretPoint().Position, 6);
        }
    }
}
