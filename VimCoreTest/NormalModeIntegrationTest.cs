using System;
using System.Linq;
using EditorUtils.UnitTest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    /// <summary>
    /// Class for testing the full integration story of normal mode in VsVim
    /// </summary>
    [TestFixture]
    public sealed class NormalModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IVimTextBuffer _vimTextBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private IJumpList _jumpList;
        private IKeyMap _keyMap;
        private IVimData _vimData;
        private IFoldManager _foldManager;
        private MockVimHost _vimHost;
        private TestableClipboardDevice _clipboardDevice;
        private bool _assertOnErrorMessage = true;
        private bool _assertOnWarningMessage = true;

        internal Register UnnamedRegister
        {
            get { return _vimBuffer.GetRegister(RegisterName.Unnamed); }
        }

        public void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.ErrorMessage +=
                (_, message) =>
                {
                    if (_assertOnErrorMessage)
                    {
                        Assert.Fail("Error Message: " + message);
                    }
                };
            _vimBuffer.WarningMessage +=
                (_, message) =>
                {
                    if (_assertOnWarningMessage)
                    {
                        Assert.Fail("Warning Message: " + message);
                    }
                };
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _keyMap = _vimBuffer.Vim.KeyMap;
            _localSettings = _vimBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _jumpList = _vimBuffer.JumpList;
            _vimHost = (MockVimHost)_vimBuffer.Vim.VimHost;
            _vimHost.BeepCount = 0;
            _vimData = Vim.VimData;
            _foldManager = FoldManagerFactory.GetFoldManager(_textView);
            _clipboardDevice = (TestableClipboardDevice)CompositionContainer.GetExportedValue<IClipboardDevice>();

            // Many of the operations operate on both the visual and edit / text snapshot
            // simultaneously.  Ensure that our setup code is producing a proper IElisionSnapshot
            // for the Visual portion so we can root out any bad mixing of instances between
            // the two
            Assert.IsTrue(_textView.VisualSnapshot is IElisionSnapshot);
            Assert.IsTrue(_textView.VisualSnapshot != _textView.TextSnapshot);
        }

        /// <summary>
        /// Make sure we jump across the blanks to get to the word and that the caret is 
        /// properly positioned
        /// </summary>
        [Test]
        public void AddToWord_Decimal()
        {
            Create(" 999");
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('a'));
            Assert.AreEqual(" 1000", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Negative decimal number
        /// </summary>
        [Test]
        public void AddToWord_Decimal_Negative()
        {
            Create(" -10");
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('a'));
            Assert.AreEqual(" -9", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Add to the word on the non-first line.  Ensures we are calculating the replacement span
        /// in the correct location
        /// </summary>
        [Test]
        public void AddToWord_Hex_SecondLine()
        {
            Create("hello", "  0x42");
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('a'));
            Assert.AreEqual("  0x43", _textBuffer.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(5), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Caret should maintain position but the text should be deleted.  The caret 
        /// exists in virtual space
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndentShouldPreserveOnSingle()
        {
            Create("  dog", "  cat", "  tree");
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        [Test]
        public void ChangeLines_NoAutoIndentShouldRemoveAllOnSingle()
        {
            Create("  dog", "  cat");
            _vimBuffer.LocalSettings.AutoIndent = false;
            _vimBuffer.Process("cc");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Caret position should be preserved in virtual space
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndentShouldPreserveOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Caret point should be preserved in virtual space
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndentShouldPreserveFirstOneOnMultiple()
        {
            Create("    dog", "  cat", "  tree");
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(4, _textView.GetCaretVirtualPoint().VirtualSpaces);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void ChangeLines_NoAutoIndentShouldRemoveAllOnMultiple()
        {
            Create("  dog", "  cat", "  tree");
            _vimBuffer.LocalSettings.AutoIndent = false;
            _vimBuffer.Process("2cc");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("  tree", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// When 'autoindent' is on we need to keep tabs and spaces at the start of the line
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndent_KeepTabsAndSpaces()
        {
            Create("\t  dog", "\t  cat");
            _localSettings.AutoIndent = true;
            _localSettings.TabStop = 4;
            _localSettings.ExpandTab = false;
            _vimBuffer.Process("ccb");
            Assert.AreEqual("\t  b", _textView.GetLine(0).GetText());
            Assert.AreEqual("\t  cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPoint(4), _textView.GetCaretPoint());
        }

        /// <summary>
        /// When 'autoindent' is on we need to keep tabs at the start of the line
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndent_KeepTabs()
        {
            Create("\tdog", "\tcat");
            _localSettings.AutoIndent = true;
            _localSettings.TabStop = 4;
            _localSettings.ExpandTab = false;
            _vimBuffer.Process("ccb");
            Assert.AreEqual("\tb", _textView.GetLine(0).GetText());
            Assert.AreEqual("\tcat", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetPoint(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// When there are tabs involved the virtual space position of the caret after a 
        /// 'cc' operation should be (tabs * tabWidth + spaces)
        /// </summary>
        [Test]
        public void ChangeLines_AutoIndent_VirtualSpace()
        {
            Create("\t  dog", "\t cat");
            _localSettings.AutoIndent = true;
            _localSettings.TabStop = 4;
            _vimBuffer.Process("cc");
            Assert.AreEqual(6, _textView.GetCaretVirtualPoint().VirtualSpaces);
        }

        /// <summary>
        /// Test the repeat of a repeated command.  Essentially ensure the act of repeating doesn't
        /// disturb the cached LastCommand value
        /// </summary>
        [Test]
        public void RepeatCommand_Repeated()
        {
            Create("the fox chased the bird");
            _vimBuffer.Process("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _vimBuffer.Process(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _vimBuffer.Process(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange1()
        {
            Create("the fox chased the bird");
            _vimBuffer.Process("cw");
            _vimBuffer.Process("hey ");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange2()
        {
            Create("the fox chased the bird");
            _vimBuffer.Process("cw");
            _vimBuffer.Process("hey");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void RepeatCommand_LinkedTextChange3()
        {
            Create("the fox chased the bird");
            _vimBuffer.Process("cw");
            _vimBuffer.Process("hey");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
            _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("A d with Enter should delete the line break")]
        public void Issue317_1()
        {
            Create("dog", "cat", "jazz", "band");
            _vimBuffer.Process("2d", enter: true);
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Verify the contents after with a paste")]
        public void Issue317_2()
        {
            Create("dog", "cat", "jazz", "band");
            _vimBuffer.Process("2d", enter: true);
            _vimBuffer.Process("p");
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
            _vimBuffer.Process(KeyInputUtil.EnterKey);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Simple maintain of the caret column going down
        /// </summary>
        [Test]
        public void MaintainCaretColumn_Down()
        {
            Create("the dog chased the ball", "hello", "the cat climbed the tree");
            _textView.MoveCaretTo(8);
            _vimBuffer.Process('j');
            Assert.AreEqual(_textView.GetPointInLine(1, 4), _textView.GetCaretPoint());
            _vimBuffer.Process('j');
            Assert.AreEqual(_textView.GetPointInLine(2, 8), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Simple maintain of the caret column going up
        /// </summary>
        [Test]
        public void MaintainCaretColumn_Up()
        {
            Create("the dog chased the ball", "hello", "the cat climbed the tree");
            _textView.MoveCaretTo(_textView.GetPointInLine(2, 8));
            _vimBuffer.Process('k');
            Assert.AreEqual(_textView.GetPointInLine(1, 4), _textView.GetCaretPoint());
            _vimBuffer.Process('k');
            Assert.AreEqual(_textView.GetPointInLine(0, 8), _textView.GetCaretPoint());
        }

        /// <summary>
        /// The column should not be maintained once the caret goes any other direction
        /// </summary>
        [Test]
        public void MaintainCaretColumn_ResetOnMove()
        {
            Create("the dog chased the ball", "hello", "the cat climbed the tree");
            _textView.MoveCaretTo(_textView.GetPointInLine(2, 8));
            _vimBuffer.Process("kh");
            Assert.AreEqual(_textView.GetPointInLine(1, 3), _textView.GetCaretPoint());
            _vimBuffer.Process('k');
            Assert.AreEqual(_textView.GetPointInLine(0, 3), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the caret column is properly maintained when we have to account for mixed
        /// tabs and spaces on the preceeding line
        /// </summary>
        [Test]
        public void MaintainCaretColumn_MixedTabsAndSpaces()
        {
            Create("    alpha", "\tbrought", "tac", "    dog");
            _localSettings.TabStop = 4;
            _textView.MoveCaretTo(4);
            foreach (var c in "abcd")
            {
                Assert.AreEqual(c.ToString(), _textView.GetCaretPoint().GetChar().ToString());
                _vimBuffer.Process('j');
            }
        }

        /// <summary>
        /// When spaces don't divide evenly into tabs the transition into a tab
        /// should land on the tab
        /// </summary>
        [Test]
        public void MaintainCaretColumn_SpacesDoNotDivideToTabs()
        {
            Create("    alpha", "\tbrought", "cat");
            _localSettings.TabStop = 4;
            _textView.MoveCaretTo(2);
            Assert.AreEqual(' ', _textView.GetCaretPoint().GetChar());
            _vimBuffer.Process('j');
            Assert.AreEqual(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            _vimBuffer.Process('j');
            Assert.AreEqual(_textBuffer.GetPointInLine(2, 2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// When spaces overlap a tab stop length we need to modulus and apply the 
        /// remaining spaces
        /// </summary>
        [Test]
        public void MaintainCaretColumn_SpacesOverlapTabs()
        {
            Create("    alpha", "\tbrought", "cat");
            _localSettings.TabStop = 2;
            _textView.MoveCaretTo(4);
            Assert.AreEqual('a', _textView.GetCaretPoint().GetChar());
            _vimBuffer.Process('j');
            Assert.AreEqual(_textBuffer.GetPointInLine(1, 3), _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void Motion_Section1()
        {
            Create("hello", "{world");
            _vimBuffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void Motion_Section2()
        {
            Create("hello", "\fworld");
            _vimBuffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section3()
        {
            Create("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _vimBuffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section4()
        {
            Create("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _vimBuffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void Motion_Section5()
        {
            Create("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _vimBuffer.Process("[[");
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
            _vimBuffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Move the caret using the end of word motion repeatedly
        /// </summary>
        [Test]
        public void Motion_MoveEndOfWord()
        {
            Create("the cat chases the dog");
            _vimBuffer.Process("e");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("e");
            Assert.AreEqual(6, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("e");
            Assert.AreEqual(13, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("e");
            Assert.AreEqual(17, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("e");
            Assert.AreEqual(21, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The 'w' needs to be able to get off of a blank line
        /// </summary>
        [Test]
        public void Motion_MoveWordAcrossBlankLine()
        {
            Create("dog", "", "cat ball");
            _vimBuffer.Process("w");
            Assert.AreEqual(_textView.GetPointInLine(1, 0), _textView.GetCaretPoint());
            _vimBuffer.Process("w");
            Assert.AreEqual(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
            _vimBuffer.Process("w");
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
            _vimBuffer.Process("w");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("w");
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
            _vimBuffer.Process("b");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("b");
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
            _vimBuffer.Process("b");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("b");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Blank lines are sentences
        /// </summary>
        [Test]
        public void Move_SentenceForBlankLine()
        {
            Create("dog.  ", "", "cat");
            _vimBuffer.Process(")");
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
            _vimBuffer.LocalSettings.GlobalSettings.WrapScan = true;
            _vimBuffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _vimBuffer.Process("/dog", enter: true);
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
            _vimBuffer.Process("}");
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
            _vimBuffer.Process("{");
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
            _vimBuffer.Process("{");
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
            _vimBuffer.Process("][");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _vimBuffer.Process("][");
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
            _vimBuffer.Process("][");
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            _textView.MoveCaretToLine(1, 3);
            _vimBuffer.Process("[]");
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
                _vimBuffer.Process("][");
                Assert.AreEqual(_textView.GetLine(i + 1).Start, _textView.GetCaretPoint());
            }

            // And now backward
            for (var i = 0; i < 4; i++)
            {
                _vimBuffer.Process("[]");
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
            _vimBuffer.Process("][");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _vimBuffer.Process("][");
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
            _vimBuffer.Process('%');
            Assert.AreEqual(7, _textView.GetCaretPoint());
            _vimBuffer.Process('%');
            Assert.AreEqual(0, _textView.GetCaretPoint());
            _vimBuffer.Process('%');
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
            _vimBuffer.Process("][");
            Assert.AreEqual(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
            _textView.MoveCaretTo(1);
            _vimBuffer.Process("][");
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
            _vimBuffer.Process("d}");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("pig", _textView.GetLine(1).GetText());
            _vimBuffer.Process("p");
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
            _vimBuffer.Process("_");
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
            _vimBuffer.Process("w");
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            _vimBuffer.Process("w");
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
            _vimBuffer.Process("yaw");
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
            _vimBuffer.Process("yaw");
            Assert.AreEqual(" cat", UnnamedRegister.StringValue);
        }

        [Test]
        public void RepeatLastSearch1()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _vimBuffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(0);
            _vimBuffer.Process('n');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch2()
        {
            Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _vimBuffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _vimBuffer.Process('n');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch3()
        {
            Create("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
            _vimBuffer.Process("/pig", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _vimBuffer.Process('N');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// A change word operation shouldn't delete the whitespace trailing the word
        /// </summary>
        [Test]
        public void Change_Word()
        {
            Create("dog cat bear");
            _vimBuffer.Process("cw");
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
            _vimBuffer.Process("caw");
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
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = String.Empty;
            _vimBuffer.Process("cl");
            Assert.AreEqual("ha", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Ensure that we can change the character at the end of a line when 've=onemore'
        /// </summary>
        [Test]
        public void Change_CharAtEndOfLine_VirtualEditOneMore()
        {
            Create("hat", "cat");
            _textView.MoveCaretTo(2);
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _vimBuffer.Process("cl");
            Assert.AreEqual("ha", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Changing till the end of the line should leave the caret in it's current position
        /// </summary>
        [Test]
        public void Change_TillEndOfLine_NoVirtualEdit()
        {
            Create("hello", "world");
            _textView.MoveCaretTo(2);
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "";
            _vimBuffer.Process("C");
            Assert.AreEqual("he", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Changing till the end of the line should leave the caret in it's current position.  The virtual
        /// edit setting shouldn't affect this
        /// </summary>
        [Test]
        public void Change_TillEndOfLine_VirtualEditOneMore()
        {
            Create("hello", "world");
            _textView.MoveCaretTo(2);
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _vimBuffer.Process("C");
            Assert.AreEqual("he", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Verify that doing a change till the end of the line won't move the cursor
        /// </summary>
        [Test]
        public void Change_Motion_EndOfLine_NoVirtualEdit()
        {
            Create("hello", "world");
            _textView.MoveCaretTo(2);
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "";
            _vimBuffer.Process("c$");
            Assert.AreEqual("he", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Verify that doing a change till the end of the line won't move the cursor
        /// </summary>
        [Test]
        public void Change_Motion_EndOfLine_VirtualEditOneMore()
        {
            Create("hello", "world");
            _textView.MoveCaretTo(2);
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _vimBuffer.Process("c$");
            Assert.AreEqual("he", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Make sure the d#d syntax doesn't apply to other commands like change.  The 'd' suffix in 'd#d' is 
        /// *not* a valid motion
        /// </summary>
        [Test]
        public void Change_Illegal()
        {
            Create("cat", "dog", "tree");
            _vimBuffer.Process("c2d");
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
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(3);
            _vimBuffer.Process('x');
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
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(3);
            _vimBuffer.Process('x');
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
            _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _vimBuffer.Process('x');
            Assert.AreEqual("tst", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When virtual edit is not enabled then the delete till end of line should cause the 
        /// caret to move back to the last non-editted character
        /// </summary>
        [Test]
        public void DeleteTillEndOfLine_NoVirtualEdit()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _vimBuffer.Process('D');
            Assert.AreEqual("c", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint());
        }

        /// <summary>
        /// When virtual edit is enabled then the delete till end of line should not move 
        /// the caret at all
        /// </summary>
        [Test]
        public void DeleteTillEndOfLine_WithVirtualEdit()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(1);
            _vimBuffer.Process('D');
            Assert.AreEqual("c", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatCommand_DeleteWord1()
        {
            Create("the cat jumped over the dog");
            _vimBuffer.Process("dw");
            _vimBuffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Make sure that movement doesn't reset the last edit command")]
        public void RepeatCommand_DeleteWord2()
        {
            Create("the cat jumped over the dog");
            _vimBuffer.Process("dw");
            _vimBuffer.Process(VimKey.Right);
            _vimBuffer.Process(VimKey.Left);
            _vimBuffer.Process(".");
            Assert.AreEqual("jumped over the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Delete word with a count")]
        public void RepeatCommand_DeleteWord3()
        {
            Create("the cat jumped over the dog");
            _vimBuffer.Process("2dw");
            _vimBuffer.Process(".");
            Assert.AreEqual("the dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("dd");
            _vimBuffer.Process(".");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteLine2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("2dd");
            _vimBuffer.Process(".");
            Assert.AreEqual("fox", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft1()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _vimBuffer.Process("<<");
            _vimBuffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftLeft2()
        {
            Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _vimBuffer.Process("2<<");
            _vimBuffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _vimBuffer.Process(">>");
            _vimBuffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_ShiftRight2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.LocalSettings.GlobalSettings.ShiftWidth = 1;
            _vimBuffer.Process("2>>");
            _vimBuffer.Process(".");
            Assert.AreEqual("  bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar1()
        {
            Create("longer");
            _vimBuffer.Process("x");
            _vimBuffer.Process(".");
            Assert.AreEqual("nger", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_DeleteChar2()
        {
            Create("longer");
            _vimBuffer.Process("2x");
            _vimBuffer.Process(".");
            Assert.AreEqual("er", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("After a search operation")]
        public void RepeatCommand_DeleteChar3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("/e", enter: true);
            _vimBuffer.Process("x");
            _vimBuffer.Process("n");
            _vimBuffer.Process(".");
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("zbra", _textView.GetLine(3).GetText());
        }

        [Test]
        public void RepeatCommand_Put1()
        {
            Create("cat");
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _vimBuffer.Process("p");
            _vimBuffer.Process(".");
            Assert.AreEqual("cloloat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Put2()
        {
            Create("cat");
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
            _vimBuffer.Process("2p");
            _vimBuffer.Process(".");
            Assert.AreEqual("clolololoat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_JoinLines1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("J");
            _vimBuffer.Process(".");
            Assert.AreEqual("bear dog cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_Change1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("cl");
            _vimBuffer.Process(VimKey.Delete);
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _vimBuffer.Process(VimKey.Down);
            _vimBuffer.Process(".");
            Assert.AreEqual("ar", _textView.GetLine(0).GetText());
            Assert.AreEqual("g", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Change2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("cl");
            _vimBuffer.Process("u");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _vimBuffer.Process(VimKey.Down);
            _vimBuffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("s");
            _vimBuffer.Process("u");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _vimBuffer.Process(VimKey.Down);
            _vimBuffer.Process(".");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("uog", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_Substitute2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("s");
            _vimBuffer.Process("u");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _vimBuffer.Process(VimKey.Down);
            _vimBuffer.Process("2.");
            Assert.AreEqual("uear", _textView.GetLine(0).GetText());
            Assert.AreEqual("ug", _textView.GetLine(1).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("i");
            _vimBuffer.Process("abc");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _vimBuffer.Process(".");
            Assert.AreEqual("ababccbear", _textView.GetLine(0).GetText());
        }

        [Test]
        public void RepeatCommand_TextInsert2()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("i");
            _vimBuffer.Process("abc");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _vimBuffer.Process(".");
            Assert.AreEqual("abcabcbear", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void RepeatCommand_TextInsert3()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("i");
            _vimBuffer.Process("abc");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(0);
            _vimBuffer.Process(".");
            _vimBuffer.Process(".");
            Assert.AreEqual("ababccabcbear", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Test the repeating of a command that changes white space to tabs
        /// </summary>
        [Test]
        public void RepeatCommand_TextInsert_WhiteSpaceToTab()
        {
            Create("    hello world", "dog");
            _vimBuffer.LocalSettings.TabStop = 4;
            _vimBuffer.LocalSettings.ExpandTab = false;
            _vimBuffer.Process('i');
            _textBuffer.Replace(new Span(0, 4), "\t\t");
            _vimBuffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process('.');
            Assert.AreEqual("\tdog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI1()
        {
            Create("bear", "dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("I");
            _vimBuffer.Process("abc");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _vimBuffer.Process(".");
            Assert.AreEqual("abcdog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        [Description("The first repeat of I should go to the first non-blank")]
        public void RepeatCommand_CapitalI2()
        {
            Create("bear", "  dog", "cat", "zebra", "fox", "jazz");
            _vimBuffer.Process("I");
            _vimBuffer.Process("abc");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
            _vimBuffer.Process(".");
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
            _vimBuffer.Process("3ru");
            Assert.AreEqual("uuu dog kicked the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(".");
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
            _vimBuffer.VimData.LastCommand = FSharpOption.Create(StoredCommand.NewVisualCommand(
                VisualCommand.NewReplaceSelection(KeyInputUtil.VimKeyToKeyInput(VimKey.LowerB)),
                VimUtil.CreateCommandData(),
                StoredVisualSpan.OfVisualSpan(VimUtil.CreateVisualSpanCharacter(_textView.GetLineSpan(0, 3))),
                CommandFlags.None));
            _textView.MoveCaretTo(1);
            _vimBuffer.Process(".");
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
            _vimBuffer.Process('a');
            _vimBuffer.Process(';');
            _vimBuffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process('.');
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
            _vimBuffer.Process('A');
            _vimBuffer.Process(';');
            _vimBuffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process('.');
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
            _vimBuffer.Process("O  fish");
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual("  fish", _textView.GetLine(2).GetText());
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process(".");
            Assert.AreEqual("  fish", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// The insert line below command should be linked the the following text change
        /// </summary>
        [Test]
        public void RepeatCommand_InsertLineBelow()
        {
            Create("cat", "dog", "tree");
            _vimBuffer.Process("o  fish");
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual("  fish", _textView.GetLine(1).GetText());
            _textView.MoveCaretToLine(2);
            _vimBuffer.Process(".");
            Assert.AreEqual("  fish", _textView.GetLine(3).GetText());
        }

        [Test]
        public void Repeat_DeleteWithIncrementalSearch()
        {
            Create("dog cat bear tree");
            _vimBuffer.Process("d/a", enter: true);
            _vimBuffer.Process('.');
            Assert.AreEqual("ar tree", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ToCharDoesNotUseMap()
        {
            Create("bear; again: dog");
            _vimBuffer.Process(":map ; :", enter: true);
            _vimBuffer.Process("dt;");
            Assert.AreEqual("; again: dog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_AlphaToRightMotion()
        {
            Create("dog");
            _vimBuffer.Process(":map a l", enter: true);
            _vimBuffer.Process("aa");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_OperatorPendingWithAmbiguousCommandPrefix()
        {
            Create("dog chases the ball");
            _vimBuffer.Process(":map a w", enter: true);
            _vimBuffer.Process("da");
            Assert.AreEqual("chases the ball", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_ReplaceDoesntUseNormalMap()
        {
            Create("dog");
            _vimBuffer.Process(":map f g", enter: true);
            _vimBuffer.Process("rf");
            Assert.AreEqual("fog", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Map_IncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _vimBuffer.Process(":cmap a o", enter: true);
            _vimBuffer.Process("/a", enter: true);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Map_ReverseIncrementalSearchUsesCommandMap()
        {
            Create("dog");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _vimBuffer.Process(":cmap a o", enter: true);
            _vimBuffer.Process("?a", enter: true);
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
            _vimBuffer.Process(":map j 3j", enter: true);
            _vimBuffer.Process(":ounmap j", enter: true);
            _vimBuffer.Process(":map k 3k", enter: true);
            _vimBuffer.Process(":ounmap k", enter: true);
            _vimBuffer.Process("k");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Move_EndOfWord_SeveralLines()
        {
            Create("the dog kicked the", "ball. The end. Bear");
            for (var i = 0; i < 10; i++)
            {
                _vimBuffer.Process("e");
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
            _vimBuffer.Process("h");
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
            _vimBuffer.Process("l");
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
            _vimBuffer.Process("l");
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
            _vimBuffer.Process("l");
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
            _vimBuffer.Process("k");
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
            _vimBuffer.Process("j");
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
            _vimBuffer.Process("*");
            Assert.AreEqual(PatternUtil.CreateWholeWord("cat"), _vimData.SearchHistory.Items.Head);
        }

        /// <summary>
        /// The'*' motion should work for non-words as well as words.  When dealing with non-words
        /// the whole word portion is not considered
        /// </summary>
        [Test]
        public void Move_NextWordUnderCursor_NonWord()
        {
            Create("{", "cat", "{", "dog");
            _vimBuffer.Process('*');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// The '*' motion should process multiple characters and properly match them
        /// </summary>
        [Test]
        public void Move_NextWordUnderCursor_BigNonWord()
        {
            Create("{{", "cat{", "{{{{", "dog");
            _vimBuffer.Process('*');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
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
            _vimBuffer.Process('j');
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _vimBuffer.Process('j');
            Assert.AreEqual(3, _textView.GetCaretLine().LineNumber);
        }

        /// <summary>
        /// The 'g*' movement should update the search history for the buffer
        /// </summary>
        [Test]
        public void Move_NextPartialWordUnderCursor()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.Process("g*");
            Assert.AreEqual("cat", _vimData.SearchHistory.Items.Head);
        }

        /// <summary>
        /// Make sure the cursor positions correctly on the next line 
        /// </summary>
        [Test]
        public void Handle_BraceClose_MiddleOfParagraph()
        {
            Create("dog", "", "cat");
            _vimBuffer.Process("}");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }


        [Test]
        public void Handle_cb_DeleteWhitespaceAtEndOfSpan()
        {
            Create("public static void Main");
            _textView.MoveCaretTo(19);
            _vimBuffer.Process("cb");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual("public static Main", _textView.GetLine(0).GetText());
            Assert.AreEqual(14, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_cl_WithCountShouldDeleteWhitespace()
        {
            Create("dog   cat");
            _vimBuffer.Process("5cl");
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            Assert.AreEqual(" cat", _textView.GetLine(0).GetText());
        }

        [Test]
        public void Handle_d_WithMarkLineMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _vimTextBuffer.SetLocalMark(LocalMark.OfChar('a').Value, 1, 0);
            _vimBuffer.Process("d'a");
            Assert.AreEqual("bear", _textView.GetLine(0).GetText());
            Assert.AreEqual("tree", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_d_WithMarkMotion()
        {
            Create("dog", "cat", "bear", "tree");
            _vimTextBuffer.SetLocalMark(LocalMark.OfChar('a').Value, 1, 1);
            _vimBuffer.Process("d`a");
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
            _vimBuffer.Process("d}");
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
        }

        [Test]
        public void Handle_f_WithTabTarget()
        {
            Create("dog\tcat");
            _vimBuffer.Process("f\t");
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void Handle_Minus_MiddleOfBuffer()
        {
            Create("dog", "  cat", "bear");
            _textView.MoveCaretToLine(2);
            _vimBuffer.Process("-");
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Escape should exit one time normal mode and return back to the previous mode
        /// </summary>
        [Test]
        public void OneTimeNormalMode_EscapeShouldExit()
        {
            Create("");
            _vimBuffer.Process("i");
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// When pasting from the clipboard where the text doesn't end in a new line it
        /// should be treated as characterwise paste
        /// </summary>
        [Test]
        public void PutAfter_ClipboardWithoutNewLine()
        {
            Create("hello world", "again");
            _textView.MoveCaretTo(5);
            _clipboardDevice.Text = "big ";
            _vimBuffer.Process("\"+p");
            Assert.AreEqual("hello big world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// When pasting from the clipboard where the text does end in a new line it 
        /// should be treated as a linewise paste
        /// </summary>
        [Test]
        public void PutAfter_ClipboardWithNewLine()
        {
            Create("hello world", "again");
            _textView.MoveCaretTo(5);
            _clipboardDevice.Text = "big " + Environment.NewLine;
            _vimBuffer.Process("\"+p");
            Assert.AreEqual("hello world", _textView.GetLine(0).GetText());
            Assert.AreEqual("big ", _textView.GetLine(1).GetText());
            Assert.AreEqual("again", _textView.GetLine(2).GetText());
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
            _vimBuffer.Process('p');
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
            _vimBuffer.Process('p');
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
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig\n", OperationKind.LineWise);
            _vimBuffer.Process("p");
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
            _vimBuffer.LocalSettings.AutoIndent = false;
            _vimBuffer.Process("p");
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
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _vimBuffer.Process("p");
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
            _vimBuffer.Process("p");
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
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
            _vimBuffer.Process("gp");
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
            _vimBuffer.Process("p");
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
            _vimBuffer.Process("p");
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
            _vimBuffer.Process("gp");
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
            _vimBuffer.Process("]p");
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
            _vimBuffer.Process("]p");
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
            _vimBuffer.Process("]p");
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
            _vimBuffer.Process("]p");
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
            _vimBuffer.Process("P");
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
            _vimBuffer.Process("P");
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
            _vimBuffer.Process("P");
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
            _vimBuffer.Process("gP");
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
            _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateBlockValues("a", "b", "c");
            _vimBuffer.Process("P");
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
            _vimBuffer.Process("[p");
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
            _vimBuffer.Process("[p");
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
            _vimBuffer.Process("[p");
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
            _vimBuffer.Process("[p");
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
            _vimBuffer.Process('s');
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            Assert.AreEqual("do", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Insert, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// This command should only yank from the current line to the end of the file
        /// </summary>
        [Test]
        public void Handle_yG_NonFirstLine()
        {
            Create("dog", "cat", "bear");
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process("yG");
            Assert.AreEqual("cat" + Environment.NewLine + "bear", _vimBuffer.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void IncrementalSearch_VeryNoMagic()
        {
            Create("dog", "cat");
            _vimBuffer.Process(@"/\Vog", enter: true);
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
            _vimBuffer.Process("/world", enter: true);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure we respect the \c marker over the 'ignorecase' option even if it appears
        /// at the end of the string
        /// </summary>
        [Test]
        public void IncrementalSearch_CaseInsensitiveAtEndOfSearhString()
        {
            Create("cat dog bear");
            _vimBuffer.Process("/DOG");
            Assert.IsTrue(_vimBuffer.IncrementalSearch.CurrentSearchResult.Value.IsNotFound);
            _vimBuffer.Process(@"\c", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we respect the \c marker over the 'ignorecase' option even if it appears
        /// in the middle of the string
        /// </summary>
        [Test]
        public void IncrementalSearch_CaseInsensitiveInMiddleOfSearhString()
        {
            Create("cat dog bear");
            _vimBuffer.Process(@"/D\cOG", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_CaseSensitive()
        {
            Create("dogDOG", "cat");
            _vimBuffer.Process(@"/\COG", enter: true);
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
            _vimBuffer.Process(@"/\CDOG", enter: true);
            Assert.AreEqual(10, _textView.GetCaretPoint());
        }

        [Test]
        public void IncrementalSearch_HandlesEscape()
        {
            Create("dog");
            _vimBuffer.Process("/do");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_HandlesEscapeInOperator()
        {
            Create("dog");
            _vimBuffer.Process("d/do");
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_UsedAsOperatorMotion()
        {
            Create("dog cat tree");
            _vimBuffer.Process("d/cat", enter: true);
            Assert.AreEqual("cat tree", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_DontMoveCaretDuringSearch()
        {
            Create("dog cat tree");
            _vimBuffer.Process("/cat");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void IncrementalSearch_MoveCaretAfterEnter()
        {
            Create("dog cat tree");
            _vimBuffer.Process("/cat", enter: true);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Verify a couple of searches for {} work as expected
        /// </summary>
        [Test]
        public void IncrementalSearch_Braces()
        {
            Create("func() {   }");
            Action<string, int> doSearch =
                (pattern, position) =>
                {
                    _textView.MoveCaretTo(0);
                    _vimBuffer.Process(pattern);
                    _vimBuffer.Process(VimKey.Enter);
                    Assert.AreEqual(position, _textView.GetCaretPoint().Position);
                };
            doSearch(@"/{", 7);
            doSearch(@"/}", 11);

            _assertOnErrorMessage = false;
            doSearch(@"/\<{\>", 0);  // Should fail
            doSearch(@"/\<}\>", 0);  // Should fail
        }

        [Test]
        public void Mark_SelectionEndIsExclusive()
        {
            Create("the brown dog");
            var span = new SnapshotSpan(_textView.GetPoint(4), _textView.GetPoint(9));
            Assert.AreEqual("brown", span.GetText());
            var visualSelection = VisualSelection.NewCharacter(CharacterSpan.CreateForSpan(span), true);
            _vimTextBuffer.LastVisualSelection = FSharpOption.Create(visualSelection);
            _vimBuffer.Process("y`>");
            Assert.AreEqual("the brown", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void Mark_NamedMarkIsExclusive()
        {
            Create("the brown dog");
            var point = _textView.GetPoint(8);
            Assert.AreEqual('n', point.GetChar());
            _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.OfChar('b').Value, 0, 8);
            _vimBuffer.Process("y`b");
            Assert.AreEqual("the brow", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

        /// <summary>
        /// The last jump mark is a user settable item
        /// </summary>
        [Test]
        public void Mark_LastJump_Set()
        {
            Create("cat", "fish", "dog");
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process("m'");
            Assert.AreEqual(_textBuffer.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
        }

        /// <summary>
        /// Make sure that a jump operation to a differet mark will properly update the LastMark
        /// selection
        /// </summary>
        [Test]
        public void Mark_LastJump_AfterMarkJump()
        {
            Create("cat", "fish", "dog");
            _vimBuffer.Process("mc");   // Mark the line
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process("'c");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
        }

        /// <summary>
        /// Jumping with the '' command should set the last jump to the current location.  So doing
        /// a '' in sequence should just jump back and forth
        /// </summary>
        [Test]
        public void Mark_LastJump_BackAndForth()
        {
            Create("cat", "fish", "dog");
            _vimBuffer.Process("mc");   // Mark the line
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process("'c");
            for (var i = 0; i < 10; i++)
            {
                _vimBuffer.Process("''");
                var line = (i % 2 != 0) ? 0 : 1;
                Assert.AreEqual(_textBuffer.GetLine(line).Start, _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Navigating the jump list shouldn't affect the LastJump mark
        /// </summary>
        [Test]
        public void Mark_LastJump_NavigateJumpList()
        {
            Create("cat", "fish", "dog");
            _vimBuffer.Process("majmbjmc'a'b'c");
            Assert.AreEqual(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
            Assert.AreEqual(_textView.GetLine(0).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void MatchingToken_Parens()
        {
            Create("cat( )");
            _vimBuffer.Process('%');
            Assert.AreEqual(5, _textView.GetCaretPoint());
            _vimBuffer.Process('%');
            Assert.AreEqual(3, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure the caret is properly positioned against a join across 3 lines
        /// </summary>
        [Test]
        public void Join_CaretPositionThreeLines()
        {
            Create("cat", "dog", "bear");
            _vimBuffer.Process("3J");
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
            _vimBuffer.Process("2o");
            _vimBuffer.Process("cat");
            _vimBuffer.Process(VimKey.Escape);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("bear", _textView.GetLine(3).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure that we use the proper line ending when inserting a new line vs. simply choosing 
        /// to use Environment.NewLine
        /// </summary>
        [Test]
        public void InsertLineBelowCaret_AlternateNewLine()
        {
            Create("");
            _textBuffer.Replace(new Span(0, 0), "cat\ndog");
            _textView.MoveCaretTo(0);
            _vimBuffer.Process("o");
            Assert.AreEqual("cat\n", _textBuffer.GetLine(0).ExtentIncludingLineBreak.GetText());
            Assert.AreEqual("\n", _textBuffer.GetLine(1).ExtentIncludingLineBreak.GetText());
            Assert.AreEqual("dog", _textBuffer.GetLine(2).ExtentIncludingLineBreak.GetText());
        }

        /// <summary>
        /// Make sure the text is repeated
        /// </summary>
        [Test]
        public void InsertAtEndOfLine_WithCount()
        {
            Create("dog", "bear");
            _vimBuffer.Process("3A");
            _vimBuffer.Process('b');
            _vimBuffer.Process(VimKey.Escape);
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
            _vimBuffer.Process("%");
            Assert.AreEqual(9, _textView.GetCaretPoint().Position);

            // From the first closing paren back to the start
            _vimBuffer.Process("%");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);

            // From the second opening paren to the last one
            var lastPoint = _textView.TextSnapshot.GetEndPoint().Subtract(1);
            Assert.AreEqual(')', lastPoint.GetChar());
            _textView.MoveCaretTo(18);
            Assert.AreEqual('(', _textView.GetCaretPoint().GetChar());
            _vimBuffer.Process("%");
            Assert.AreEqual(lastPoint, _textView.GetCaretPoint());

            // And back to the start one
            _vimBuffer.Process("%");
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
                    _vimBuffer.Process("%");
                    Assert.AreEqual(end, _textView.GetCaretPoint().Position);

                    if (start != end)
                    {
                        _textView.MoveCaretTo(end);
                        _vimBuffer.Process("%");
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
            _vimBuffer.Process("fr");
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process(";");
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
            _vimBuffer.VimData.LastCharSearch = FSharpOption.Create(Tuple.Create(CharSearchKind.ToChar, Path.Forward, 'o'));
            _textView.MoveCaretTo(_textView.GetEndPoint().Subtract(1));
            _vimBuffer.Process(',');
            Assert.AreEqual(Path.Forward, _vimBuffer.VimData.LastCharSearch.Value.Item2);
            Assert.AreEqual(13, _textView.GetCaretPoint().Position);
            _vimBuffer.Process(',');
            Assert.AreEqual(Path.Forward, _vimBuffer.VimData.LastCharSearch.Value.Item2);
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
            _vimBuffer.Process("/dog");
            _vimBuffer.Process(VimKey.Enter);
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure we can remap keys to nop and have them do nothing
        /// </summary>
        [Test]
        public void Remap_Nop()
        {
            Create("cat");
            _keyMap.MapWithNoRemap("$", "<nop>", KeyRemapMode.Normal);
            _vimBuffer.Process('$');
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Ensure the commands map properly
        /// </summary>
        [Test]
        public void Remap_Issue474()
        {
            Create("cat", "dog", "bear", "pig", "tree", "fish");
            _vimBuffer.Process(":nnoremap gj J");
            _vimBuffer.Process(VimKey.Enter);
            _vimBuffer.Process(":map J 4j");
            _vimBuffer.Process(VimKey.Enter);
            _vimBuffer.Process("J");
            Assert.AreEqual(4, _textView.GetCaretLine().LineNumber);
            _textView.MoveCaretTo(0);
            _vimBuffer.Process("gj");
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
            _vimBuffer.LocalSettings.GlobalSettings.WrapScan = false;
            _vimBuffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog", Path.Backward);
            _vimBuffer.Process('/');
            _vimBuffer.Process(VimKey.Enter);
            Assert.AreEqual(8, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Substitute command should set the LastSearch value
        /// </summary>
        [Test]
        public void LastSearch_SetBySubstitute()
        {
            Create("dog cat dog");
            _vimBuffer.Process(":s/dog/cat", enter: true);
            Assert.AreEqual("dog", _vimBuffer.VimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Substitute command should re-use the LastSearch value if there is no specific 
        /// search value set
        /// </summary>
        [Test]
        public void LastSearch_UsedBySubstitute()
        {
            Create("dog cat dog");
            _vimBuffer.VimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _vimBuffer.Process(":s//cat", enter: true);
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
            _vimBuffer.Process(":s/CAT/fish/i", enter: true);
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            var didHit = false;
            _vimBuffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("CAT"), message);
                    didHit = true;
                };
            _vimBuffer.Process("n");
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// Delete with an append register should concatenate the values
        /// </summary>
        [Test]
        public void Delete_Append()
        {
            Create("dog", "cat", "fish");
            _vimBuffer.Process("\"cyaw");
            _vimBuffer.Process("j");
            _vimBuffer.Process("\"Cdw");
            Assert.AreEqual("dogcat", _vimBuffer.RegisterMap.GetRegister('c').StringValue);
            Assert.AreEqual("dogcat", _vimBuffer.RegisterMap.GetRegister('C').StringValue);
            _vimBuffer.Process("\"cp");
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
            _vimBuffer.Process("d0");
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
            _vimBuffer.Process("dh");
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
            _vimBuffer.Process("dj");
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
            _vimBuffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _vimBuffer.Process("d/dog", enter: true);
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
            _vimBuffer.Process("dw");
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
            _vimBuffer.Process("dw");
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
            _vimBuffer.Process("d/cha", enter: true);
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
            _vimBuffer.Process("d/cha", enter: true);
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
            _vimBuffer.Process("daw");
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
            _vimBuffer.Process("daw");
            Assert.AreEqual("the", _textView.GetLine(0).GetText());
            Assert.AreEqual("  chased the bird", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// When virtual edit is enabled then deletion should not cause the caret to 
        /// move if it would otherwise be in virtual space
        /// </summary>
        [Test]
        public void Delete_WithVirtualEdit()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(2);
            _vimBuffer.Process("dl");
            Assert.AreEqual("ca", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When virtual edit is not enabled then deletion should cause the caret to 
        /// move if it would end up in virtual space
        /// </summary>
        [Test]
        public void Delete_NoVirtualEdit()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(2);
            _vimBuffer.Process("dl");
            Assert.AreEqual("ca", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure deleting the last line changes the line count in the buffer
        /// </summary>
        [Test]
        public void DeleteLines_OnLastLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _vimBuffer.Process("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        /// <summary>
        /// Delete lines with the special d#d count syntax
        /// </summary>
        [Test]
        public void DeleteLines_Special_Simple()
        {
            Create("cat", "dog", "bear", "fish");
            _vimBuffer.Process("d2d");
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
            _vimBuffer.Process("2d2d");
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
            _vimBuffer.Process("yaw");
            _textView.MoveCaretToLine(1);
            _vimBuffer.Process("\"cyaw");
            _textView.MoveCaretToLine(2);
            _vimBuffer.Process("dw");
            _vimBuffer.Process("\"0p");
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
            _vimBuffer.Process("y]]");
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
            _vimBuffer.Process("\"cyaw");
            _vimBuffer.Process("j");
            _vimBuffer.Process("\"Cyaw");
            Assert.AreEqual("dogcat", _vimBuffer.RegisterMap.GetRegister('c').StringValue);
            Assert.AreEqual("dogcat", _vimBuffer.RegisterMap.GetRegister('C').StringValue);
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
            _vimBuffer.Process("yh");
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
            _vimBuffer.Process("yj");
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
            _vimBuffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_SearchHitBottomWithout(@"\<dog\>"), message);
                    didSee = true;
                };
            _vimBuffer.Process("y*");
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
            _vimBuffer.Process("yw");
            Assert.AreEqual(" ", UnnamedRegister.StringValue);
            _textView.MoveCaretTo(7);
            _vimBuffer.Process("yw");
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
            _vimBuffer.Process("yw");
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
            _vimBuffer.Process("yw");
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
            _vimBuffer.Process("y2w");
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
            _vimBuffer.Process("y2w");
            Assert.AreEqual(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
            Assert.AreEqual("og" + Environment.NewLine, UnnamedRegister.StringValue);
            _vimBuffer.Process("p");
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
            _vimBuffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _assertOnWarningMessage = false;
            _globalSettings.WrapScan = true;
            _textView.MoveCaretToLine(2);

            _vimBuffer.Process("y/dog", enter: true);
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
            _vimBuffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("bug"), message);
                    didSee = true;
                };
            _vimBuffer.Process("y/bug", enter: true);
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Doing an 'iw' yank from the start of the word should yank just the word
        /// </summary>
        [Test]
        public void Yank_InnerWord_FromWordStart()
        {
            Create("the dog chased the ball");
            _vimBuffer.Process("yiw");
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
            _vimBuffer.Process("y2iw");
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
            _vimBuffer.Process("y2iw");
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
            _vimBuffer.Process("y2iw");
            Assert.AreEqual("cat" + Environment.NewLine + "dog", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Yank lines using the special y#y syntax
        /// </summary>
        [Test]
        public void YankLines_Special_Simple()
        {
            Create("cat", "dog", "bear");
            _vimBuffer.Process("y2y");
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
            _vimBuffer.Process("y*");
            Assert.AreEqual(_textView.GetPoint(0), _jumpList.Jumps.First().Position);
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
            _vimBuffer.WarningMessage +=
                (_, msg) =>
                {
                    Assert.AreEqual(Resources.Common_SearchForwardWrapped, msg);
                    didHit = true;
                };
            _assertOnWarningMessage = false;
            _vimBuffer.Process("*");
            _textView.MoveCaretToLine(2);
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(_textView.GetPoint(0), _textView.GetCaretPoint());
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
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
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
            Assert.AreEqual(_textView.GetLine(0).Start, _textView.GetCaretPoint());
            Assert.AreEqual(1, _jumpList.CurrentIndex.Value);
            Assert.AreEqual(2, _jumpList.Jumps.Length);
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Subtract a negative decimal number
        /// </summary>
        [Test]
        public void SubtractFromWord_Decimal_Negative()
        {
            Create(" -10");
            _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('x'));
            Assert.AreEqual(" -11", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we handle the 'gv' command to switch to the previous visual mode
        /// </summary>
        [Test]
        public void SwitchPreviousVisualMode_Line()
        {
            Create("cats", "dogs", "fish");
            var visualSelection = VisualSelection.NewLine(
                _textView.GetLineRange(0, 1),
                true,
                1);
            _vimTextBuffer.LastVisualSelection = FSharpOption.Create(visualSelection);
            _vimBuffer.Process("gv");
            Assert.AreEqual(ModeKind.VisualLine, _vimBuffer.ModeKind);
            Assert.AreEqual(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Line));
        }

        /// <summary>
        /// Make sure the caret is positioned properly during undo
        /// </summary>
        [Test]
        public void Undo_DeleteAllWord()
        {
            Create("cat", "dog");
            _textView.MoveCaretTo(1);
            _vimBuffer.Process("daw");
            _vimBuffer.Process("u");
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
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("cc");
            _vimBuffer.Process(VimKey.Escape);
            _vimBuffer.Process("u");
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
            _vimBuffer.LocalSettings.AutoIndent = true;
            _vimBuffer.Process("3cc");
            _vimBuffer.Process(VimKey.Escape);
            _vimBuffer.Process("u");
            Assert.AreEqual("dog", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(_textView.GetPointInLine(2, 2), _textView.GetCaretPoint());
        }
    }
}
