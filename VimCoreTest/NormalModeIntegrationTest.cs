using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class NormalModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IVimGlobalSettings _globalSettings;
        private IJumpList _jumpList;
        private IKeyMap _keyMap;
        private IVimData _vimData;
        private IFoldManager _foldManager;
        private MockVimHost _vimHost;
        private bool _assertOnErrorMessage = true;
        private bool _assertOnWarningMessage = true;

        internal Register UnnamedRegister
        {
            get { return _buffer.GetRegister(RegisterName.Unnamed); }
        }

        public void Create(params string[] lines)
        {
            var tuple = EditorUtil.CreateTextViewAndEditorOperations(lines);
            _textView = tuple.Item1;
            _textBuffer = _textView.TextBuffer;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
            _buffer.ErrorMessage +=
                (_, message) =>
                {
                    if (_assertOnErrorMessage)
                    {
                        Assert.Fail("Error Message: " + message);
                    }
                };
            _buffer.WarningMessage +=
                (_, message) =>
                {
                    if (_assertOnWarningMessage)
                    {
                        Assert.Fail("Warning Message: " + message);
                    }
                };
            _keyMap = _buffer.Vim.KeyMap;
            _globalSettings = _buffer.LocalSettings.GlobalSettings;
            _jumpList = _buffer.JumpList;
            _vimHost = (MockVimHost)_buffer.Vim.VimHost;
            _vimHost.BeepCount = 0;
            _vimData = service.Vim.VimData;
            _foldManager = EditorUtil.FactoryService.FoldManagerFactory.GetFoldManager(_textView);

            // Many of the operations operate on both the visual and edit / text snapshot
            // simultaneously.  Ensure that our setup code is producing a proper IElisionSnapshot
            // for the Visual portion so we can root out any bad mixing of instances between
            // the two
            Assert.IsTrue(_textView.VisualSnapshot is IElisionSnapshot);
            Assert.IsTrue(_textView.VisualSnapshot != _textView.TextSnapshot);
        }

        [TearDown]
        public void TearDown()
        {
            _keyMap.ClearAll();
            _buffer.Close();

            // Make sure that on tear down we don't have a current transaction.  Having one indicates
            // we didn't close it and hence are killing undo in the ITextBuffer
            var history = EditorUtil.GetUndoHistory(_textView.TextBuffer);
            Assert.IsNull(history.CurrentTransaction);

            _vimData.SearchHistory.Clear();
            _vimData.CommandHistory.Clear();
        }

        [Test]
        public void dd_OnLastLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _buffer.Process("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void RepeatCommand_Repeated()
        {
            Create("the fox chased the bird");
            _buffer.Process("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange1()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey ");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange2()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange3()
        {
            Create("the fox chased the bird");
            _buffer.Process("cw");
            _buffer.Process("hey");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("A d with Enter should delete the line break")]
        public void Issue317_1()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process("2d", enter: true);
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Verify the contents after with a paste")]
        public void Issue317_2()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process("2d", enter: true);
            _buffer.Process("p");
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("jazz", _textView.GetLine(3).GetText());
        }

        [Test]
        [Description("Plain old Enter should just move the cursor one line")]
        public void Issue317_3()
        {
            Create("dog", "cat", "jazz", "band");
            _buffer.Process(KeyInputUtil.EnterKey);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void Motion_Section1()
        {
            Create("hello", "{world");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void Motion_Section2()
        {
            Create("hello", "\fworld");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section3()
        {
            Create("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section4()
        {
            Create("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section5()
        {
            Create("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// The ']]' motion should stop on section macros
        /// </summary>
        [Test]
        public void Motion_SectionForwardToMacro()
        {
            Create("cat", "", "bear", ".HU", "sheep");
            _globalSettings.Sections = "HU";
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Move the caret using the end of word motion repeatedly
        /// </summary>
        [Test]
        public void Motion_MoveEndOfWord()
        {
            Create("the cat chases the dog");
            _buffer.Process("e");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _buffer.Process("e");
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
            _buffer.Process("e");
            Assert.AreEqual(13, _textView.GetCaretPoint().Position);
            _buffer.Process("e");
            Assert.AreEqual(17, _textView.GetCaretPoint().Position);
            _buffer.Process("e");
            Assert.AreEqual(21, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'w' needs to be able to get off of a blank line
        /// </summary>
        [Test]
        public void Motion_MoveWordAcrossBlankLine()
        {
            Create("dog", "", "cat ball");
            _buffer.Process("w");
            Assert.AreEqual(_textView.GetPointInLine(1, 0), _textView.GetCaretPoint());
            _buffer.Process("w");
            Assert.AreEqual(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
            _buffer.Process("w");
            Assert.AreEqual(_textView.GetPointInLine(2, 4), _textView.GetCaretPoint());
        }

        /// <summary>
        /// The 'w' from a blank should move to the next word
        /// </summary>
        [Test]
        public void Motion_WordFromBlank()
        {
            Create("the dog chased the ball");
            _textView.MoveCaretTo(3);
            _buffer.Process("w");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _buffer.Process("w");
            Assert.AreEqual(8, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'b' from a blank should move to the start of the previous word
        /// </summary>
        [Test]
        public void Motion_WordFromBlankBackward()
        {
            Create("the dog chased the ball");
            _textView.MoveCaretTo(7);
            _buffer.Process("b");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _buffer.Process("b");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'b' from the start of a word should move to the start of the previous word
        /// </summary>
        [Test]
        public void Motion_WordFromStartBackward()
        {
            Create("the dog chased the ball");
            _textView.MoveCaretTo(8);
            _buffer.Process("b");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _buffer.Process("b");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Blank lines are sentences
        /// </summary>
        [Test]
        public void Move_SentenceForBlankLine()
        {
            Create("dog.  ", "", "cat");
            _buffer.Process(")");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// A warning message should be raised when a search forward for a value
        /// causes a wrap to occur
        /// </summary>
        [Test]
        public void Move_SearchWraps()
        {
            Create("dog", "cat", "tree");
            var didHit = false;
            _textView.MoveCaretToLine(1);
            _assertOnWarningMessage = false;
            _buffer.LocalSettings.GlobalSettings.WrapScan = true;
            _buffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _buffer.Process("/dog", enter: true);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// Make sure the paragraph move goes to the appropriate location
        /// </summary>
        [Test]
        public void Move_ParagraphForward()
        {
            Create("dog", "", "cat", "", "bear");
            _buffer.Process("}");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the paragraph move backward goes to the appropriate location
        /// </summary>
        [Test]
        public void Move_ParagraphBackward()
        {
            Create("dog", "", "cat", "pig", "");
            _textView.MoveCaretToLine(3);
            _buffer.Process("{");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the paragraph move backward goes to the appropriate location when 
        /// started on the first line of the paragraph containing actual text
        /// </summary>
        [Test]
        public void Move_ParagraphBackwardFromTextStart()
        {
            Create("dog", "", "cat", "pig", "");
            _textView.MoveCaretToLine(2);
            _buffer.Process("{");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure that when starting on a section start line we jump over it when 
        /// using the section forward motion
        /// </summary>
        [Test]
        public void Move_SectionForwardFromCloseBrace()
        {
            Create("dog", "}", "bed", "cat");
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure that we move off of the brace line when we are past the opening
        /// brace on the line
        /// </summary>
        [Test]
        public void Move_SectionFromAfterCloseBrace()
        {
            Create("dog", "} bed", "cat");
            _textView.MoveCaretToLine(1, 3);
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            _textView.MoveCaretToLine(1, 3);
            _buffer.Process("[]");
            Assert.AreEqual(_textView.GetLine(0).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure we handle the cases of many braces in a row correctly
        /// </summary>
        [Test]
        public void Move_SectionBracesInARow()
        {
            Create("dog", "}", "}", "}", "cat");

            // Go forward
            for (var i = 0; i < 4; i++)
            {
                _buffer.Process("][");
                Assert.AreEqual(_textView.GetLine(i + 1).Start, _textView.GetCaretPoint());
            }

            // And now backward
            for (var i = 0; i < 4; i++)
            {
                _buffer.Process("[]");
                Assert.AreEqual(_textView.GetLine(4 - i - 1).Start, _textView.GetCaretPoint());
            }
        }

        /// <summary>
        /// Make sure that when starting on a section start line for a macro we jump 
        /// over it when using the section forward motion
        /// </summary>
        [Test]
        public void Move_SectionForwardFromMacro()
        {
            Create("dog", ".SH", "bed", "cat");
            _globalSettings.Sections = "SH";
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Ensure the '%' motion properly moves between the block comments in the 
        /// mismatch case
        /// </summary>
        [Test]
        public void MatchingToken_MismatchedBlockComments()
        {
            Create("/* /* */");
            _textView.MoveCaretTo(3);
            _buffer.Process('%');
            Assert.AreEqual(7, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(0, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(7, _textView.GetCaretPoint());
        }

        /// <summary>
        /// See the full discussion in issue #509
        ///
        /// https://github.com/jaredpar/VsVim/issues/509
        ///
        /// Make sure that doing a ""][" from the middle of the line ends on the '}' if it is
        /// preceded by a blank line
        /// </summary>
        [Test]
        public void Motion_MoveSection_RegressionTest_509()
        {
            Create("cat", "", "}");
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
            _textView.MoveCaretTo(1);
            _buffer.Process("][");
            Assert.AreEqual(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Case is explicitly called out in the ':help exclusive-linewise' portion
        /// of the documentation
        /// </summary>
        [Test]
        public void Motion_ExclusiveLineWise()
        {
            Create("  dog", "cat", "", "pig");
            _textView.MoveCaretTo(2);
            _buffer.Process("d}");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            _buffer.Process("p");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("pig", _textView.GetLine(3).GetText());
        }

        /// <summary>
        /// Make sure we move to the column on the current line when there is no count
        /// </summary>
        [Test]
        public void Motion_FirstNonWhiteSpaceOnLine()
        {
            Create(" cat", "  dog", "   fish");
            _textView.MoveCaretToLine(1);
            _buffer.Process("_");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Simple word motion.  Make sure the caret gets put on the start of the next
        /// word
        /// </summary>
        [Test]
        public void Motion_Word()
        {
            Create("cat dog bear");
            _buffer.Process("w");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _buffer.Process("w");
            Assert.AreEqual(8, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When there is no white space following a word and there is white space before 
        /// and a word on the same line then we grab the white space before the word
        /// </summary>
        [Test]
        public void Motion_AllWord_WhiteSpaceOnlyBefore()
        {
            Create("hello", "cat dog", "  bat");
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(4));
            Assert.AreEqual('d', _textView.GetCaretPoint().GetChar());
            _buffer.Process("yaw");
            Assert.AreEqual(" dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// When starting in the white space it should be included and not the white space
        /// after
        /// </summary>
        [Test]
        public void Motion_AllWord_InWhiteSpaceBeforeWord()
        {
            Create("dog cat tree");
            _textView.MoveCaretTo(3);
            _buffer.Process("yaw");
            Assert.AreEqual(" cat", UnnamedRegister.StringValue);
        }

        [Test]
        public void RepeatLastSearch1()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(0);
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch2()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch3()
        {
            Create("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _buffer.Process('N');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// A change word operation shouldn't delete the whitespace trailing the word
        /// </summary>
        [Test]
        public void Change_Word()
        {
            Create("dog cat bear");
            _buffer.Process("cw");
            Assert.AreEqual(" cat bear", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// A change all word operation should delete the whitespace trailing the word.  Really
        /// odd when considering 'cw' doesn't.
        /// </summary>
        [Test]
        public void Change_AllWord()
        {
            Create("dog cat bear");
            _buffer.Process("caw");
            Assert.AreEqual("cat bear", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure that we can change the character at the end of a line
        /// </summary>
        [Test]
        public void Change_CharAtEndOfLine()
        {
            Create("hat", "cat");
            _textView.MoveCaretTo(2);
            _buffer.LocalSettings.GlobalSettings.VirtualEdit = String.Empty;
            _buffer.Process("cl");
            Assert.AreEqual("ha", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// Ensure that we can change the character at the end of a line when 've=onemore'
        /// </summary>
        [Test]
        public void Change_CharAtEndOfLine_VirtualEditOneMore()
        {
            Create("hat", "cat");
            _textView.MoveCaretTo(2);
            _buffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _buffer.Process("cl");
            Assert.AreEqual("ha", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// Make sure the d#d syntax doesn't apply to other commands like change.  The 'd' suffix in 'd#d' is 
        /// *not* a valid motion
        /// </summary>
        [Test]
        public void Change_Illegal()
        {
            Create("cat", "dog", "tree");
            _buffer.Process("c2d");
            Assert.AreEqual("cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("dog", _textBuffer.GetLine(1).GetText());
            Assert.AreEqual("tree", _textBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// When virtual edit is disabled and 'x' is used to delete the last character on the line
        /// then the caret needs to move backward to maintain the non-virtual edit position
        /// </summary>
        [Test]
        public void DeleteChar_EndOfLine_NoVirtualEdit()
        {
            Create("test");
            _buffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When virtual edit is enabled and 'x' is used to delete the last character on the line
        /// then the caret should stay in it's current position 
        /// </summary>
        [Test]
        public void DeleteChar_EndOfLine_VirtualEdit()
        {
            Create("test", "bar");
            _buffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret position should remain unchanged when deleting a character in the middle of 
        /// a word
        /// </summary>
        [Test]
        public void DeleteChar_MiddleOfWord()
        {
            Create("test", "bar");
            _buffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _buffer.Process('x');
            Assert.AreEqual("tst", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_DeleteWord1()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("dw");
            _buffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Make sure that movement doesn't reset the last edit command")]
        public void RepeatCommand_DeleteWord2()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("dw");
            _buffer.Process(VimKey.Right);
            _buffer.Process(VimKey.Left);
            _buffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Delete word with a count")]
        public void RepeatCommand_DeleteWord3()
        {
            Create("the cat jumped over the dog");
            _buffer.Process("2dw");
            _buffer.Process(".");
            Assert.AreEqual("the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("dd");
            _buffer.Process(".");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("2dd");
            _buffer.Process(".");
            Assert.AreEqual("fox", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft1()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft2()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2<<");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process(">>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _buffer.Process("2>>");
            _buffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar1()
        {
            Create("longer");
            _buffer.Process("x");
            _buffer.Process(".");
            Assert.AreEqual("nger", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar2()
        {
            Create("longer");
            _buffer.Process("2x");
            _buffer.Process(".");
            Assert.AreEqual("er", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("After a search operation")]
        public void RepeatCommand_DeleteChar3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("/e", enter: true);
            _buffer.Process("x");
            _buffer.Process("n");
            _buffer.Process(".");
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("zbra", _textView.GetLine(3).GetText());
        }

        [Test]
        public void RepeatCommand_Put1()
        {
            Create("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("p");
            _buffer.Process(".");
            Assert.AreEqual("cloloat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Put2()
        {
            Create("cat");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _buffer.Process("2p");
            _buffer.Process(".");
            Assert.AreEqual("clolololoat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_JoinLines1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("J");
            _buffer.Process(".");
            Assert.AreEqual("bear dog cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Change1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.Process(VimKey.Delete);
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("ar", _textView.GetLine(0).GetText());
            Assert.AreEqual("g", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Change2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("cl");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("s");
            _buffer.Process("u");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _buffer.Process(VimKey.Down);
            _buffer.Process("2.");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("ug", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _buffer.Process(".");
            Assert.AreEqual("ababccbear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            Assert.AreEqual("abcabcbear", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_TextInsert3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("i");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _buffer.Process(".");
            _buffer.Process(".");
            Assert.AreEqual("ababccabcbear", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Test the repeating of a command that changes white space to tabs
        /// </summary>
        [Test]
        public void RepeatCommand_TextInsert_WhiteSpaceToTab()
        {
            Create("    hello world", "dog");
            _buffer.LocalSettings.TabStop = 4;
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.Process('i');
            _textBuffer.Replace(new Span(0, 4), "\t\t");
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("\tdog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI2()
        {
            Create("bear", "  dog", "cat", "zebra", "fox", "jazz");
            _buffer.Process("I");
            _buffer.Process("abc");
            _buffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _buffer.Process(".");
            Assert.AreEqual("  abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(4), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Repeating a replace char command should move the caret to the end just like
        /// the original command did
        /// </summary>
        [Test]
        public void RepeatCommand_ReplaceChar_ShouldMoveCaret()
        {
            Create("the dog kicked the ball");
            _buffer.Process("3ru");
            Assert.AreEqual("uuu dog kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _textView.MoveCaretTo(4);
            _buffer.Process(".");
            Assert.AreEqual("uuu uuu kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Repeating a 
        /// replace char command from visual mode should not move the caret
        /// </summary>
        [Test]
        public void RepeatCommand_ReplaceCharVisual_ShouldNotMoveCaret()
        {
            Create("the dog kicked the ball");
            _buffer.VimData.LastCommand = FSharpOption.Create(StoredCommand.NewVisualCommand(
                VisualCommand.NewReplaceSelection(KeyInputUtil.VimKeyToKeyInput(VimKey.LowerB)),
                VimUtil.CreateCommandData(),
                StoredVisualSpan.OfVisualSpan(VisualSpan.NewCharacter(_textView.GetLineSpan(0, 3))),
                CommandFlags.None));
            _textView.MoveCaretTo(1);
            _buffer.Process(".");
            Assert.AreEqual("tbbbdog kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the caret movement occurs as part of the repeat
        /// </summary>
        [Test]
        public void RepeatCommand_AppendShouldRepeat()
        {
            Create("{", "}");
            _textView.MoveCaretToLine(0);
            _buffer.Process('a');
            _buffer.Process(';');
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("};", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure the caret movement occurs as part of the repeat
        /// </summary>
        [Test]
        public void RepeatCommand_AppendEndOfLineShouldRepeat()
        {
            Create("{", "}");
            _textView.MoveCaretToLine(0);
            _buffer.Process('A');
            _buffer.Process(';');
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("};", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// The insert line above command should be linked the the following text change
        /// </summary>
        [Test]
        public void RepeatCommand_InsertLineAbove()
        {
            Create("cat", "dog", "tree");
            _textView.MoveCaretToLine(2);
            _buffer.Process("O  fish");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("  fish", _textView.GetLine(2).GetText());
            _textView.MoveCaretToLine(1);
            _buffer.Process(".");
            Assert.AreEqual("  fish", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// The insert line below command should be linked the the following text change
        /// </summary>
        [Test]
        public void RepeatCommand_InsertLineBelow()
        {
            Create("cat", "dog", "tree");
            _buffer.Process("o  fish");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("  fish", _textView.GetLine(1).GetText());
            _textView.MoveCaretToLine(2);
            _buffer.Process(".");
            Assert.AreEqual("  fish", _textView.GetLine(3).GetText());
        }

        [Test]
        public void Repeat_DeleteWithIncrementalSearch()
        {
            Create("dog cat bear tree");
            _buffer.Process("d/a", enter: true);
            _buffer.Process('.');
            Assert.AreEqual("ar tree", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ToCharDoesNotUseMap()
        {
            Create("bear; again: dog");
            _buffer.Process(":map ; :", enter: true);
            _buffer.Process("dt;");
            Assert.AreEqual("; again: dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_AlphaToRightMotion()
        {
            Create("dog");
            _buffer.Process(":map a l", enter: true);
            _buffer.Process("aa");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_OperatorPendingWithAmbiguousCommandPrefix()
        {
            Create("dog chases the ball");
            _buffer.Process(":map a w", enter: true);
            _buffer.Process("da");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ReplaceDoesntUseNormalMap()
        {
            Create("dog");
            _buffer.Process(":map f g", enter: true);
            _buffer.Process("rf");
            Assert.AreEqual("fog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_IncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _buffer.Process(":cmap a o", enter: true);
            _buffer.Process("/a", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_ReverseIncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _buffer.Process(":cmap a o", enter: true);
            _buffer.Process("?a", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Ensure that we don't regress issue 522 which is a recursive key mapping problem
        /// </summary>
        [Test]
        public void Map_Issue522()
        {
            Create("cat", "dog");
            _textView.MoveCaretToLine(1);
            _buffer.Process(":map j 3j", enter: true);
            _buffer.Process(":ounmap j", enter: true);
            _buffer.Process(":map k 3k", enter: true);
            _buffer.Process(":ounmap k", enter: true);
            _buffer.Process("k");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Move_EndOfWord_SeveralLines()
        {
            Create("the dog kicked the", "ball. The end. Bear");
            for (var i = 0; i < 10; i++)
            {
                _buffer.Process("e");
            }
            Assert.AreEqual(_textView.GetLine(1).End.Subtract(1), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Trying a move caret left at the start of the line should cause a beep 
        /// to be produced
        /// </summary>
        [Test]
        public void Move_CharLeftAtStartOfLine()
        {
            Create("cat", "dog");
            _textView.MoveCaretToLine(1);
            _buffer.Process("h");
            Assert.AreEqual(1, _vimHost.BeepCount);
        }

        /// <summary>
        /// Beep when moving a character right at the end of the line
        /// </summary>
        [Test]
        public void Move_CharRightAtLastOfLine()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = String.Empty;  // Ensure not 'OneMore'
            _textView.MoveCaretTo(2);
            _buffer.Process("l");
            Assert.AreEqual(1, _vimHost.BeepCount);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Succeed in moving when the 'onemore' option is set 
        /// </summary>
        [Test]
        public void Move_CharRightAtLastOfLineWithOneMore()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(2);
            _buffer.Process("l");
            Assert.AreEqual(0, _vimHost.BeepCount);
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Fail at moving one more right when in the end 
        /// </summary>
        [Test]
        public void Move_CharRightAtEndOfLine()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(3);
            _buffer.Process("l");
            Assert.AreEqual(1, _vimHost.BeepCount);
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This should beep 
        /// </summary>
        [Test]
        public void Move_UpFromFirstLine()
        {
            Create("cat");
            _buffer.Process("k");
            Assert.AreEqual(1, _vimHost.BeepCount);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This should beep
        /// </summary>
        [Test]
        public void Move_DownFromLastLine()
        {
            Create("cat");
            _buffer.Process("j");
            Assert.AreEqual(1, _vimHost.BeepCount);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The '*' movement should update the search history for the buffer
        /// </summary>
        [Test]
        public void Move_NextWordUnderCursor()
        {
            Create("cat", "dog", "cat");
            _buffer.Process("*");
            Assert.AreEqual(PatternUtil.CreateWholeWord("cat"), _vimData.SearchHistory.Items.Head);
        }

        /// <summary>
        /// When moving a line down over a fold it should not be expanded and the entire fold
        /// should count as a single line
        /// </summary>
        [Test]
        public void Move_LineDown_OverFold()
        {
            Create("cat", "dog", "tree", "fish");
            var range = _textView.GetLineRange(1, 2);
            _foldManager.CreateFold(range);
            _buffer.Process('j');
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _buffer.Process('j');
            Assert.AreEqual(3, _textView.GetCaretLine().LineNumber);
        }

        /// <summary>
        /// The 'g*' movement should update the search history for the buffer
        /// </summary>
        [Test]
        public void Move_NextPartialWordUnderCursor()
        {
            Create("cat", "dog", "cat");
            _buffer.Process("g*");
            Assert.AreEqual("cat", _vimData.SearchHistory.Items.Head);
        }

        /// <summary>
        /// Make sure the cursor positions correctly on the next line 
        /// </summary>
        [Test]
        public void Handle_BraceClose_MiddleOfParagraph()
        {
            Create("dog", "", "cat");
            _buffer.Process("}");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should maintain position but the text should be deleted.  The caret 
        /// exists in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveOnSingle()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Handle_cc_NoAutoIndentShouldRemoveAllOnSingle()
        {
            Create("  dog", "  cat");
            _buffer.LocalSettings.AutoIndent = false;
            _buffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Caret position should be preserved in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Caret point should be preserved in virtual space
        /// </summary>
        [Test]
        public void Handle_cc_AutoIndentShouldPreserveFirstOneOnMultiple()
        {
            Create("    dog", "  cat", "  tree");
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(4, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_cc_NoAutoIndentShouldRemoveAllOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _buffer.LocalSettings.AutoIndent = false;
            _buffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_cb_DeleteWhitespaceAtEndOfSpan()
        {
            Create("public static void Main");
            _textView.MoveCaretTo(19);
            _buffer.Process("cb");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual("public static Main", _textView.GetLine(0).GetText());
            Assert.AreEqual(14, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_cl_WithCountShouldDeleteWhitespace()
        {
            Create("dog   cat");
            _buffer.Process("5cl");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(" cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Handle_d_WithMarkLineMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.MarkMap.SetMark(_textView.GetLine(1).Start, 'a');
            _buffer.Process("d'a");
            Assert.AreEqual("bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_d_WithMarkMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.MarkMap.SetMark(_textView.GetLine(1).Start.Add(1), 'a');
            _buffer.Process("d`a");
            Assert.AreEqual("at", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Even though the motion will include the second line it should not 
        /// be included in the delete operation.  This hits the special case
        /// listed in :help exclusive
        /// </summary>
        [Test]
        public void Handle_d_WithParagraphMotion()
        {
            Create("dog", "", "cat");
            _buffer.Process("d}");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_f_WithTabTarget()
        {
            Create("dog\tcat");
            _buffer.Process("f\t");
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_Minus_MiddleOfBuffer()
        {
            Create("dog", "  cat", "bear");
            _textView.MoveCaretToLine(2);
            _buffer.Process("-");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Escape should exit one time normal mode and return back to the previous mode
        /// </summary>
        [Test]
        public void OneTimeNormalMode_EscapeShouldExit()
        {
            Create("");
            _buffer.Process("i");
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        /// <summary>
        /// A putafter at the end of the line should still put the text after the caret
        /// </summary>
        [Test]
        public void PutAfter_EndOfLine()
        {
            Create("dog");
            _textView.MoveCaretTo(2);
            Assert.AreEqual('g', _textView.GetCaretPoint().GetChar());
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _buffer.Process('p');
            Assert.AreEqual("dogcat", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// A putafter on an empty line is the only thing that shouldn't move the caret
        /// </summary>
        [Test]
        public void PutAfter_EmptyLine()
        {
            Create("");
            UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
            _buffer.Process('p');
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned at the start of the inserted line
        /// </summary>
        [Test]
        public void PutAfter_LineWiseSimpleString()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig\n", OperationKind.LineWise);
            _buffer.Process("p");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(1).Start);
        }

        /// <summary>
        /// Caret should be positioned at the start of the indent even when autoindent is off
        /// </summary>
        [Test]
        public void PutAfter_LineWiseWithIndent()
        {
            Create("dog", "cat", "bear", "tree");
            UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
            _buffer.LocalSettings.AutoIndent = false;
            _buffer.Process("p");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  pig", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(1).Start.Add(2));
        }

        /// <summary>
        /// Caret should be positioned on the last character of the inserted text
        /// </summary>
        [Test]
        public void PutAfter_CharacterWiseSimpleString()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _buffer.Process("p");
            Assert.AreEqual("dpigog", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When putting a character wise selection which spans over multiple lines into 
        /// the ITextBuffer the caret is positioned at the start of the text and not 
        /// after it as it is with most put operations
        /// </summary>
        [Test]
        public void PutAfter_CharacterWise_MultipleLines()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be");
            _buffer.Process("p");
            Assert.AreEqual("dtree", _textView.GetLine(0).GetText());
            Assert.AreEqual("beog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned after the last character of the inserted text
        /// </summary>
        [Test]
        public void PutAfter_CharacterWiseSimpleString_WithCaretMove()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _buffer.Process("gp");
            Assert.AreEqual("dpigog", _textView.GetLine(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The caret should be positioned at the last character of the first block string
        /// inserted text
        /// </summary>
        [Test]
        public void PutAfter_BlockOverExisting()
        {
            Create("dog", "cat", "bear", "tree");
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("p");
            Assert.AreEqual("daaog", _textView.GetLine(0).GetText());
            Assert.AreEqual("cbbat", _textView.GetLine(1).GetText());
            Assert.AreEqual("bear", _textView.GetLine(2).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The new text should be on new lines at the same indetn and the caret posion should
        /// be the same as puting over existing lines
        /// </summary>
        [Test]
        public void PutAfter_BlockOnNewLines()
        {
            Create("dog");
            _textView.MoveCaretTo(1);
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _buffer.Process("p");
            Assert.AreEqual("doaag", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bb", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This should cause the cursor to be put on the first line after the inserted 
        /// lines
        /// </summary>
        [Test]
        public void PutAfter_LineWise_WithCaretMove()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
            _buffer.Process("gp");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Putting a word which doesn't span multiple lines with indent is simply no 
        /// different than a typically put after command
        /// </summary>
        [Test]
        public void PutAfterWithIndent_Word()
        {
            Create("  dog", "  cat", "fish", "tree");
            UnnamedRegister.UpdateValue("bear", OperationKind.CharacterWise);
            _buffer.Process("]p");
            Assert.AreEqual(" bear dog", _textView.GetLine(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Putting a line should cause the indent to be matched in the second line irrespective
        /// of what the original indent was
        /// </summary>
        [Test]
        public void PutAfterWithIndent_SingleLine()
        {
            Create("  dog", "  cat", "fish", "tree");
            UnnamedRegister.UpdateValue("bear" + Environment.NewLine, OperationKind.LineWise);
            _buffer.Process("]p");
            Assert.AreEqual("  dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Putting a line should cause the indent to be matched in all of the pasted lines 
        /// irrespective of their original indent
        /// </summary>
        [Test]
        public void PutAfterWithIndent_MultipleLines()
        {
            Create("  dog", "  cat");
            UnnamedRegister.UpdateValue("    tree" + Environment.NewLine + "    bear" + Environment.NewLine, OperationKind.LineWise);
            _buffer.Process("]p");
            Assert.AreEqual("  dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
            Assert.AreEqual("  bear", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Putting a character wise block of text which spans multiple lines is the trickiest
        /// version.  It requires that the first line remain unchanged while the subsequent lines
        /// are indeed indented to the proper level
        /// </summary>
        [Test]
        public void PutAfterWithIndent_CharcterWiseOverSeveralLines()
        {
            Create("  dog", "  cat");
            UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be", OperationKind.CharacterWise);
            _buffer.Process("]p");
            Assert.AreEqual(" tree", _textView.GetLine(0).GetText());
            Assert.AreEqual("  be dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("  cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be at the start of the inserted text
        /// </summary>
        [Test]
        public void PutBefore_LineWiseStartOfBuffer()
        {
            Create("dog");
            UnnamedRegister.UpdateValue("pig\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should be positioned at the start of the indented text
        /// </summary>
        [Test]
        public void PutBefore_LineWiseStartOfBufferWithIndent()
        {
            Create("dog");
            UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("  pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be on the first line of the newly inserted lines
        /// </summary>
        [Test]
        public void PutBefore_LineWiseMiddleOfBuffer()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1);
            UnnamedRegister.UpdateValue("fish\ntree\n", OperationKind.LineWise);
            _buffer.Process("P");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("fish", _textView.GetLine(1).GetText());
            Assert.AreEqual("tree", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Character should be on the first line after the inserted lines
        /// </summary>
        [Test]
        public void PutBefore_LineWise_WithCaretMove()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
            _buffer.Process("gP");
            Assert.AreEqual("pig", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void PutBefore_CharacterWiseBlockStringOnExistingLines()
        {
            Create("dog", "cat", "bear", "tree");
            _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateBlockValues("a", "b", "c");
            _buffer.Process("P");
            Assert.AreEqual("adog", _textView.GetLine(0).GetText());
            Assert.AreEqual("bcat", _textView.GetLine(1).GetText());
            Assert.AreEqual("cbear", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetCaretPoint(), _textView.GetLine(0).Start);
        }

        /// <summary>
        /// Putting a word which doesn't span multiple lines with indent is simply no 
        /// different than a typically put after command
        /// </summary>
        [Test]
        public void PutBeforeWithIndent_Word()
        {
            Create("  dog", "  cat", "fish", "tree");
            UnnamedRegister.UpdateValue("bear", OperationKind.CharacterWise);
            _buffer.Process("[p");
            Assert.AreEqual("bear  dog", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Putting a line should cause the indent to be matched in the second line irrespective
        /// of what the original indent was
        /// </summary>
        [Test]
        public void PutBeforeWithIndent_SingleLine()
        {
            Create("  dog", "  cat", "fish", "tree");
            UnnamedRegister.UpdateValue("bear" + Environment.NewLine, OperationKind.LineWise);
            _buffer.Process("[p");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPointInLine(0, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Putting a line should cause the indent to be matched in all of the pasted lines 
        /// irrespective of their original indent
        /// </summary>
        [Test]
        public void PutBeforeWithIndent_MultipleLines()
        {
            Create("  dog", "  cat");
            UnnamedRegister.UpdateValue("    tree" + Environment.NewLine + "    bear" + Environment.NewLine, OperationKind.LineWise);
            _buffer.Process("[p");
            Assert.AreEqual("  tree", _textView.GetLine(0).GetText());
            Assert.AreEqual("  bear", _textView.GetLine(1).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetPointInLine(0, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Putting a character wise block of text which spans multiple lines is the trickiest
        /// version.  It requires that the first line remain unchanged while the subsequent lines
        /// are indeed indented to the proper level
        /// </summary>
        [Test]
        public void PutBeforeWithIndent_CharcterWiseOverSeveralLines()
        {
            Create("  dog", "  cat");
            UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be", OperationKind.CharacterWise);
            _buffer.Process("[p");
            Assert.AreEqual("tree", _textView.GetLine(0).GetText());
            Assert.AreEqual("  be  dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("  cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_s_AtEndOfLine()
        {
            Create("dog", "cat");
            _textView.MoveCaretTo(2);
            _buffer.Process('s');
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual("do", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// This command should only yank from the current line to the end of the file
        /// </summary>
        [Test]
        public void Handle_yG_NonFirstLine()
        {
            Create("dog", "cat", "bear");
            _textView.MoveCaretToLine(1);
            _buffer.Process("yG");
            Assert.AreEqual("cat" + Environment.NewLine + "bear", _buffer.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void IncrementalSearch_VeryNoMagic()
        {
            Create("dog", "cat");
            _buffer.Process(@"/\Vog", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the caret goes to column 0 on the next line even if one of the 
        /// motion adjustment applies (:help exclusive-linewise)
        /// </summary>
        [Test]
        public void IncrementalSearch_CaretOnColumnZero()
        {
            Create("hello", "world");
            _textView.MoveCaretTo(2);
            _buffer.Process("/world", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void IncrementalSearch_CaseSensitive()
        {
            Create("dogDOG", "cat");
            _buffer.Process(@"/\COG", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The case option in the search string should take precedence over the 
        /// ignore case option
        /// </summary>
        [Test]
        public void IncrementalSearch_CaseSensitiveAgain()
        {
            Create("hello dog DOG");
            _globalSettings.IgnoreCase = true;
            _buffer.Process(@"/\CDOG", enter: true);
            Assert.AreEqual(10, _textView.GetCaretPoint());
        }

        [Test]
        public void IncrementalSearch_HandlesEscape()
        {
            Create("dog");
            _buffer.Process("/do");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_HandlesEscapeInOperator()
        {
            Create("dog");
            _buffer.Process("d/do");
            _buffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_UsedAsOperatorMotion()
        {
            Create("dog cat tree");
            _buffer.Process("d/cat", enter: true);
            Assert.AreEqual("cat tree", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_DontMoveCaretDuringSearch()
        {
            Create("dog cat tree");
            _buffer.Process("/cat");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_MoveCaretAfterEnter()
        {
            Create("dog cat tree");
            _buffer.Process("/cat", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Mark_SelectionEndIsExclusive()
        {
            Create("the brown dog");
            var span = new SnapshotSpan(_textView.GetPoint(4), _textView.GetPoint(9));
            Assert.AreEqual("brown", span.GetText());
            _textView.Selection.Select(span);
            _textView.Selection.Clear();
            _buffer.Process("y`>");
            Assert.AreEqual("the brow", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void Mark_NamedMarkIsExclusive()
        {
            Create("the brown dog");
            var point = _textView.GetPoint(8);
            Assert.AreEqual('n', point.GetChar());
            _buffer.MarkMap.SetMark(point, 'b');
            _buffer.Process("y`b");
            Assert.AreEqual("the brow", _buffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void MatchingToken_Parens()
        {
            Create("cat( )");
            _buffer.Process('%');
            Assert.AreEqual(5, _textView.GetCaretPoint());
            _buffer.Process('%');
            Assert.AreEqual(3, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the caret is properly positioned against a join across 3 lines
        /// </summary>
        [Test]
        public void Join_CaretPositionThreeLines()
        {
            Create("cat", "dog", "bear");
            _buffer.Process("3J");
            Assert.AreEqual("cat dog bear", _textView.GetLine(0).GetText());
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Ensure the text inserted is repeated after the Escape
        /// </summary>
        [Test]
        public void InsertLineBelowCaret_WithCount()
        {
            Create("dog", "bear");
            _buffer.Process("2o");
            _buffer.Process("cat");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("bear", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the text is repeated
        /// </summary>
        [Test]
        public void InsertAtEndOfLine_WithCount()
        {
            Create("dog", "bear");
            _buffer.Process("3A");
            _buffer.Process('b');
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dogbbb", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure the matching token behavior fits all of the issues described in 
        /// issue 468
        /// </summary>
        [Test]
        public void MatchingTokens_Issue468()
        {
            Create("(wchar_t*) realloc(pwcsSelFile, (nSelFileLen+1)*sizeof(wchar_t))");

            // First open paren to the next closing one
            _buffer.Process("%");
            Assert.AreEqual(9, _textView.GetCaretPoint().Position);

            // From the first closing paren back to the start
            _buffer.Process("%");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);

            // From the second opening paren to the last one
            var lastPoint = _textView.TextSnapshot.GetEndPoint().Subtract(1);
            Assert.AreEqual(')', lastPoint.GetChar());
            _textView.MoveCaretTo(18);
            Assert.AreEqual('(', _textView.GetCaretPoint().GetChar());
            _buffer.Process("%");
            Assert.AreEqual(lastPoint, _textView.GetCaretPoint());

            // And back to the start one
            _buffer.Process("%");
            Assert.AreEqual(18, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we jump correctly between matching token values of different types
        ///
        /// TODO: This test is also broken due to the matching case not being able to 
        /// come of the '/' in a '*/'
        /// </summary>
        [Test]
        public void MatchingTokens_DifferentTypes()
        {
            Create("{ { (( } /* a /*) b */ })");
            Action<int, int> del = (start, end) =>
                {
                    _textView.MoveCaretTo(start);
                    _buffer.Process("%");
                    Assert.AreEqual(end, _textView.GetCaretPoint().Position);

                    if (start != end)
                    {
                        _textView.MoveCaretTo(end);
                        _buffer.Process("%");
                        Assert.AreEqual(start, _textView.GetCaretPoint().Position);
                    }
                };
            del(0, 23);
            del(2, 7);
            del(4, 24);
            del(5, 16);
            del(9, 21);
        }

        /// <summary>
        /// Make sure repeat last char search is functioning
        /// </summary>
        [Test]
        public void RepeatLastCharSearch_Forward()
        {
            Create("hello", "world");
            _buffer.Process("fr");
            _textView.MoveCaretToLine(1);
            _buffer.Process(";");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// The repeat last char search command shouldn't toggle itself.  Or in short it should be
        /// possible to scan an entire line in one direction
        /// </summary>
        [Test]
        public void RepeatLastCharSearch_ManyTimes()
        {
            Create("hello world dog");
            _buffer.VimData.LastCharSearch = FSharpOption.Create(Tuple.Create(CharSearchKind.ToChar, Path.Forward, 'o'));
            _textView.MoveCaretTo(_textView.GetEndPoint().Subtract(1));
            _buffer.Process(',');
            Assert.AreEqual(Path.Forward, _buffer.VimData.LastCharSearch.Value.Item2);
            Assert.AreEqual(13, _textView.GetCaretPoint().Position);
            _buffer.Process(',');
            Assert.AreEqual(Path.Forward, _buffer.VimData.LastCharSearch.Value.Item2);
            Assert.AreEqual(7, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Enter should not go through normal mode mapping during an incremental search
        /// </summary>
        [Test]
        public void Remap_EnterShouldNotMapDuringSearch()
        {
            Create("cat dog");
            _keyMap.MapWithNoRemap("<Enter>", "o<Esc>", KeyRemapMode.Normal);
            _buffer.Process("/dog");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure the commands map properly
        /// </summary>
        [Test]
        public void Remap_Issue474()
        {
            Create("cat", "dog", "bear", "pig", "tree", "fish");
            _buffer.Process(":nnoremap gj J");
            _buffer.Process(VimKey.Enter);
            _buffer.Process(":map J 4j");
            _buffer.Process(VimKey.Enter);
            _buffer.Process("J");
            Assert.AreEqual(4, _textView.GetCaretLine().LineNumber);
            _textView.MoveCaretTo(0);
            _buffer.Process("gj");
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Incremental search should re-use the last search if the entered search string is
        /// empty.  It should ignore the direction though and base it's search off the '/' or
        /// '?' it was created with
        /// </summary>
        [Test]
        public void LastSearch_IncrementalReuse()
        {
            Create("dog cat dog");
            _textView.MoveCaretTo(1);
            _buffer.LocalSettings.GlobalSettings.WrapScan = false;
            _buffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog", Path.Backward);
            _buffer.Process('/');
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(8, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Substitute command should set the LastSearch value
        /// </summary>
        [Test]
        public void LastSearch_SetBySubstitute()
        {
            Create("dog cat dog");
            _buffer.Process(":s/dog/cat", enter: true);
            Assert.AreEqual("dog", _buffer.VimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Substitute command should re-use the LastSearch value if there is no specific 
        /// search value set
        /// </summary>
        [Test]
        public void LastSearch_UsedBySubstitute()
        {
            Create("dog cat dog");
            _buffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _buffer.Process(":s//cat", enter: true);
            Assert.AreEqual("cat cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// The search options used by a :s command should not be stored.  For example the 
        /// 'i' flag is used only for the :s command and not for repeats of the search 
        /// later on.
        /// </summary>
        [Test]
        public void LastSearch_DontStoreSearchOptions()
        {
            Create("cat", "dog", "cat");
            _assertOnErrorMessage = false;
            _globalSettings.IgnoreCase = false;
            _globalSettings.WrapScan = true;
            _textView.MoveCaretToLine(2);
            _buffer.Process(":s/CAT/fish/i", enter: true);
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            var didHit = false;
            _buffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("CAT"), message);
                    didHit = true;
                };
            _buffer.Process("n");
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// Delete with an append register should concatenate the values
        /// </summary>
        [Test]
        public void Delete_Append()
        {
            Create("dog", "cat", "fish");
            _buffer.Process("\"cyaw");
            _buffer.Process("j");
            _buffer.Process("\"Cdw");
            Assert.AreEqual("dogcat", _buffer.RegisterMap.GetRegister('c').StringValue);
            Assert.AreEqual("dogcat", _buffer.RegisterMap.GetRegister('C').StringValue);
            _buffer.Process("\"cp");
            Assert.AreEqual("dogcat", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure that 'd0' is interpreted correctly as 'd{motion}' and not 'd#d'.  0 is not 
        /// a count
        /// </summary>
        [Test]
        public void Delete_BeginingOfLine()
        {
            Create("dog");
            _textView.MoveCaretTo(1);
            _buffer.Process("d0");
            Assert.AreEqual("og", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Deleting a word left at the start of the line results in empty data and
        /// should not cause the register contents to be altered
        /// </summary>
        [Test]
        public void Delete_LeftAtStartOfLine()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("hello");
            _buffer.Process("dh");
            Assert.AreEqual("hello", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _vimHost.BeepCount);
        }

        /// <summary>
        /// Delete when combined with the line down motion 'j' should delete two lines
        /// since it's deleting the result of the motion from the caret
        ///
        /// Convered by issue 288
        /// </summary>
        [Test]
        public void Delete_LineDown()
        {
            Create("abc", "def", "ghi", "jkl");
            _textView.MoveCaretTo(1);
            _buffer.Process("dj");
            Assert.AreEqual("ghi", _textView.GetLine(0).GetText());
            Assert.AreEqual("jkl", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint());
        }

        /// <summary>
        /// When a delete of a search motion which wraps occurs a warning message should
        /// be displayed
        /// </summary>
        [Test]
        public void Delete_SearchWraps()
        {
            Create("dog", "cat", "tree");
            var didHit = false;
            _textView.MoveCaretToLine(1);
            _assertOnWarningMessage = false;
            _buffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _buffer.Process("d/dog", enter: true);
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// Delete a word at the end of the line.  It should not delete the line break
        /// </summary>
        [Test]
        public void Delete_WordEndOfLine()
        {
            Create("the cat", "chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("dw");
            Assert.AreEqual("the ", _textView.GetLine(0).GetText());
            Assert.AreEqual("chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Delete a word at the end of the line where the next line doesn't start in column
        /// 0.  This should still not cause the end of the line to delete
        /// </summary>
        [Test]
        public void Delete_WordEndOfLineNextStartNotInColumnZero()
        {
            Create("the cat", "  chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("dw");
            Assert.AreEqual("the ", _textView.GetLine(0).GetText());
            Assert.AreEqual("  chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Delete across a line where the search ends in white space but not inside of 
        /// column 0
        /// </summary>
        [Test]
        public void Delete_SearchAcrossLineNotInColumnZero()
        {
            Create("the cat", "  chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("d/cha", enter: true);
            Assert.AreEqual("the chased the bird", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Delete across a line where the search ends in column 0 of the next line
        /// </summary>
        [Test]
        public void Delete_SearchAcrossLineIntoColumnZero()
        {
            Create("the cat", "chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("d/cha", enter: true);
            Assert.AreEqual("the ", _textView.GetLine(0).GetText());
            Assert.AreEqual("chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Don't delete the new line when doing a 'daw' at the end of the line
        /// </summary>
        [Test]
        public void Delete_AllWordEndOfLineIntoColumnZero()
        {
            Create("the cat", "chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("daw");
            Assert.AreEqual("the", _textView.GetLine(0).GetText());
            Assert.AreEqual("chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Delete a word at the end of the line where the next line doesn't start in column
        /// 0.  This should still not cause the end of the line to delete
        /// </summary>
        [Test]
        public void Delete_AllWordEndOfLineNextStartNotInColumnZero()
        {
            Create("the cat", "  chased the bird");
            _textView.MoveCaretTo(4);
            _buffer.Process("daw");
            Assert.AreEqual("the", _textView.GetLine(0).GetText());
            Assert.AreEqual("  chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Delete lines with the special d#d count syntax
        /// </summary>
        [Test]
        public void DeleteLines_Special_Simple()
        {
            Create("cat", "dog", "bear", "fish");
            _buffer.Process("d2d");
            Assert.AreEqual("bear", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(2, _textBuffer.CurrentSnapshot.LineCount);
        }


        /// <summary>
        /// Delete lines with both counts and make sure the counts are multiplied together
        /// </summary>
        [Test]
        public void DeleteLines_Special_TwoCounts()
        {
            Create("cat", "dog", "bear", "fish", "tree");
            _buffer.Process("2d2d");
            Assert.AreEqual("tree", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(1, _textBuffer.CurrentSnapshot.LineCount);
        }

        /// <summary>
        /// Make sure we properly update register 0 during a yank
        /// </summary>
        [Test]
        public void Yank_Register0()
        {
            Create("dog", "cat", "fish");
            _buffer.Process("yaw");
            _textView.MoveCaretToLine(1);
            _buffer.Process("\"cyaw");
            _textView.MoveCaretToLine(2);
            _buffer.Process("dw");
            _buffer.Process("\"0p");
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
        }

        /// <summary>
        /// Where there are not section boundaries between the caret and the end of the 
        /// ITextBuffer the entire ITextBuffer should be yanked when section forward 
        /// is used
        /// </summary>
        [Test]
        public void Yank_SectionForwardToEndOfBuffer()
        {
            Create("dog", "cat", "bear");
            _buffer.Process("y]]");
            Assert.AreEqual("dog" + Environment.NewLine + "cat" + Environment.NewLine + "bear", UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Yanking with an append register should concatenate the values
        /// </summary>
        [Test]
        public void Yank_Append()
        {
            Create("dog", "cat", "fish");
            _buffer.Process("\"cyaw");
            _buffer.Process("j");
            _buffer.Process("\"Cyaw");
            Assert.AreEqual("dogcat", _buffer.RegisterMap.GetRegister('c').StringValue);
            Assert.AreEqual("dogcat", _buffer.RegisterMap.GetRegister('C').StringValue);
        }

        /// <summary>
        /// Trying to char left from the start of the line should not cause a beep to 
        /// be emitted.  However it should cause the targetted register to be updated 
        /// </summary>
        [Test]
        public void Yank_EmptyCharLeftMotion()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateValue("hello");
            _textView.MoveCaretToLine(1);
            _buffer.Process("yh");
            Assert.AreEqual("", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _vimHost.BeepCount);
        }

        /// <summary>
        /// Yanking a line down from the end of the buffer should not cause the 
        /// unnamed register text from resetting and it should cause a beep to occur
        /// </summary>
        [Test]
        public void Yank_LineDownAtEndOfBuffer()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1);
            UnnamedRegister.UpdateValue("hello");
            _buffer.Process("yj");
            Assert.AreEqual(1, _vimHost.BeepCount);
            Assert.AreEqual("hello", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// A yank of a search which needs no wrap but doesn't wrap should raise an 
        /// error message
        /// </summary>
        [Test]
        public void Yank_WrappingSearch()
        {
            Create("dog", "cat", "dog", "fish");
            _globalSettings.WrapScan = false;
            _textView.MoveCaretToLine(2);
            _assertOnErrorMessage = false;

            var didSee = false;
            _buffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_SearchHitBottomWithout(@"\<dog\>"), message);
                    didSee = true;
                };
            _buffer.Process("y*");
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Doing a word yank from a blank should yank the white space till the start of 
        /// the next word 
        /// </summary>
        [Test]
        public void Yank_WordFromBlank()
        {
            Create("dog cat  ball");
            _textView.MoveCaretTo(3);
            _buffer.Process("yw");
            Assert.AreEqual(" ", UnnamedRegister.StringValue);
            _textView.MoveCaretTo(7);
            _buffer.Process("yw");
            Assert.AreEqual("  ", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yanking a word in a blank line should yank the line and be a linewise motion
        /// </summary>
        [Test]
        public void Yank_WordInEmptyLine()
        {
            Create("dog", "", "cat");
            _textView.MoveCaretToLine(1);
            _buffer.Process("yw");
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
            Assert.AreEqual(Environment.NewLine, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yanking a word in a blank line with white space in the following line should 
        /// ignore the white space in the following line
        /// </summary>
        [Test]
        public void Yank_WordInEmptyLineWithWhiteSpaceInFollowing()
        {
            Create("dog", "", "  cat");
            _textView.MoveCaretToLine(1);
            _buffer.Process("yw");
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
            Assert.AreEqual(Environment.NewLine, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yanking a word which includes a blank line should still be line wise if it started at 
        /// the beginning of the previous word
        /// </summary>
        [Test]
        public void Yank_WordEndInEmptyLine()
        {
            Create("dog", "", "cat");
            _buffer.Process("y2w");
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
            Assert.AreEqual("dog" + Environment.NewLine + Environment.NewLine, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yanking a word which includes a blank line should not be line wise if it starts in 
        /// the middle of a word
        /// </summary>
        [Test]
        public void Yank_WordMiddleEndInEmptyLin()
        {
            Create("dog", "", "cat");
            _textView.MoveCaretTo(1);
            _buffer.Process("y2w");
            Assert.AreEqual(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
            Assert.AreEqual("og" + Environment.NewLine, UnnamedRegister.StringValue);
            _buffer.Process("p");
            Assert.AreEqual("doog", _textView.GetLine(0).GetText());
            Assert.AreEqual("g", _textView.GetLine(1).GetText());
            Assert.AreEqual("", _textView.GetLine(2).GetText());
            Assert.AreEqual("cat", _textView.GetLine(3).GetText());
        }

        /// <summary>
        /// A yank which wraps around the buffer should just be a backwards motion and 
        /// shouldn't cause an error or warning message to be displayed
        /// </summary>
        [Test]
        public void Yank_WrappingSearchSucceeds()
        {
            Create("dog", "cat", "dog", "fish");
            var didHit = false;
            _buffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _assertOnWarningMessage = false;
            _globalSettings.WrapScan = true;
            _textView.MoveCaretToLine(2);

            _buffer.Process("y/dog", enter: true);
            Assert.AreEqual("dog" + Environment.NewLine + "cat" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// A yank of a search which has no match should raise an error 
        /// </summary>
        [Test]
        public void Yank_SearchMotionWithNoResult()
        {
            Create("dog", "cat", "dog", "fish");
            _globalSettings.WrapScan = false;
            _textView.MoveCaretToLine(2);
            _assertOnErrorMessage = false;

            var didSee = false;
            _buffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("bug"), message);
                    didSee = true;
                };
            _buffer.Process("y/bug", enter: true);
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Doing an 'iw' yank from the start of the word should yank just the word
        /// </summary>
        [Test]
        public void Yank_InnerWord_FromWordStart()
        {
            Create("the dog chased the ball");
            _buffer.Process("yiw");
            Assert.AreEqual("the", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Doing an 'iw' yank with a count of 2 should yank the word and the trailing
        /// white space
        /// </summary>
        [Test]
        public void Yank_InnerWord_FromWordStartWithCount()
        {
            Create("the dog chased the ball");
            _buffer.Process("y2iw");
            Assert.AreEqual("the ", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Doing an 'iw' from white space should yank the white space
        /// </summary>
        [Test]
        public void Yank_InnerWord_FromWhiteSpace()
        {
            Create("the dog chased the ball");
            _textView.MoveCaretTo(3);
            _buffer.Process("y2iw");
            Assert.AreEqual(" dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yanking a word across new lines should not count the new line as a word. Odd since
        /// most white space is counted
        /// </summary>
        [Test]
        public void Yank_InnerWord_AcrossNewLine()
        {
            Create("cat", "dog", "bear");
            _buffer.Process("y2iw");
            Assert.AreEqual("cat" + Environment.NewLine + "dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yank lines using the special y#y syntax
        /// </summary>
        [Test]
        public void YankLines_Special_Simple()
        {
            Create("cat", "dog", "bear");
            _buffer.Process("y2y");
            Assert.AreEqual("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// A yank of a jump motion should update the jump list
        /// </summary>
        [Test]
        public void JumpList_YankMotionShouldUpdate()
        {
            Create("cat", "dog", "cat");
            _buffer.Process("y*");
            Assert.IsTrue(_jumpList.Current.IsSome(_textView.GetPoint(0)));
            Assert.AreEqual("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Doing a * on a word that doesn't even match should still update the jump list
        /// </summary>
        [Test]
        public void JumpList_NextWordUnderCursorWithNoMatch()
        {
            Create("cat", "dog", "fish");
            var didHit = false;
            _buffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _assertOnWarningMessage = false;
            _buffer.Process("*");
            _textView.MoveCaretToLine(2);
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(_textView.GetPoint(0), _textView.GetCaretPoint());
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// If a jump to previous occurs on a location which is not in the list and we
        /// are not already traversing the jump list then the location is added
        /// </summary>
        [Test]
        public void JumpList_FromLocationNotInList()
        {
            Create("cat", "dog", "fish");
            _jumpList.Add(_textView.GetPoint(0));
            _textView.MoveCaretToLine(1);
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(_textView.GetLine(0).Start, _textView.GetCaretPoint());
            Assert.AreEqual(1, _jumpList.CurrentIndex.Value);
            Assert.AreEqual(2, _jumpList.Jumps.Length);
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the caret is positioned properly during undo
        /// </summary>
        [Test]
        public void Undo_DeleteAllWord()
        {
            Create("cat", "dog");
            _textView.MoveCaretTo(1);
            _buffer.Process("daw");
            _buffer.Process("u");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Undoing a change lines for a single line should put the caret at the start of the
        /// line which was changed
        /// </summary>
        [Test]
        public void Undo_ChangeLines_OneLine()
        {
            Create("  cat");
            _textView.MoveCaretTo(4);
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("cc");
            _buffer.Process(VimKey.Escape);
            _buffer.Process("u");
            Assert.AreEqual("  cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Undoing a change lines for a multiple lines should put the caret at the start of the
        /// second line which was changed.  
        /// </summary>
        [Test]
        public void Undo_ChangeLines_MultipleLines()
        {
            Create("dog", "  cat", "  bear", "  tree");
            _textView.MoveCaretToLine(1);
            _buffer.LocalSettings.AutoIndent = true;
            _buffer.Process("3cc");
            _buffer.Process(VimKey.Escape);
            _buffer.Process("u");
            Assert.AreEqual("dog", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(_textView.GetPointInLine(2, 2), _textView.GetCaretPoint());
        }
    }
}
