using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test the functionality of the InsertUtil set of operations
    /// </summary>
    public abstract class InsertUtilTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
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
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _globalSettings = _vimBuffer.GlobalSettings;
            _localSettings = _vimBuffer.LocalSettings;

            var operations = CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData);
            var motionUtil = new MotionUtil(_vimBuffer.VimBufferData, operations);
            _insertUtilRaw = new InsertUtil(_vimBuffer.VimBufferData, motionUtil, operations);
            _insertUtil = _insertUtilRaw;
        }

        public abstract class ApplyTextChangeTest : InsertUtilTest
        {
            public sealed class DeleteRightTest : ApplyTextChangeTest
            {
                [Fact]
                public void PastEndOfBuffer()
                {
                    Create("cat dog");
                    _textView.MoveCaretTo(3);
                    _insertUtilRaw.ApplyTextChange(TextChange.NewDeleteRight(10), addNewLines: false);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [Fact]
                public void Normal()
                {
                    Create("cat dog");
                    _insertUtilRaw.ApplyTextChange(TextChange.NewDeleteRight(4), addNewLines: false);
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void AtEndOfBuffer()
                {
                    Create("cat");
                    _textView.MoveCaretTo(3);
                    _insertUtilRaw.ApplyTextChange(TextChange.NewDeleteRight(4), addNewLines: false);
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class CombinationTest : ApplyTextChangeTest
            {
                /// <summary>
                /// Insert text at the end of the buffer then delete to the right.  The trick here is
                /// to make the length of the inserted text such that the insert position would be 
                /// off the end of the ITextBuffer.  Need to make sure that the delete happens on 
                /// the original ITextBuffer position, not the caret position 
                /// </summary>
                [Fact]
                public void InsertThenDeletePastEndOfOriginalBuffer()
                {
                    Create("cat");
                    _insertUtilRaw.ApplyTextChange(
                        TextChange.NewCombination(
                            TextChange.NewInsert("trucker"),
                            TextChange.NewDeleteRight(3)),
                        addNewLines: false);
                    Assert.Equal("trucker", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(7, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Use delete right and insert to replace some text 
                /// </summary>
                [Fact]
                public void ReplaceSimple()
                {
                    Create("cat");
                    _insertUtilRaw.ApplyTextChange(
                        TextChange.NewCombination(
                            TextChange.NewDeleteRight(1),
                            TextChange.NewInsert("b")),
                        addNewLines: false);
                    Assert.Equal("bat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Use delete right and insert to replace some text 
                /// </summary>
                [Fact]
                public void ReplaceBig()
                {
                    Create("cat");
                    _insertUtilRaw.ApplyTextChange(
                        TextChange.NewCombination(
                            TextChange.NewCombination(
                                TextChange.NewDeleteRight(1),
                                TextChange.NewInsert("b")),
                            TextChange.NewCombination(
                                TextChange.NewDeleteRight(1),
                                TextChange.NewInsert("i"))),
                        addNewLines: false);
                    Assert.Equal("bit", _textBuffer.GetLine(0).GetText());
                }
            }
        }

        public sealed class CompleteModeTest : InsertUtilTest
        {
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
            public void CaretInVirtualSpace()
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
            public void Standard()
            {
                Create("cat dog");
                _textView.MoveCaretTo(2);
                _insertUtilRaw.CompleteMode(true);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class DeleteWordBeforeCursorTest : InsertUtilTest
        {
            /// <summary>
            /// Run the command from the beginning of a word
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
                _globalSettings.Backspace = "start,indent";
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
            public void LineWithOption()
            {
                Create("dog", "cat");
                _globalSettings.Backspace = "start,eol";
                _textView.MoveCaretToLine(1);
                _insertUtilRaw.DeleteWordBeforeCursor();
                Assert.Equal("dogcat", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class DeleteLineBeforeCursorTest : InsertUtilTest
        {
            /// <summary>
            /// Run the command from the end of the line
            /// </summary>
            [Fact]
            public void DeleteLineBeforeCursor_EndOfLine()
            {
                Create("dog bear cat");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretTo(12);
                _insertUtilRaw.DeleteLineBeforeCursor();
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Run the command from the middle of a line
            /// </summary>
            [Fact]
            public void DeleteLineBeforeCursor_MiddleOfLine()
            {
                Create("dog bear cat");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretTo(4);
                _insertUtilRaw.DeleteLineBeforeCursor();
                Assert.Equal("bear cat", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Before the first non-blank this should delete the indent on the line
            /// </summary>
            [Fact]
            public void DeleteLineBeforeCursor_BeforeFirstNonBlank()
            {
                Create("   dog cat");
                _globalSettings.Backspace = "start,indent";
                _textView.MoveCaretTo(3);
                _insertUtilRaw.DeleteLineBeforeCursor();
                Assert.Equal("dog cat", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Don't delete a line break if the eol suboption isn't set 
            /// </summary>
            [Fact]
            public void DeleteLineBeforeCursor_LineNoOption()
            {
                Create("dog", "cat");
                _globalSettings.Backspace = "start";
                _textView.MoveCaretToLine(1);
                _insertUtilRaw.DeleteLineBeforeCursor();
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// If the eol option is set then delete the line break and move the caret back a line
            /// </summary>
            [Fact]
            public void DeleteLineBeforeCursor_LineWithOption()
            {
                Create("dog", "cat");
                _globalSettings.Backspace = "start,eol";
                _textView.MoveCaretToLine(1);
                _insertUtilRaw.DeleteLineBeforeCursor();
                Assert.Equal("dogcat", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class InsertTabTest : InsertUtilTest
        {
            /// <summary>
            /// Make sure the caret position is correct when inserting in the middle of a word
            /// </summary>
            [Fact]
            public void MiddleOfText()
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
            public void MiddleOfText_NonEvenOffset()
            {
                Create("static LPTSTRpValue");
                _textView.MoveCaretTo(13);
                _localSettings.ExpandTab = true;
                _localSettings.TabStop = 4;
                _insertUtilRaw.InsertTab();
                Assert.Equal("static LPTSTR   pValue", _textView.GetLine(0).GetText());
                Assert.Equal(16, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class ShiftTest : InsertUtilTest
        {
            /// <summary>
            /// Make sure that shift left functions correctly when the caret is in virtual
            /// space.  The virtual space should just be converted to spaces and processed
            /// as such
            /// </summary>
            [Fact]
            public void Left_FromVirtualSpace()
            {
                Create("", "dog");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _textView.MoveCaretTo(0, 8);

                _insertUtilRaw.ShiftLineLeft();

                Assert.Equal("    ", _textView.GetLine(0).GetText());
                Assert.Equal(4, _insertUtilRaw.CaretColumn.Column);
                Assert.False(_textView.Caret.InVirtualSpace);
                // probably redundant, but we just want to be sure...
                Assert.Equal(0, _textView.Caret.Position.VirtualSpaces);
            }

            /// <summary>
            /// This is actually non-vim behavior. Vim would leave the caret where it started, just
            /// dedented 2 columns. I think we're opting for VS-ish behavior instead here.
            /// </summary>
            [Fact]
            public void Left_CaretIsMovedToBeginningOfLineIfInVirtualSpaceAfterEndOfLine()
            {
                Create("    foo");
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                _textView.MoveCaretTo(0, 16);

                _insertUtilRaw.ShiftLineLeft();

                Assert.Equal("  foo", _textView.GetLine(0).GetText());
                Assert.Equal(2, _insertUtilRaw.CaretColumn.Column);
                Assert.Equal(0, _textView.Caret.Position.VirtualSpaces);
            }

            /// <summary>
            /// Make sure that shift right functions correctly when the caret is in virtual
            /// space.  The virtual space should just be converted to spaces and processed
            /// as such
            /// </summary>
            [Fact]
            public void Right_FromVirtualSpace()
            {
                Create("", "dog");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _vimBuffer.LocalSettings.ExpandTab = true;
                _textView.MoveCaretTo(0, 8);

                _insertUtilRaw.ShiftLineRight();

                Assert.Equal("            ", _textView.GetLine(0).GetText());
                Assert.Equal(12, _insertUtilRaw.CaretColumn.Column);
                Assert.False(_textView.Caret.InVirtualSpace);
                // probably redundant, but we just want to be sure...
                Assert.Equal(0, _textView.Caret.Position.VirtualSpaces);
            }

            /// <summary>
            /// Make sure that shift right functions correctly when the caret is in virtual
            /// space with leading spaces.
            /// </summary>
            [Fact]
            public void Right_FromVirtualSpaceWithLeadingSpaces()
            {
                Create("    ", "dog");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _vimBuffer.LocalSettings.ExpandTab = true;
                _textView.MoveCaretTo(4, 4);

                _insertUtilRaw.ShiftLineRight();

                Assert.Equal("            ", _textView.GetLine(0).GetText());
                Assert.Equal(12, _insertUtilRaw.CaretColumn.Column);
                Assert.False(_textView.Caret.InVirtualSpace);
                // probably redundant, but we just want to be sure...
                Assert.Equal(0, _textView.Caret.Position.VirtualSpaces);
            }

            /// <summary>
            /// Make sure that shift right properly produces mixed tabs and spaces
            /// when the 'shiftwidth' is smaller than the 'tabstop'
            /// as such
            /// </summary>
            [Fact]
            public void Right_ToTabsAndSpaces()
            {
                Create("", "dog");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _textView.MoveCaretTo(0, 8);

                _insertUtilRaw.ShiftLineRight();

                Assert.Equal("\t    ", _textView.GetLine(0).GetText());
                Assert.Equal(5, _insertUtilRaw.CaretColumn.Column);
                Assert.False(_textView.Caret.InVirtualSpace);
                // probably redundant, but we just want to be sure...
                Assert.Equal(0, _textView.Caret.Position.VirtualSpaces);
            }

            /// <summary>
            /// Make sure that shift right functions correctly on blank lines
            /// </summary>
            [Fact]
            public void Right_FromBlankLine()
            {
                Create("");
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _insertUtilRaw.ShiftLineRight();

                Assert.Equal("    ", _textView.GetLine(0).GetText());
                Assert.Equal(4, _insertUtilRaw.CaretColumn.Column);
            }

            /// <summary>
            /// Make sure that shift right functions correctly on lines with
            /// leading blanks not equivalent to a multiple of the shift wdith
            /// </summary>
            [Fact]
            public void Right_WithIrregularLeadingBlanks()
            {
                Create("   abc");
                _textView.MoveCaretTo(3);
                _vimBuffer.LocalSettings.ShiftWidth = 4;
                _insertUtilRaw.ShiftLineRight();

                Assert.Equal("    abc", _textView.GetLine(0).GetText());
                Assert.Equal(4, _insertUtilRaw.CaretColumn.Column);
            }
        }

        public sealed class MoveCaretByWordTest : InsertUtilTest
        {
            [Fact]
            public void Backward_FromMiddleOfWord()
            {
                Create("dogs look bad with greasy fur");
                _textView.MoveCaretTo(7);

                _insertUtilRaw.MoveCaretByWord(Direction.Left);

                Assert.Equal(5, _insertUtilRaw.CaretColumn.Column);
            }

            [Fact]
            public void Backward_Twice()
            {
                Create("dogs look bad with greasy fur");
                _textView.MoveCaretTo(7);

                _insertUtilRaw.MoveCaretByWord(Direction.Left);
                _insertUtilRaw.MoveCaretByWord(Direction.Left);

                Assert.Equal(0, _insertUtilRaw.CaretColumn.Column);
            }

            [Fact]
            public void Forward_FromMiddleOfWord_ItLandsAtBeginningOfNextWord()
            {
                Create("dogs look bad with greasy fur");
                _textView.MoveCaretTo(7);

                _insertUtilRaw.MoveCaretByWord(Direction.Right);

                Assert.Equal(10, _insertUtilRaw.CaretColumn.Column);
            }

            [Fact]
            public void Forward_Twice()
            {
                Create("dogs look bad with greasy fur");
                _textView.MoveCaretTo(7);

                _insertUtilRaw.MoveCaretByWord(Direction.Right);
                _insertUtilRaw.MoveCaretByWord(Direction.Right);

                Assert.Equal(14, _insertUtilRaw.CaretColumn.Column);
            }

            [Fact]
            public void Forward_NextLine()
            {
                Create("dogs", "look bad with greasy fur");
                _textView.MoveCaretTo(0);

                _insertUtilRaw.MoveCaretByWord(Direction.Right);

                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Issue #1269 - part I
            /// </summary>
            [Fact]
            public void Forward_NextLineFromBlankLine()
            {
                Create("", "dogs look bad with greasy fur");
                _textView.MoveCaretTo(0);

                _insertUtilRaw.MoveCaretByWord(Direction.Right);

                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Issue #1269 - part II
            /// </summary>
            [Fact]
            public void Forward_FromLastWordOfLastLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(5);

                _insertUtilRaw.MoveCaretByWord(Direction.Right);

                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Tests for moving the caret in insert mode
        /// </summary>
        public sealed class MoveCaretWithArrowTest : InsertUtilTest
        {
            /// <summary>
            /// Arrow left at beginning of line without 'whichwrap=['
            /// should stay put
            /// </summary>
            [Fact]
            public void Without_IsWhichWrapArrowLeftInsert()
            {
                Create("dog", "cat");
                _globalSettings.WhichWrap = "";
                _textView.MoveCaretTo(5);
                _insertUtilRaw.MoveCaretWithArrow(Direction.Left);
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Arrow left at beginning of line with 'whichwrap=['
            /// should move the end of the previous line
            /// </summary>
            [Fact]
            public void With_IsWhichWrapArrowLeftInsert()
            {
                Create("dog", "cat");
                _globalSettings.WhichWrap = "[";
                _textView.MoveCaretTo(5);
                _insertUtilRaw.MoveCaretWithArrow(Direction.Left);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Arrow right at end of line without 'whichwrap=['
            /// should stay put
            /// </summary>
            [Fact]
            public void Without_IsWhichWrapArrowRightInsert()
            {
                Create("dog", "cat");
                _globalSettings.WhichWrap = "";
                _textView.MoveCaretTo(3);
                _insertUtilRaw.MoveCaretWithArrow(Direction.Right);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Arrow right at end of line with 'whichwrap=]'
            /// should move the beginning of the next line
            /// </summary>
            [Fact]
            public void With_IsWhichWrapArrowRightInsert()
            {
                Create("dog", "cat");
                _globalSettings.WhichWrap = "]";
                _textView.MoveCaretTo(3);
                _insertUtilRaw.MoveCaretWithArrow(Direction.Right);
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }
        }
    }
}
