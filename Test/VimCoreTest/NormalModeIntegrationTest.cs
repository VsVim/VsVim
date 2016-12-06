using System;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Vim.Extensions;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Class for testing the full integration story of normal mode in VsVim
    /// </summary>
    public abstract class NormalModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected IVimBufferData _vimBufferData;
        protected IVimTextBuffer _vimTextBuffer;
        protected IWpfTextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimLocalSettings _localSettings;
        protected IVimWindowSettings _windowSettings;
        protected IJumpList _jumpList;
        protected IKeyMap _keyMap;
        protected IVimData _vimData;
        protected IFoldManager _foldManager;
        protected INormalMode _normalMode;
        protected MockVimHost _vimHost;
        protected TestableClipboardDevice _clipboardDevice;
        protected bool _assertOnErrorMessage = true;
        protected bool _assertOnWarningMessage = true;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.ErrorMessage +=
                (_, message) =>
                {
                    if (_assertOnErrorMessage)
                    {
                        throw new Exception("Error Message: " + message.Message);
                    }
                };
            _vimBuffer.WarningMessage +=
                (_, message) =>
                {
                    if (_assertOnWarningMessage)
                    {
                        throw new Exception("Warning Message: " + message.Message);
                    }
                };
            _vimBufferData = _vimBuffer.VimBufferData;
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _normalMode = _vimBuffer.NormalMode;
            _keyMap = _vimBuffer.Vim.KeyMap;
            _localSettings = _vimBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _windowSettings = _vimBuffer.WindowSettings;
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
            Assert.True(_textView.VisualSnapshot is IElisionSnapshot);
            Assert.True(_textView.VisualSnapshot != _textView.TextSnapshot);
        }

        public sealed class MoveTest : NormalModeIntegrationTest
        {
            [Fact]
            public void HomeStartOfLine()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("<Home>");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Blank lines are sentences
            /// </summary>
            [Fact]
            public void SentenceForBlankLine()
            {
                Create("dog.  ", "", "cat");
                _vimBuffer.Process(")");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// A warning message should be raised when a search forward for a value
            /// causes a wrap to occur
            /// </summary>
            [Fact]
            public void SearchWraps()
            {
                Create("dog", "cat", "tree");
                var didHit = false;
                _textView.MoveCaretToLine(1);
                _assertOnWarningMessage = false;
                _vimBuffer.LocalSettings.GlobalSettings.WrapScan = true;
                _vimBuffer.WarningMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchForwardWrapped, args.Message);
                        didHit = true;
                    };
                _vimBuffer.Process("/dog", enter: true);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.True(didHit);
            }

            /// <summary>
            /// Make sure the paragraph move goes to the appropriate location
            /// </summary>
            [Fact]
            public void ParagraphForward()
            {
                Create("dog", "", "cat", "", "bear");
                _vimBuffer.Process("}");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the paragraph move goes to the appropriate location
            /// </summary>
            [Fact]
            public void ParagraphForward_DontMovePastBlankLine()
            {
                Create("dog", " ", "cat", "", "bear");
                _vimBuffer.Process("}");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void FirstNonBlankOnLine()
            {
                Create("  dog");
                _vimBuffer.Process("_");
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
            }

            /// <summary>
            /// Make sure the paragraph move backward goes to the appropriate location
            /// </summary>
            [Fact]
            public void ParagraphBackward()
            {
                Create("dog", "", "cat", "pig", "");
                _textView.MoveCaretToLine(3);
                _vimBuffer.Process("{");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the paragraph move backward goes to the appropriate location
            /// </summary>
            [Fact]
            public void ParagraphBackward_DontMovePastBlankLine()
            {
                Create("dog", " ", "cat", "pig", "");
                _textView.MoveCaretToLine(3);
                _vimBuffer.Process("{");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the paragraph move backward goes to the appropriate location when 
            /// started on the first line of the paragraph containing actual text
            /// </summary>
            [Fact]
            public void ParagraphBackwardFromTextStart()
            {
                Create("dog", "", "cat", "pig", "");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("{");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that when starting on a section start line we jump over it when 
            /// using the section forward motion
            /// </summary>
            [Fact]
            public void SectionForwardFromCloseBrace()
            {
                Create("dog", "}", "bed", "cat");
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that we move off of the brace line when we are past the opening
            /// brace on the line
            /// </summary>
            [Fact]
            public void SectionFromAfterCloseBrace()
            {
                Create("dog", "} bed", "cat");
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
                _textView.MoveCaretToLine(1, 3);
                _vimBuffer.Process("[]");
                Assert.Equal(_textView.GetLine(0).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure we handle the cases of many braces in a row correctly
            /// </summary>
            [Fact]
            public void SectionBracesInARow()
            {
                Create("dog", "}", "}", "}", "cat");

                // Go forward
                for (var i = 0; i < 4; i++)
                {
                    _vimBuffer.Process("][");
                    Assert.Equal(_textView.GetLine(i + 1).Start, _textView.GetCaretPoint());
                }

                // And now backward
                for (var i = 0; i < 4; i++)
                {
                    _vimBuffer.Process("[]");
                    Assert.Equal(_textView.GetLine(4 - i - 1).Start, _textView.GetCaretPoint());
                }
            }

            /// <summary>
            /// Make sure that when starting on a section start line for a macro we jump 
            /// over it when using the section forward motion
            /// </summary>
            [Fact]
            public void SectionForwardFromMacro()
            {
                Create("dog", ".SH", "bed", "cat");
                _globalSettings.Sections = "SH";
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure we can move forward searching for a tab
            /// </summary>
            [Fact]
            public void SearchForTab()
            {
                Create("dog", "hello\tworld");
                _vimBuffer.ProcessNotation(@"/\t<Enter>");
                Assert.Equal(_textView.GetPointInLine(1, 5), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When the 'w' motion ends on a new line it should move to the first non-blank
            /// in the next line
            /// </summary>
            [Fact]
            public void WordToFirstNonBlankAfterNewLine()
            {
                Create("cat", "  dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("w");
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The 'w' motion needs to jump over the blanks at the end of the previous line and
            /// find the blank in the next line
            /// </summary>
            [Fact]
            public void WordToFirstNonBlankAfterNewLineWithSpacesOnPrevious()
            {
                Create("cat    ", "  dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("w");
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The 'w' motion can't jump an empty line
            /// </summary>
            [Fact]
            public void WordOverEmptyLineWithIndent()
            {
                Create("cat", "", "  dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("w");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The 'w' motion can jump over a blank line 
            /// </summary>
            [Fact]
            public void WordOverBlankLine()
            {
                Create("cat", "    ", "  dog");
                _vimBuffer.Process("w");
                Assert.Equal(_textBuffer.GetLine(2).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When the last line in the buffer is empty make sure that we can move down to the 
            /// second to last line. 
            /// </summary>
            [Fact]
            public void DownToLastLineBeforeEmpty()
            {
                Create("a", "b", "");
                _vimBuffer.Process("j");
                Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                Assert.Equal('b', _textView.GetCaretPoint().GetChar());
            }

            /// <summary>
            /// Make sure we can move to the empty last line with the 'j' command
            /// </summary>
            [Fact]
            public void DownToEmptyLastLine()
            {
                Create("a", "b", "");
                _vimBuffer.Process("jj");
                Assert.Equal(2, _textView.GetCaretLine().LineNumber);
            }

            [Fact]
            public void UpFromEmptyLastLine()
            {
                Create("a", "b", "");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("kk");
                Assert.Equal(0, _textView.GetCaretLine().LineNumber);
            }

            [Fact]
            public void EndOfWord_SeveralLines()
            {
                Create("the dog kicked the", "ball. The end. Bear");
                for (var i = 0; i < 10; i++)
                {
                    _vimBuffer.Process("e");
                }
                Assert.Equal(_textView.GetLine(1).End.Subtract(1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Trying a move caret left at the start of the line should cause a beep 
            /// to be produced
            /// </summary>
            [Fact]
            public void CharLeftAtStartOfLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("h");
                Assert.Equal(1, _vimHost.BeepCount);
            }

            /// <summary>
            /// Beep when moving a character right at the end of the line
            /// </summary>
            [Fact]
            public void CharRightAtLastOfLine()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = String.Empty;  // Ensure not 'OneMore'
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("l");
                Assert.Equal(1, _vimHost.BeepCount);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Succeed in moving when the 'onemore' option is set 
            /// </summary>
            [Fact]
            public void CharRightAtLastOfLineWithOneMore()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("l");
                Assert.Equal(0, _vimHost.BeepCount);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Fail at moving one more right when in the end 
            /// </summary>
            [Fact]
            public void CharRightAtEndOfLine()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("l");
                Assert.Equal(1, _vimHost.BeepCount);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This should beep 
            /// </summary>
            [Fact]
            public void UpFromFirstLine()
            {
                Create("cat");
                _vimBuffer.Process("k");
                Assert.Equal(1, _vimHost.BeepCount);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This should beep
            /// </summary>
            [Fact]
            public void DownFromLastLine()
            {
                Create("cat");
                _vimBuffer.Process("j");
                Assert.Equal(1, _vimHost.BeepCount);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The '*' movement should update the search history for the buffer
            /// </summary>
            [Fact]
            public void NextWord()
            {
                Create("cat", "dog", "cat");
                _vimBuffer.Process("*");
                Assert.Equal(PatternUtil.CreateWholeWord("cat"), _vimData.SearchHistory.Items.Head);
            }

            /// <summary>
            /// The'*' motion should work for non-words as well as words.  When dealing with non-words
            /// the whole word portion is not considered
            /// </summary>
            [Fact]
            public void NextWord_NonWord()
            {
                Create("{", "cat", "{", "dog");
                _vimBuffer.Process('*');
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The '*' motion should process multiple characters and properly match them
            /// </summary>
            [Fact]
            public void NextWord_BigNonWord()
            {
                Create("{{", "cat{", "{{{{", "dog");
                _vimBuffer.Process('*');
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// If the caret is positioned an a non-word character but there is a word 
            /// later on the line then the '*' should target that word
            /// </summary>
            [Fact]
            public void NextWord_JumpToWord()
            {
                Create("{ try", "{", "try");
                _vimBuffer.Process("*");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The _ character is a word character and hence a full word match should be
            /// done when doing a * search
            /// </summary>
            [Fact]
            public void NextWord_UnderscoreIsWord()
            {
                Create("last_item", "hello");
                _assertOnWarningMessage = false;
                _vimBuffer.Process("*");
                Assert.Equal(PatternUtil.CreateWholeWord("last_item"), _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// If the caret is positioned an a non-word character but there is a word 
            /// later on the line then the 'g*' should target that word
            /// </summary>
            [Fact]
            public void NextPartialWord_JumpToWord()
            {
                Create("{ try", "{", "trying");
                _vimBuffer.Process("g*");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When moving a line down over a fold it should not be expanded and the entire fold
            /// should count as a single line
            /// </summary>
            [Fact]
            public void LineDown_OverFold()
            {
                Create("cat", "dog", "tree", "fish");
                var range = _textView.GetLineRange(1, 2);
                _foldManager.CreateFold(range);
                _vimBuffer.Process('j');
                Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                _vimBuffer.Process('j');
                Assert.Equal(3, _textView.GetCaretLine().LineNumber);
            }

            /// <summary>
            /// The 'g*' movement should update the search history for the buffer
            /// </summary>
            [Fact]
            public void NextPartialWordUnderCursor()
            {
                Create("cat", "dog", "cat");
                _vimBuffer.Process("g*");
                Assert.Equal("cat", _vimData.SearchHistory.Items.Head);
            }

            /// <summary>
            /// The S-Space command isn't documented that I can find but it appears to be 
            /// an alias for the word forward motion.  Ad-hoc testing shows they have the 
            /// same behavior
            ///
            /// Issue #910
            /// </summary>
            [Fact]
            public void NextWordViaShiftSpace()
            {
                Create("cat dog bear tree");
                _vimBuffer.ProcessNotation("<S-Space>");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.ProcessNotation("<S-Space>");
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void MoveOverFold()
            {
                Create("cat", "dog", "fish", "tree");
                _foldManager.CreateFold(_textBuffer.GetLineRange(1, endLine: 2));
                _vimBuffer.ProcessNotation("j");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
                _vimBuffer.ProcessNotation("j");
                Assert.Equal(_textBuffer.GetPointInLine(3, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void Issue603()
            {
                Create(
                    "const ExampleType*\tpObject = __super::operator[](ii);",
                    "const char*\t\tpcszPath = \"<unknown path>\";");
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.LocalSettings.TabStop = 4;
                _vimBuffer.LocalSettings.ShiftWidth = 8;
                _textView.MoveCaretTo(22);
                Assert.Equal('j', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process('j');
                Assert.Equal('h', _textView.GetCaretPoint().GetChar());
            }

            /// <summary>
            /// Don't consider 'smartcase' when doing a * operation 
            /// </summary>
            [Fact]
            public void Issue1511()
            {
                Create("foo", "FOO", "foo");
                _assertOnWarningMessage = false;
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                _vimBuffer.Process('*');
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.Process('*');
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
                _vimBuffer.Process('*');
                Assert.Equal(_textBuffer.GetLine(0).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Have to make sure that 'j' correctly maintains caret column when stepping 
            /// over collapsed regions.  
            ///
            /// In general this is straight forward because vim regions include the trailing
            /// new line.  A C# region though does not and we end up with the text of 2 
            /// lines (first and new line of last) in the same visual line.  This makes mapping
            /// considerably more difficult.  
            /// </summary>
            [Fact]
            public void Issue1522()
            {
                Create("cat", "dog", "bear", "tree");

                // The span should *not* include the line break of the last line.  
                var span = new SnapshotSpan(
                    _textBuffer.GetLine(1).Start.Add(2),
                    _textBuffer.GetLine(2).End);

                // Collapse the region specified above
                var adhocOutliner = EditorUtilsFactory.GetOrCreateOutliner(_textBuffer);
                adhocOutliner.CreateOutliningRegion(span, SpanTrackingMode.EdgeInclusive, "test", "test");
                OutliningManagerService.GetOutliningManager(_textView).CollapseAll(span, _ => true);

                _vimBuffer.ProcessNotation("j");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.ProcessNotation("j");
                Assert.Equal(_textBuffer.GetLine(3).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class MatchingTokenTest : NormalModeIntegrationTest
        {
            private void AssertLine(int lineNumber)
            {
                Assert.Equal(_textBuffer.GetLine(lineNumber).Start, _textView.GetCaretPoint());
            }

            private void AssertPattern(params int[] lineNumbers)
            {
                foreach (var lineNumber in lineNumbers)
                {
                    _vimBuffer.Process("%");
                    AssertLine(lineNumber);
                }
            }

            /// <summary>
            /// The space after the # character doesn't prevent it from being recognized
            /// as a preprocessor symbol
            /// </summary>
            [Fact]
            public void SpaceAfterPoundBeforeIf()
            {
                Create("# if", "#else", "#endif");
                AssertPattern(1, 2, 0);
            }

            [Fact]
            public void SpaceAfterAll()
            {
                Create("# if", "# else", "# endif");
                AssertPattern(1, 2, 0);
            }

            /// <summary>
            /// The space before the # doesn't matter either
            /// </summary>
            [Fact]
            public void SpaceBeforeAll()
            {
                Create("  #if", "  #else", "  #endif");
                _vimBuffer.Process("%");
                var caretPoint = _textView.GetCaretPoint();
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                _vimBuffer.Process("%");
                Assert.Equal(_textBuffer.GetPointInLine(2, 2), _textView.GetCaretPoint());
                _vimBuffer.Process("%");
                Assert.Equal(_textBuffer.GetPointInLine(0, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that we can jump around in a nested pragma statement
            /// </summary>
            [Fact]
            public void NestedBlock()
            {
                Create("#if 0", "#if 1", "#else // !1", "#endif // !1", "#endif // 0");
                _textView.MoveCaretToLine(1);
                AssertPattern(2, 3, 1);
            }

            /// <summary>
            /// Commented out code doesn't factor into the equation here.  The preprocessor directives
            /// still count
            /// </summary>
            [Fact]
            public void CommentsDontMatter()
            {
                Create("# if", "/*", "#else", "*/", "#endif");
                AssertPattern(2, 4, 0);
            }

            /// <summary>
            /// If there is no matchnig #endif then we get stuck on the last #elif directive
            /// </summary>
            [Fact]
            public void NoEndIf()
            {
                Create("#if", "#elif", "#if");
                for (int i = 0; i < 10; i++)
                {
                    _vimBuffer.Process("%");
                    Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                }
            }

            [Fact]
            public void MismatchedBlockCommentsMultiline()
            {
                Create("/*", "/*", "*/");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("%");
                Assert.Equal(_textBuffer.GetPointInLine(2, 1), _textView.GetCaretPoint());
                _vimBuffer.Process("%");
                Assert.Equal(_textBuffer.GetPointInLine(0, 0), _textView.GetCaretPoint());
                _vimBuffer.Process("%");
                Assert.Equal(_textBuffer.GetPointInLine(2, 1), _textView.GetCaretPoint());
            }
            /// <summary>
            /// Ensure the '%' motion properly moves between the block comments in the 
            /// mismatch case
            /// </summary>
            [Fact]
            public void MismatchedBlockCommentsSameLine()
            {
                Create("/* /* */");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process('%');
                Assert.Equal(7, _textView.GetCaretPoint());
                _vimBuffer.Process('%');
                Assert.Equal(0, _textView.GetCaretPoint());
                _vimBuffer.Process('%');
                Assert.Equal(7, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParensAfterWord()
            {
                Create("cat( )");
                _vimBuffer.Process('%');
                Assert.Equal(5, _textView.GetCaretPoint());
                _vimBuffer.Process('%');
                Assert.Equal(3, _textView.GetCaretPoint());
            }

            /// <summary>
            /// In issue #744 where there are 3 parts to the conditional, the caret is supposed to cycle through
            /// (#if, #else, #end, #if), ... However, it actually only cycles between the last 
            /// two (#if, #else, #end, #else)
            /// </summary>
            [Fact]
            public void PreProcessorIfElse()
            {
                Create("#if DEBUG", "#else", "#endif");

                _vimBuffer.Process("%");
                // checking that % does actually change lines at all
                Assert.Equal(_textView.GetCaretLine().LineNumber, 1);
                _vimBuffer.Process("%%");

                Assert.Equal(_textView.GetCaretLine().LineNumber, 0);
            }

            [Fact]
            public void PreProcessorIfdefElse()
            {
                Create("#ifdef DEBUG", "#else", "#endif");
                // move caret off of #if, otherwise it'll be covered by the previous functionaly and won't actually prove anything
                _textView.MoveCaretTo(4);

                _vimBuffer.Process("%");
                // checking that % does actually change lines at all
                Assert.Equal(_textView.GetCaretLine().LineNumber, 1);
                _vimBuffer.Process("%%");

                Assert.Equal(_textView.GetCaretLine().LineNumber, 0);
            }

            [Fact]
            public void PreProcessorIfndefElse()
            {
                Create("#ifndef DEBUG", "#else", "#endif");
                // move caret off of #if, otherwise it'll be covered by the previous functionaly and won't actually prove anything
                _textView.MoveCaretTo(4);

                _vimBuffer.Process("%");
                Assert.Equal(_textView.GetCaretLine().LineNumber, 1);
                _vimBuffer.Process("%%");

                Assert.Equal(_textView.GetCaretLine().LineNumber, 0);
            }

            [Fact]
            public void ItMatchesEvenWhenCaretIsAtTheEnd()
            {
                Create("#if DEBUG", "#endif");
                // move caret off of #if, otherwise it'll be covered by the previous functionaly and won't actually prove anything
                _textView.MoveCaretTo(6);

                _vimBuffer.Process("%");

                Assert.Equal(_textView.GetCaretLine().LineNumber, 1);
            }

            /// <summary>
            /// Make sure we jump correctly between matching token values of different types
            ///
            /// TODO: This test is also broken due to the matching case not being able to 
            /// come of the '/' in a '*/'
            /// </summary>
            [Fact]
            public void DifferentTypes()
            {
                Create("{ { (( } /* a /*) b */ })");
                Action<int, int> del = (start, end) =>
                    {
                        _textView.MoveCaretTo(start);
                        _vimBuffer.Process("%");
                        Assert.Equal(end, _textView.GetCaretPoint().Position);

                        if (start != end)
                        {
                            _textView.MoveCaretTo(end);
                            _vimBuffer.Process("%");
                            Assert.Equal(start, _textView.GetCaretPoint().Position);
                        }
                    };
                del(0, 23);
                del(2, 7);
                del(4, 24);
                del(5, 16);
                del(9, 21);
            }

            /// <summary>
            /// Make sure the matching token behavior fits all of the issues described in 
            /// issue 468
            /// </summary>
            [Fact]
            public void Issue468()
            {
                Create("(wchar_t*) realloc(pwcsSelFile, (nSelFileLen+1)*sizeof(wchar_t))");

                // First open paren to the next closing one
                _vimBuffer.Process("%");
                Assert.Equal(9, _textView.GetCaretPoint().Position);

                // From the first closing paren back to the start
                _vimBuffer.Process("%");
                Assert.Equal(0, _textView.GetCaretPoint().Position);

                // From the second opening paren to the last one
                var lastPoint = _textView.TextSnapshot.GetEndPoint().Subtract(1);
                Assert.Equal(')', lastPoint.GetChar());
                _textView.MoveCaretTo(18);
                Assert.Equal('(', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process("%");
                Assert.Equal(lastPoint, _textView.GetCaretPoint());

                // And back to the start one
                _vimBuffer.Process("%");
                Assert.Equal(18, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure that we handle the nested case properly 
            /// </summary>
            [Fact]
            public void Issue900()
            {
                Create("#if", "#if", "#elif", "#endif", "#endif");
                _textView.MoveCaretToLine(1);
                AssertPattern(2, 3, 1);
            }

            /// <summary>
            /// Handle white space between the # and the start of the if statement
            /// </summary>
            [Fact]
            public void Issue901()
            {
                Create("#    if", "#      else", "#     endif");
                AssertPattern(1, 2, 0);
            }

            [Fact]
            public void Issue987()
            {
                Create("#if 0", "#if 1", "#else // !1", "#endif // !1", "#endif // 0");
                AssertPattern(4, 0);
            }

            [Fact]
            public void Issue1362()
            {
                Create("/*", "abc", "*/", "/*", "def", "*/");
                _textView.MoveCaretToLine(5);
                _vimBuffer.Process("%");
                Assert.Equal(_textView.GetPointInLine(3, 0), _textView.GetCaretPoint());
            }
        }

        public sealed class UnmatchedTokenTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// The search is forward and doesn't consider the tokens that are prior
            /// to the current
            /// </summary>
            [Fact]
            public void ParenForwardFromStart()
            {
                Create("( )");
                _vimBuffer.Process("])");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ParenFromFromBefore()
            {
                Create(" ()");
                _vimBuffer.Process("])");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ParenForwardAfterMatching()
            {
                Create(" ())");
                _vimBuffer.Process("])");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Further test to ensure that we don't consider the token immediately under
            /// the caret 
            /// </summary>
            [Fact]
            public void ParenForwardFromUnmatching()
            {
                Create(")) dog");
                _vimBuffer.Process("])");
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ParenForwardWithCount()
            {
                Create(" ))))");
                _vimBuffer.Process("2])");
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenForwardWithCountMultiline()
            {
                Create("()", ")");
                _vimBuffer.Process("2])");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenForwardMultiline()
            {
                Create("dog", ")");
                _vimBuffer.Process("])");
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenBackward()
            {
                Create("()");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("[(");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenBackwardNonStart()
            {
                Create("(( dog");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("[(");
                Assert.Equal(1, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenBackwardWithCount()
            {
                Create("(( dog");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("2[(");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenBackwardMultiline()
            {
                Create("(", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("[(");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void ParenBackwardMultiline2()
            {
                Create("((", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("[(");
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void BraceForward()
            {
                Create("{}");
                _vimBuffer.Process("]}");
                Assert.Equal(1, _textView.GetCaretPoint());
            }

            [Fact]
            public void BraceBackward()
            {
                Create("{}");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("[{");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void YankParenForward()
            {
                Create("dog)");
                _vimBuffer.Process("y])");
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }

            [Fact]
            public void YankParenBackward()
            {
                Create("(dog");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("y[(");
                Assert.Equal("(do", UnnamedRegister.StringValue);
            }
        }

        public sealed class ParagraphMotionTest : NormalModeIntegrationTest
        {
            [Fact]
            public void MoveBackwards()
            {
                Create("cat", "dog", "", "fish", "tree");
                _textView.MoveCaretToLine(3);
                _vimBuffer.Process('{');
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void MoveForwards()
            {
                Create("cat", "dog", "", "fish", "tree");
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process('}');
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void DeleteBackwards()
            {
                Create("cat", "dog", "", "fish", "tree");
                _textView.MoveCaretToLine(3);
                _vimBuffer.Process("d{");
                Assert.Equal(
                    new[] { "cat", "dog", "fish", "tree" },
                    _textBuffer.GetLines());
            }

            [Fact]
            public void DeleteBackwardsFromMiddle()
            {
                Create("cat", "dog", "", "fish", "tree");
                _textView.MoveCaretToLine(3, 1);
                _vimBuffer.Process("d{");
                Assert.Equal(
                    new[] { "cat", "dog", "ish", "tree" },
                    _textBuffer.GetLines());
            }

            [Fact]
            public void DeleteBackwardsPastEndOfLine()
            {
                Create("cat", "dog", "", "fish", "tree");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretToLine(3, 4);
                _vimBuffer.Process("d{");
                Assert.Equal(
                    new[] { "cat", "dog", "tree" },
                    _textBuffer.GetLines());
            }

            [Fact]
            public void DeleteForwards()
            {
                Create("cat", "dog", "", "fish", "tree");
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process("d}");
                Assert.Equal(
                    new[] { "", "fish", "tree" },
                    _textBuffer.GetLines());
            }

            [Fact]
            public void Issue978()
            {
                var text = @"
        [ThreadStatic] static IScheduler _UnitTestDeferredScheduler;
        static IScheduler _DeferredScheduler;

        /// <summary>
        /// DeferredScheduler is the scheduler used to schedule work items that
        /// should be run ""on the UI thread"". In normal mode, this will be
        /// DispatcherScheduler, and in Unit Test mode this will be Immediate,
        /// to simplify writing common unit tests.
        /// </summary>
        public static IScheduler DeferredScheduler {
";
                Create(text.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretToLine(9);
                _vimBuffer.Process('$');
                Assert.Equal('>', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process("ld{");
                Assert.Equal("        public static IScheduler DeferredScheduler {", _textView.GetCaretLine().GetText());
            }
        }

        public sealed class YankTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Make sure we properly update register 0 during a yank
            /// </summary>
            [Fact]
            public void Register0()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.Process("yaw");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("\"cyaw");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("dw");
                _vimBuffer.Process("\"0p");
                Assert.Equal("dog", _textView.GetLine(2).GetText());
            }

            /// <summary>
            /// Where there are not section boundaries between the caret and the end of the 
            /// ITextBuffer the entire ITextBuffer should be yanked when section forward 
            /// is used
            /// </summary>
            [Fact]
            public void SectionForwardToEndOfBuffer()
            {
                Create("dog", "cat", "bear");
                _vimBuffer.Process("y]]");
                Assert.Equal("dog" + Environment.NewLine + "cat" + Environment.NewLine + "bear", UnnamedRegister.StringValue);
                Assert.Equal(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// Yanking with an append register should concatenate the values
            /// </summary>
            [Fact]
            public void Append()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.Process("\"cyaw");
                _vimBuffer.Process("j");
                _vimBuffer.Process("\"Cyaw");
                Assert.Equal("dogcat", _vimBuffer.RegisterMap.GetRegister('c').StringValue);
                Assert.Equal("dogcat", _vimBuffer.RegisterMap.GetRegister('C').StringValue);
            }

            /// <summary>
            /// Trying to char left from the start of the line should not cause a beep to 
            /// be emitted.  However it should cause the targetted register to be updated 
            /// </summary>
            [Fact]
            public void EmptyCharLeftMotion()
            {
                Create("dog", "cat");
                UnnamedRegister.UpdateValue("hello");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yh");
                Assert.Equal("", UnnamedRegister.StringValue);
                Assert.Equal(0, _vimHost.BeepCount);
            }

            /// <summary>
            /// Yanking a line down from the end of the buffer should not cause the 
            /// unnamed register text from resetting and it should cause a beep to occur
            /// </summary>
            [Fact]
            public void LineDownAtEndOfBuffer()
            {
                Create("dog", "cat");
                _textView.MoveCaretToLine(1);
                UnnamedRegister.UpdateValue("hello");
                _vimBuffer.Process("yj");
                Assert.Equal(1, _vimHost.BeepCount);
                Assert.Equal("hello", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// A yank of a search which needs no wrap but doesn't wrap should raise an 
            /// error message
            /// </summary>
            [Fact]
            public void WrappingSearch()
            {
                Create("dog", "cat", "dog", "fish");
                _globalSettings.WrapScan = false;
                _textView.MoveCaretToLine(2);
                _assertOnErrorMessage = false;

                var didSee = false;
                _vimBuffer.ErrorMessage +=
                    (sender, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchHitBottomWithout(@"\<dog\>"), args.Message);
                        didSee = true;
                    };
                _vimBuffer.Process("y*");
                Assert.True(didSee);
            }

            /// <summary>
            /// Doing a word yank from a blank should yank the white space till the start of 
            /// the next word 
            /// </summary>
            [Fact]
            public void WordFromBlank()
            {
                Create("dog cat  ball");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("yw");
                Assert.Equal(" ", UnnamedRegister.StringValue);
                _textView.MoveCaretTo(7);
                _vimBuffer.Process("yw");
                Assert.Equal("  ", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yanking a word in a blank line should yank the line and be a linewise motion
            /// </summary>
            [Fact]
            public void WordInEmptyLine()
            {
                Create("dog", "", "cat");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yw");
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
                Assert.Equal(Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yanking a word in a blank line with white space in the following line should 
            /// ignore the white space in the following line
            /// </summary>
            [Fact]
            public void WordInEmptyLineWithWhiteSpaceInFollowing()
            {
                Create("dog", "", "  cat");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yw");
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
                Assert.Equal(Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yanking a word which includes a blank line should still be line wise if it started at 
            /// the beginning of the previous word
            /// </summary>
            [Fact]
            public void WordEndInEmptyLine()
            {
                Create("dog", "", "cat");
                _vimBuffer.Process("y2w");
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
                Assert.Equal("dog" + Environment.NewLine + Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yanking a word which includes a blank line should not be line wise if it starts in 
            /// the middle of a word
            /// </summary>
            [Fact]
            public void WordMiddleEndInEmptyLin()
            {
                Create("dog", "", "cat");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("y2w");
                Assert.Equal(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
                Assert.Equal("og" + Environment.NewLine, UnnamedRegister.StringValue);
                _vimBuffer.Process("p");
                Assert.Equal("doog", _textView.GetLine(0).GetText());
                Assert.Equal("g", _textView.GetLine(1).GetText());
                Assert.Equal("", _textView.GetLine(2).GetText());
                Assert.Equal("cat", _textView.GetLine(3).GetText());
            }

            /// <summary>
            /// Even though the 'w' motion should move to the first non-blank in the next line
            /// it shouldn't yank that text
            /// </summary>
            [Fact]
            public void WordIndentOnNextLine()
            {
                Create("cat", "  dog");
                _vimBuffer.Process("yw");
                Assert.Equal("cat", UnnamedRegister.StringValue);
            }

            [Fact]
            public void WordViaShiftPlusSpace()
            {
                Create("cat dog bear");
                _vimBuffer.ProcessNotation("y<S-Space>");
                Assert.Equal("cat ", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// A yank which wraps around the buffer should just be a backwards motion and 
            /// shouldn't cause an error or warning message to be displayed
            /// </summary>
            [Fact]
            public void WrappingSearchSucceeds()
            {
                Create("dog", "cat", "dog", "fish");
                var didHit = false;
                _vimBuffer.WarningMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchForwardWrapped, args.Message);
                        didHit = true;
                    };
                _assertOnWarningMessage = false;
                _globalSettings.WrapScan = true;
                _textView.MoveCaretToLine(2);

                _vimBuffer.Process("y/dog", enter: true);
                Assert.Equal("dog" + Environment.NewLine + "cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.True(didHit);
            }

            /// <summary>
            /// A yank of a search which has no match should raise an error 
            /// </summary>
            [Fact]
            public void SearchMotionWithNoResult()
            {
                Create("dog", "cat", "dog", "fish");
                _globalSettings.WrapScan = false;
                _textView.MoveCaretToLine(2);
                _assertOnErrorMessage = false;

                var didSee = false;
                _vimBuffer.ErrorMessage +=
                    (sender, args) =>
                    {
                        Assert.Equal(Resources.Common_PatternNotFound("bug"), args.Message);
                        didSee = true;
                    };
                _vimBuffer.Process("y/bug", enter: true);
                Assert.True(didSee);
            }

            [Fact]
            public void SearchWithOffsetEnd()
            {
                Create("the big dog", "cat", "fish");
                _vimBuffer.ProcessNotation("y/big/e", enter: true);
                Assert.Equal("the big", UnnamedRegister.StringValue);
            }

            [Fact]
            public void SearchWithOffsetEndAndCount()
            {
                Create("the big dog", "cat", "fish");
                _vimBuffer.ProcessNotation("y/big/e-1", enter: true);
                Assert.Equal("the bi", UnnamedRegister.StringValue);
            }

            [Fact]
            public void SearchWithLineCount()
            {
                Create("the big dog", "cat", "fish");
                _vimBuffer.ProcessNotation("y/big/0", enter: true);
                Assert.Equal("the big dog" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// Doing an 'iw' yank from the start of the word should yank just the word
            /// </summary>
            [Fact]
            public void InnerWord_FromWordStart()
            {
                Create("the dog chased the ball");
                _vimBuffer.Process("yiw");
                Assert.Equal("the", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Doing an 'iw' yank with a count of 2 should yank the word and the trailing
            /// white space
            /// </summary>
            [Fact]
            public void InnerWord_FromWordStartWithCount()
            {
                Create("the dog chased the ball");
                _vimBuffer.Process("y2iw");
                Assert.Equal("the ", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Doing an 'iw' from white space should yank the white space
            /// </summary>
            [Fact]
            public void InnerWord_FromWhiteSpace()
            {
                Create("the dog chased the ball");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("y2iw");
                Assert.Equal(" dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yanking a word across new lines should not count the new line as a word. Odd since
            /// most white space is counted
            /// </summary>
            [Fact]
            public void InnerWord_AcrossNewLine()
            {
                Create("cat", "dog", "bear");
                _vimBuffer.Process("y2iw");
                Assert.Equal("cat" + Environment.NewLine + "dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Make sure a yank goes to the clipboard if we don't specify a register and the 
            /// unnamed option is set
            /// </summary>
            [Fact]
            public void UnnamedGoToClipboardIfOptionSet()
            {
                Create("cat", "dog");
                _globalSettings.Clipboard = "unnamed,autoselect";
                _vimBuffer.Process("yaw");
                Assert.Equal("cat", ClipboardDevice.Text);
            }

            /// <summary>
            /// Make sure a yank goes to unnamed if the register is explicitly specified even if the
            /// unnamed option is set in 'clipboard'
            /// </summary>
            [Fact]
            public void UnnamedExplicitBypassesClipboardOption()
            {
                Create("cat", "dog");
                _globalSettings.Clipboard = "unnamed,autoselect";
                _vimBuffer.Process("\"\"yaw");
                Assert.Equal("", ClipboardDevice.Text);
                Assert.Equal("cat", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Yank lines using the special y#y syntax
            /// </summary>
            [Fact]
            public void SpecialSyntaxSimple()
            {
                Create("cat", "dog", "bear");
                _vimBuffer.Process("y2y");
                Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// Ensure that the special linewise case which applies to delete doesn't apply
            /// to for yank operations
            /// </summary>
            [Fact]
            public void DeleteSpecialCaseDoesntApply()
            {
                Create(" cat", " dog    ", "fish");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("y/   ", enter: true);
                Assert.Equal(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// In the Visual Studio editor the last line is defined as not having a new line at the end.  In Vim it's
            /// not clear if this is defined or not.  However once it's yanked into a register it clearly has a new 
            /// line at that point.  This is visible when printing out the target register value
            /// </summary>
            [Fact]
            public void LastLineShouldAppendNewLineInRegister()
            {
                Create("cat");
                _vimBuffer.Process("yy");
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// An empty last line is treated the same as one which contains text
            /// </summary>
            [Fact]
            public void EmptyLastLineShouldAppendNewLineInRegister()
            {
                Create("cat", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yy");
                Assert.Equal(Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Vim will always add a new line to the last line of the file.  This isn't visible in Vim but it can be 
            /// viewed by directly examining the bytes of the file in question.  The new line will be the one which is 
            /// specified by the current file format.  Replicate that logic here by making sure we use the buffer 
            /// specified new line character
            /// </summary>
            [Fact]
            public void LastLineShouldUseBufferNewLine()
            {
                Create("cat");
                _textView.Options.SetOptionValue(DefaultOptions.NewLineCharacterOptionId, "\n");
                _vimBuffer.Process("yy");
                Assert.Equal("cat\n", UnnamedRegister.StringValue);
            }

            [Fact]
            public void Issue1203()
            {
                Create("cat dog", "fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation(":nmap Y y$", enter: true);
                _vimBuffer.ProcessNotation("\"aY");
                Assert.Equal("dog", RegisterMap.GetRegister('a').StringValue);
            }
        }

        public abstract class KeyMappingTest : NormalModeIntegrationTest
        {
            public sealed class AmbiguousTest : KeyMappingTest
            {
                /// <summary>
                /// When two mappings have the same prefix then they are ambiguous and require a
                /// tie breaker input.
                /// </summary>
                [Fact]
                public void Standard()
                {
                    Create("");
                    _vimBuffer.Process(":map aa foo", enter: true);
                    _vimBuffer.Process(":map aaa bar", enter: true);
                    _vimBuffer.Process("aa");
                    Assert.Equal(KeyInputSetUtil.OfString("aa"), KeyInputSetUtil.OfList(_vimBuffer.BufferedKeyInputs));
                }

                /// <summary>
                /// Resolving the ambiguity should cause both the original plus the next input to be 
                /// returned
                /// </summary>
                [Fact]
                public void ResolveShorter()
                {
                    Create("");
                    _vimBuffer.Process(":map aa ifoo", enter: true);
                    _vimBuffer.Process(":map aaa ibar", enter: true);
                    _vimBuffer.Process("aab");
                    Assert.Equal("foob", _textBuffer.GetLine(0).GetText());
                }

                [Fact]
                public void ResolveLonger()
                {
                    Create("");
                    _vimBuffer.Process(":map aa ifoo", enter: true);
                    _vimBuffer.Process(":map aaa ibar", enter: true);
                    _vimBuffer.Process("aaa");
                    Assert.Equal("bar", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// In the ambiguous double resolve case we should reslove the first but still 
                /// buffer the input for the second one
                /// </summary>
                [Fact]
                public void Double()
                {
                    Create("");
                    _vimBuffer.Process(":imap aa one", enter: true);
                    _vimBuffer.Process(":imap aaa two", enter: true);
                    _vimBuffer.Process(":imap b three", enter: true);
                    _vimBuffer.Process(":imap bb four", enter: true);
                    _vimBuffer.Process("iaab");
                    Assert.Equal("one", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(KeyInputSetUtil.OfString("b"), KeyInputSetUtil.OfList(_vimBuffer.BufferedKeyInputs));
                }

                [Fact]
                public void DoubleResolved()
                {
                    Create("");
                    _vimBuffer.Process(":imap aa one", enter: true);
                    _vimBuffer.Process(":imap aaa two", enter: true);
                    _vimBuffer.Process(":imap b three", enter: true);
                    _vimBuffer.Process(":imap bb four", enter: true);
                    _vimBuffer.Process("iaabb");
                    Assert.Equal("onefour", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(0, _vimBuffer.BufferedKeyInputs.Length);
                }
            }

            public sealed class CountTest : KeyMappingTest
            {
                /// <summary>
                /// After the count the key mapping mode should still be set to normal
                /// </summary>
                [Fact]
                public void NormalAfterCount()
                {
                    Create("");
                    _vimBuffer.Process("2");
                    Assert.Equal(_normalMode.KeyRemapMode, KeyRemapMode.Normal);
                }

                /// <summary>
                /// The 0 key shouldn't respect any key mappings when in the middle of a 
                /// count operation
                /// </summary>
                [Fact]
                public void DontMapZero()
                {
                    Create("the dog chases the cat around the tree again");
                    _vimBuffer.Process(":nmap 0 ^", enter: true);
                    _vimBuffer.Process("10l");
                    Assert.Equal(10, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Even though 0 itself doesn't map we do map strings that come after the
                /// 0 key
                /// </summary>
                [Fact]
                public void ComplexMapAfterZero()
                {
                    var str = new string('z', 1000);
                    Create(str);
                    _vimBuffer.Process(":nmap b 3", enter: true);
                    _vimBuffer.Process(":nmap a 10b", enter: true);
                    _vimBuffer.Process("al");
                    Assert.Equal(103, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Zero does map when it is a part of a larger string
                /// </summary>
                [Fact]
                public void ComplexMapWithZero()
                {
                    var str = new string('z', 1000);
                    Create(str);
                    _vimBuffer.Process(":nmap 10 20", enter: true);
                    _vimBuffer.Process("10l");
                    Assert.Equal(20, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Another strange case
                /// </summary>
                [Fact]
                public void ComplexOther()
                {
                    var str = new string('z', 1000);
                    Create(str);
                    _vimBuffer.Process(":nmap 0a 20", enter: true);
                    _vimBuffer.Process("10a");
                    Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                }

                /// <summary>
                /// The 0 key can be mapped to during a count but it can't be mapped any
                /// further.  In this case the first 'a' will map fully to 1 because it
                /// is not inside a count.  The second 'a' will stop at 0 because it is 
                /// inside a count and hence all 0 mapping is disabled
                /// </summary>
                [Fact]
                public void DontMapZeroInsideMapping()
                {
                    Create("the dog chases the cat around the tree again");
                    _vimBuffer.Process(":nmap 0 1", enter: true);
                    _vimBuffer.Process(":nmap a 0", enter: true);
                    _vimBuffer.Process("aal");
                    Assert.Equal(10, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class KeyMappingMiscTest : KeyMappingTest
            {
                [Fact]
                public void ToCharDoesNotUseMap()
                {
                    Create("bear; again: dog");
                    _vimBuffer.Process(":map ; :", enter: true);
                    _vimBuffer.Process("dt;");
                    Assert.Equal("; again: dog", _textView.GetLine(0).GetText());
                }

                [Fact]
                public void AlphaToRightMotion()
                {
                    Create("dog");
                    _vimBuffer.Process(":map a l", enter: true);
                    _vimBuffer.Process("aa");
                    Assert.Equal(2, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void OperatorPendingWithAmbiguousCommandPrefix()
                {
                    Create("dog chases the ball");
                    _vimBuffer.Process(":map a w", enter: true);
                    _vimBuffer.Process("da");
                    Assert.Equal("chases the ball", _textView.GetLine(0).GetText());
                }

                [Fact]
                public void ReplaceDoesntUseNormalMap()
                {
                    Create("dog");
                    _vimBuffer.Process(":map f g", enter: true);
                    _vimBuffer.Process("rf");
                    Assert.Equal("fog", _textView.GetLine(0).GetText());
                }

                [Fact]
                public void IncrementalSearchUsesCommandMap()
                {
                    Create("dog");
                    _vimBuffer.Process(":cmap a o", enter: true);
                    _vimBuffer.Process("/a", enter: true);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void ReverseIncrementalSearchUsesCommandMap()
                {
                    Create("dog");
                    _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
                    _vimBuffer.Process(":cmap a o", enter: true);
                    _vimBuffer.Process("?a", enter: true);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Ensure that accidental recursive key mappings don't cause VsVim to hang 
                /// indefinitely.  In gVim this will actually cause infinite mapping recursion
                /// but because we don't implemente CTRL-C as a break command yet we use a custom
                /// count to back out of the infinite mappings
                /// </summary>
                [Fact]
                public void InfiniteMappingHueristic()
                {
                    Create("cat", "dog");
                    _assertOnErrorMessage = false;
                    _vimBuffer.Process(":imap l 3l", enter: true);
                    _vimBuffer.Process("il");  // Will recurse or break out and complete
                }

                /// <summary>
                /// By default the '\' isn't special in mappings
                /// </summary>
                [Fact]
                public void BackslashIsntSpecial()
                {
                    Create("");
                    _vimBuffer.Process(@":map / /\v", enter: true);
                    _vimBuffer.Process("/");
                    Assert.Equal(@"/\v", _vimBuffer.NormalMode.Command);
                }

                /// <summary>
                /// This is a mapping operation which takes multiple stages to complete.  Round 1 
                /// will map '5' to 'd8'.  The 'd' has no mapping hence control passes back to the 
                /// IVimBuffer where it consumes 'd' and moves to operator pending mode.  The '8' 
                /// is then reconsidered and because we are now in operator pending mode it is 
                /// translated to 'w' instead of 'l'
                /// </summary>
                [Fact]
                public void MultiStepMapBetweenModes()
                {
                    Create("cat dog");
                    _vimBuffer.Process(":omap 8 w", enter: true);
                    _vimBuffer.Process(":nmap 8 l", enter: true);
                    _vimBuffer.Process(":nmap 5 d8", enter: true);
                    _vimBuffer.Process("5");
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// This is undocumented behavior that I reversed engineered through testing.  It shows 
                /// up though for every normal mode command which has a prefix shared with another normal
                /// mode command (both g and z).
                ///
                /// In this mode no mapping occurs on the specified input.  Instead it will be processed
                /// as is 
                /// </summary>
                [Fact]
                public void MultiStepMapWithAmbiguousInput()
                {
                    Create("");
                    _vimBuffer.Process(":map j ifoo", enter: true);
                    _vimBuffer.Process("gj");
                    Assert.Equal("", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// KeyInput values like 'g' and 'z' are ambiguous commands in normal mode.  They could
                /// be the start of several commands.  After pressing 'g' or 'z' the next key stroke 
                /// won't participate in normal mode mapping.  
                /// 
                /// Note: This is completely undocumented behavior as far as I can tell.  But you can
                /// easily prove it with the below code 
                /// </summary>
                [Fact]
                public void TwoKeyCommandsHaveNoRemapAfterFirstKey()
                {
                    Create("cat");
                    _vimBuffer.Process(":map j I", enter: true);
                    _vimBuffer.Process("gj");
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    _vimBuffer.Process("gI");
                    Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                }

                /// <summary>
                /// Scenario from above but explicitly verify the KeyRemapMode
                /// </summary>
                [Fact]
                public void TwoKeyCommandsHaveNoRemapAfterFirstKey_Mode()
                {
                    Create("cat");
                    _vimBuffer.Process("g");
                    Assert.Equal(_vimBuffer.NormalMode.KeyRemapMode, KeyRemapMode.None);
                }

                [Fact]
                public void ProcessBufferedKeyInputsShouldMap()
                {
                    Create("");
                    _vimBuffer.Process(":imap x short", enter: true);
                    _vimBuffer.Process(":imap xx long", enter: true);
                    _vimBuffer.Process("ix");
                    Assert.Equal(1, _vimBuffer.BufferedKeyInputs.Length);
                    _vimBuffer.ProcessBufferedKeyInputs();
                    Assert.Equal("short", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Verify that the :omap command only takes affect when we are in operator
                /// pending mode
                /// </summary>
                [Fact]
                public void OperatorPendingWithDelete()
                {
                    Create("cat dog");
                    _vimBuffer.Process(":omap l w", enter: true);
                    _vimBuffer.Process("dll");
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// In the case where the remapping is not considered for the first character becuase 
                /// of the matching prefix problem (:nmap a ab) make sure that the remapping of the  
                /// latter characters is considered in the mode which occurs after processing the 
                /// first character
                /// </summary>
                [Fact]
                public void MatchingPrefixWithModeSwitch()
                {
                    Create("");
                    _vimBuffer.Process(":nmap i ia", enter: true);
                    _vimBuffer.Process(":imap a hit", enter: true);
                    _vimBuffer.Process("i");
                    Assert.Equal("hit", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Even though keypad divide is processed as simply / it should still go through key
                /// mapping as kDivide
                /// </summary>
                [Fact]
                public void KeypadDivideMustMap()
                {
                    Create("cat dog");
                    _vimBuffer.Process(":nmap <kDivide> i", enter: true);
                    _vimBuffer.Process(VimKey.KeypadDivide);
                    Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                }

                /// <summary>
                /// The {C-H} and {BS} key combinations aren't true equivalent keys.  They can be bound
                /// to separate commands
                /// </summary>
                [Fact]
                public void ControlHAndBackspace()
                {
                    Create("");
                    _vimBuffer.Process(":nmap <C-H> icontrol h<Esc>", enter: true);
                    _vimBuffer.Process(":nmap <BS> a and backspace", enter: true);
                    _vimBuffer.ProcessNotation("<C-H><BS>");
                    Assert.Equal("control h and backspace", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// After the register the key mapping mode is not defined
                /// </summary>
                [Fact]
                public void NoneAfterRegister()
                {
                    Create("");
                    _vimBuffer.Process("\"");
                    Assert.Equal(_normalMode.KeyRemapMode, KeyRemapMode.None);
                }

                /// <summary>
                /// After a register is entered the KeyRemapMode should still be normal mode
                /// </summary>
                [Fact]
                public void KeyRemapModeAfterRegister()
                {
                    Create("");
                    _vimBuffer.Process("\"a");
                    Assert.Equal(KeyRemapMode.Normal, _normalMode.KeyRemapMode);
                }

                /// <summary>
                /// After a count is entered the KeyRemapMode should still be normal mode
                /// </summary>
                [Fact]
                public void KeyRemapModeAfterCount()
                {
                    Create("");
                    _vimBuffer.Process("3");
                    Assert.Equal(KeyRemapMode.Normal, _normalMode.KeyRemapMode);
                }

                /// <summary>
                /// Make sure that we don't regress issue 522.  In this particular case the user
                /// has defined apparent recursive mappings and we need to make sure they aren't
                /// treated as such
                ///
                /// Strictly speaking the ounmap calls aren't necessary but keeping them here for 
                /// completeness with the sample
                /// </summary>
                [Fact]
                public void Issue522()
                {
                    Create("cat", "dog");
                    _vimBuffer.Process(":map j gj", enter: true);
                    _vimBuffer.Process(":ounmap j", enter: true);
                    _vimBuffer.Process(":map k gk", enter: true);
                    _vimBuffer.Process(":ounmap k", enter: true);
                    _vimBuffer.Process("j");
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void Issue896()
                {
                    Create("");
                    _globalSettings.HighlightSearch = true;
                    _vimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                    _vimBuffer.Process(":nnoremap <Esc> :nohl<Enter><Esc>", enter: true);

                    var ran = false;
                    _vimData.DisplayPatternChanged += delegate { ran = true; };
                    _vimBuffer.Process(VimKey.Escape);
                    Assert.True(ran);
                    Assert.True(String.IsNullOrEmpty(_vimData.DisplayPattern));
                }


                /// <summary>
                /// Make sure the ambiguous case (:help map-ambiguous) is done correctly in the
                /// face of operators
                /// </summary>
                [Fact]
                public void Issue880_Part1()
                {
                    Create("cat", "dog");
                    _vimBuffer.Process(":nnoremap c y", enter: true);
                    _vimBuffer.Process(":nnoremap cc yy", enter: true);
                    _vimBuffer.Process("cc");
                    Assert.Equal("cat\r\n", UnnamedRegister.StringValue);
                }

                /// <summary>
                /// Make sure that we properly handle the mapping during operator pending
                /// </summary>
                [Fact]
                public void Issue880_Part2()
                {
                    Create("cat", "dog");
                    _vimBuffer.Process(":nnoremap w e", enter: true);
                    _vimBuffer.Process("yw");
                    Assert.Equal("cat", UnnamedRegister.StringValue);
                }

                /// <summary>
                /// When 0 is mapped it should only be used before we get into a count situation.  Once inside of a 
                /// count operation we should ignore the :nmap of 0 and instead us 0 as a number
                /// </summary>
                [Fact]
                public void Issue890()
                {
                    Create("cat dog fish big tree to chase");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.ProcessNotation(@":nmap 0 ^", enter: true);
                    _vimBuffer.ProcessNotation("10l");
                    Assert.Equal(11, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Make sure that key mapping correctly takse effect after a count.  For example when trying
                /// to replay a macro with a count 
                /// </summary>
                [Fact]
                public void Issue1083()
                {
                    Create("");
                    var keyInputSet = KeyNotationUtil.StringToKeyInputSet("il<Esc>");
                    RegisterMap.GetRegister('q').UpdateValue(keyInputSet.KeyInputs.ToArray());
                    _vimBuffer.Process(":nmap <space> @q", enter: true);
                    _vimBuffer.ProcessNotation("2<Space>");
                    Assert.Equal("ll", _textBuffer.CurrentSnapshot.GetText());
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void Issue1368()
                {
                    // At the moment we don't support the options although we do process the keys anyways
                    _assertOnWarningMessage = false;

                    Create("");
                    _vimBuffer.Process(":nmap <silent> // icat<Esc>", enter: true);
                    _vimBuffer.Process("//");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                }

                [Fact]
                public void Issue1435()
                {
                    Create("cat", "dog");
                    _localSettings.ShiftWidth = 2;
                    _assertOnErrorMessage = false;
                    _vimBuffer.Process(":nmap > >> \" better indentation", enter: true);
                    _vimBuffer.Process(">");
                    Assert.Equal("  cat", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                }
            }
        }

        public sealed class LineToLineMotionTest : NormalModeIntegrationTest
        {
            [Fact]
            public void ColumnShorter()
            {
                Create("the dog", "the", "cat");
                Vim.GlobalSettings.StartOfLine = false;
                _textView.MoveCaretToLine(lineNumber: 0, column: 4);
                _vimBuffer.Process("2G");
                Assert.Equal(_textBuffer.GetPointInLine(line: 1, column: 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Issue 1854
            /// </summary>
            [Fact]
            public void MaintainCaretColumn()
            {
                Create("the dog", "the", "cat");
                Vim.GlobalSettings.StartOfLine = false;
                var point = _textBuffer.GetPointInLine(line: 0, column: 4);
                _textView.MoveCaretTo(point);
                _vimBuffer.Process("2Gk");
                Assert.Equal(point, _textView.GetCaretPoint());
            }
        }

        public sealed class LastSearchTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Incremental search should re-use the last search if the entered search string is
            /// empty.  It should ignore the direction though and base it's search off the '/' or
            /// '?' it was created with
            /// </summary>
            [Fact]
            public void IncrementalReuse()
            {
                Create("dog cat dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.LocalSettings.GlobalSettings.WrapScan = false;
                _vimBuffer.VimData.LastSearchData = new SearchData("dog", SearchPath.Backward);
                _vimBuffer.Process('/');
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal(8, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Substitute command should set the LastSearch value
            /// </summary>
            [Fact]
            public void SetBySubstitute()
            {
                Create("dog cat dog");
                _vimBuffer.Process(":s/dog/cat", enter: true);
                Assert.Equal("dog", _vimBuffer.VimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Substitute command should re-use the LastSearch value if there is no specific 
            /// search value set
            /// </summary>
            [Fact]
            public void UsedBySubstitute()
            {
                Create("dog cat dog");
                _vimBuffer.VimData.LastSearchData = new SearchData("dog", SearchPath.Forward);
                _vimBuffer.Process(":s//cat", enter: true);
                Assert.Equal("cat cat dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// The search options used by a :s command should not be stored.  For example the 
            /// 'i' flag is used only for the :s command and not for repeats of the search 
            /// later on.
            /// </summary>
            [Fact]
            public void DontStoreSearchOptions()
            {
                Create("cat", "dog", "cat");
                _assertOnErrorMessage = false;
                _globalSettings.IgnoreCase = false;
                _globalSettings.WrapScan = true;
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process(":s/CAT/fish/i", enter: true);
                Assert.Equal("fish", _textView.GetLine(2).GetText());
                var didHit = false;
                _vimBuffer.ErrorMessage +=
                    (sender, args) =>
                    {
                        Assert.Equal(Resources.Common_PatternNotFound("CAT"), args.Message);
                        didHit = true;
                    };
                _vimBuffer.Process("n");
                Assert.True(didHit);
            }

            [Fact]
            public void Issue1244_1()
            {
                Create("cat", "dog", "cat");
                _vimBuffer.ProcessNotation(":s/cat/foo", enter: true);
                _vimBuffer.ProcessNotation("n");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Issue1244_2()
            {
                Create("cat", "dog", "cat", "dog");
                _vimBuffer.ProcessNotation(":/dog", enter: true);
                _vimBuffer.ProcessNotation("n");
                Assert.Equal(_textBuffer.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Search offsets should apply to the 'n' and 'N' searches
            /// </summary>
            [Fact]
            public void Issue1244_3()
            {
                Create("cat", "dog", "cat", "dog");
                _vimBuffer.ProcessNotation("/d/e+1", enter: true);
                _vimBuffer.ProcessNotation("n");
                Assert.Equal(_textBuffer.GetPointInLine(3, 1), _textView.GetCaretPoint());
            }
        }

        public sealed class MapLeaderTest : NormalModeIntegrationTest
        {
            [Fact]
            public void SimpleUpdatesVariableMap()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=""x""", enter: true);
                var value = Vim.VariableMap["mapleader"];
                Assert.Equal("x", value.AsString().Item);
            }

            [Fact]
            public void Simple()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=""x""", enter: true);
                _vimBuffer.Process(@":nmap <Leader>i ihit it", enter: true);
                _vimBuffer.Process(@"xi");
                Assert.Equal("hit it", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The Leader value can occur in the middle of a mapping.  Make sure that it's 
            /// supported
            /// </summary>
            [Fact]
            public void InMiddle()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=""x""", enter: true);
                _vimBuffer.Process(@":nmap i<Leader>i ihit it", enter: true);
                _vimBuffer.Process(@"ixi");
                Assert.Equal("hit it", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The Leader value can also occur on the RHS of the mapping string.
            /// </summary>
            [Fact]
            public void InRight()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=""x""", enter: true);
                _vimBuffer.Process(@":nmap ii ihit it<Leader>", enter: true);
                _vimBuffer.Process(@"ii");
                Assert.Equal("hit itx", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// The mapleader value is interpreted at the point of definition.  It doesn't get
            /// reinterpretted after a change occurs
            /// </summary>
            [Fact]
            public void LeaderInterpretedAtDefintion()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=""x""", enter: true);
                _vimBuffer.Process(@":nmap <Leader>i ihit it", enter: true);
                _vimBuffer.Process(@":let mapleader=""z""", enter: true);
                _vimBuffer.Process(@"xi");
                Assert.Equal("hit it", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// If the mapleader value is defined as a number it should be seen as string when 
            /// we use it in a mapping
            /// </summary>
            [Fact]
            public void NumberSeenAsString()
            {
                Create("");
                _vimBuffer.Process(@":let mapleader=2", enter: true);
                _vimBuffer.Process(@":nmap <Leader>i ihit it", enter: true);
                _vimBuffer.Process(@"2i");
                Assert.Equal("hit it", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// When there is no mapleader value then the Leader entry should be replaced with
            /// a backslash
            /// </summary>
            [Fact]
            public void NoMapLeaderValue()
            {
                Create("");
                _vimBuffer.Process(@":nmap <Leader>i ihit it", enter: true);
                _vimBuffer.Process(@"\i");
                Assert.False(VariableMap.ContainsKey("mapleader"));
                Assert.Equal("hit it", _textBuffer.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }
        }

        public sealed class MarksTest : NormalModeIntegrationTest
        {
            [Fact]
            public void SelectionEndIsExclusive()
            {
                Create("the brown dog");
                var span = new SnapshotSpan(_textView.GetPoint(4), _textView.GetPoint(9));
                Assert.Equal("brown", span.GetText());
                var visualSelection = VisualSelection.NewCharacter(new CharacterSpan(span), SearchPath.Backward);
                _vimTextBuffer.LastVisualSelection = FSharpOption.Create(visualSelection);
                _vimBuffer.Process("y`>");
                Assert.Equal("the brown", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            [Fact]
            public void NamedMarkIsExclusive()
            {
                Create("the brown dog");
                var point = _textView.GetPoint(8);
                Assert.Equal('n', point.GetChar());
                _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.OfChar('b').Value, 0, 8);
                _vimBuffer.Process("y`b");
                Assert.Equal("the brow", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// The last jump mark is a user settable item
            /// </summary>
            [Fact]
            public void LastJump_Set()
            {
                Create("cat", "fish", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("m'");
                Assert.Equal(_textBuffer.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
            }

            /// <summary>
            /// Make sure that a jump operation to a different mark will properly update the LastMark
            /// selection
            /// </summary>
            [Fact]
            public void LastJump_AfterMarkJump()
            {
                Create("cat", "fish", "dog");
                _vimBuffer.Process("mc");   // Mark the line
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("'c");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
            }

            /// <summary>
            /// Jumping with the '' command should set the last jump to the current location.  So doing
            /// a '' in sequence should just jump back and forth
            /// </summary>
            [Fact]
            public void LastJump_BackAndForth()
            {
                Create("cat", "fish", "dog");
                _vimBuffer.Process("mc");   // Mark the line
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("'c");
                for (var i = 0; i < 10; i++)
                {
                    _vimBuffer.Process("''");
                    var line = (i % 2 != 0) ? 0 : 1;
                    Assert.Equal(_textBuffer.GetLine(line).Start, _textView.GetCaretPoint().Position);
                }
            }

            /// <summary>
            /// Navigating the jump list shouldn't affect the LastJump mark
            /// </summary>
            [Fact]
            public void LastJump_NavigateJumpList()
            {
                Create("cat", "fish", "dog");
                _vimBuffer.Process("majmbjmc'a'b'c");
                Assert.Equal(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.Equal(_textView.GetLine(1).Start, _jumpList.LastJumpLocation.Value.Position);
                Assert.Equal(_textView.GetLine(0).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Jumping to a mark with ` should jump to the literal mark wherever it occurs 
            /// in the line
            /// </summary>
            [Fact]
            public void JumpToMark()
            {
                Create("cat", "  dog");
                Vim.MarkMap.SetLocalMark('a', _vimBufferData, 1, 3);
                _vimBuffer.Process("`a");
                Assert.Equal(_textBuffer.GetPointInLine(1, 3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Jumping to a mark with ' should jump to the start of the line where the mark 
            /// occurs
            /// </summary>
            [Fact]
            public void JumpToMarkLine()
            {
                Create("cat", "  dog");
                Vim.MarkMap.SetLocalMark('a', _vimBufferData, 1, 3);
                _vimBuffer.Process("'a");
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The delete character command should update the last edit point 
            /// </summary>
            [Fact]
            public void LastEditPoint_DeleteCharacter()
            {
                Create("cat", "dog");
                Assert.True(_vimTextBuffer.LastEditPoint.IsNone());
                _vimBuffer.ProcessNotation("lx");
                Assert.True(_vimTextBuffer.LastEditPoint.IsSome());
                Assert.Equal(1, _vimTextBuffer.LastEditPoint.Value);
            }

            /// <summary>
            /// The delete line command should update the last edit point
            /// </summary>
            [Fact]
            public void LastEditPoint_DeleteLine()
            {
                Create("cat", "dog", "tree");
                Assert.True(_vimTextBuffer.LastEditPoint.IsNone());
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("dd");
                Assert.True(_vimTextBuffer.LastEditPoint.IsSome());
                Assert.Equal(_textBuffer.GetLine(1).Start, _vimTextBuffer.LastEditPoint.Value);
            }
        }

        public sealed class ChangeLinesTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Caret should maintain position but the text should be deleted.  The caret 
            /// exists in virtual space
            /// </summary>
            [Fact]
            public void AutoIndentShouldPreserveOnSingle()
            {
                Create("  dog", "  cat", "  tree");
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("cc");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
                Assert.Equal("", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void NoAutoIndentShouldRemoveAllOnSingle()
            {
                Create("  dog", "  cat");
                _vimBuffer.LocalSettings.AutoIndent = false;
                _vimBuffer.Process("cc");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Caret position should be preserved in virtual space
            /// </summary>
            [Fact]
            public void AutoIndentShouldPreserveOnMultiple()
            {
                Create("  dog", "  cat", "  tree");
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("2cc");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("  tree", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Caret point should be preserved in virtual space
            /// </summary>
            [Fact]
            public void AutoIndentShouldPreserveFirstOneOnMultiple()
            {
                Create("    dog", "  cat", "  tree");
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("2cc");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretVirtualPoint().VirtualSpaces);
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("  tree", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void NoAutoIndentShouldRemoveAllOnMultiple()
            {
                Create("  dog", "  cat", "  tree");
                _vimBuffer.LocalSettings.AutoIndent = false;
                _vimBuffer.Process("2cc");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("  tree", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// When 'autoindent' is on we need to keep tabs and spaces at the start of the line
            /// </summary>
            [Fact]
            public void AutoIndent_KeepTabsAndSpaces()
            {
                Create("\t  dog", "\t  cat");
                _localSettings.AutoIndent = true;
                _localSettings.TabStop = 4;
                _localSettings.ExpandTab = false;
                _vimBuffer.Process("ccb");
                Assert.Equal("\t  b", _textView.GetLine(0).GetText());
                Assert.Equal("\t  cat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPoint(4), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When 'autoindent' is on we need to keep tabs at the start of the line
            /// </summary>
            [Fact]
            public void AutoIndent_KeepTabs()
            {
                Create("\tdog", "\tcat");
                _localSettings.AutoIndent = true;
                _localSettings.TabStop = 4;
                _localSettings.ExpandTab = false;
                _vimBuffer.Process("ccb");
                Assert.Equal("\tb", _textView.GetLine(0).GetText());
                Assert.Equal("\tcat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPoint(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When there are tabs involved the virtual space position of the caret after a 
            /// 'cc' operation should be (tabs * tabWidth + spaces)
            /// </summary>
            [Fact]
            public void AutoIndent_VirtualSpace()
            {
                Create("\t  dog", "\t cat");
                _localSettings.AutoIndent = true;
                _localSettings.TabStop = 4;
                _vimBuffer.Process("cc");
                Assert.Equal(6, _textView.GetCaretVirtualPoint().VirtualSpaces);
            }

            [Fact]
            public void Issue1145()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.ProcessNotation("c2cabc<Esc>");
                Assert.Equal(new[] { "abc", "fish" }, _textBuffer.GetLines());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class ChangeMotionTest : NormalModeIntegrationTest
        {
            [Fact]
            public void EndOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.Process("c$");
                Assert.Equal(new[] { "", "dog" }, _textBuffer.GetLines());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat", UnnamedRegister.StringValue);
            }

            [Fact]
            public void NextLineMotion()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.ProcessNotation("c<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "", "fish" }, _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Any motion which ends in the line break should cause only the text up till
            /// the line break to be deleted.  It still goes down as a line wise operation in
            /// the register though
            /// </summary>
            [Fact]
            public void EndInLineBreak()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.ProcessNotation("c/d", enter: true);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "", "dog", "fish" }, _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The motion into the line break rule is true even over several lines
            /// </summary>
            [Fact]
            public void EndInLineBreakOverMultiple()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.ProcessNotation("c/f<CR>");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "", "fish" }, _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void UndoEndOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("c$<Esc>u");
                Assert.Equal(new[] { "cat", "dog" }, _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void UndoEndInLineBreakOverMultiple()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.ProcessNotation("c/f<CR><Esc>u");
                Assert.Equal(new[] { "cat", "dog", "fish" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The undo of a line wise change of several lines should move the caret back to the
            /// start of the second line.  I can find no documentation for this behavior but 
            /// experiments show this is the intended behavior
            /// </summary>
            [Fact]
            public void UndoLineWiseManyLines()
            {
                Create("cat", "dog", "fish", "tree");
                _vimBuffer.ProcessNotation("c/tr<CR><Esc>u");
                Assert.Equal(new[] { "cat", "dog", "fish", "tree" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Very similar to the above test case.  This time though we start at the 'a' in 
            /// 'cat'.  This changes the motion into a character wise motion.  Hence the undo
            /// goes back to the start of the change not the second line 
            /// </summary>
            [Fact]
            public void UndoCharacterWiseManyLines()
            {
                Create("cat", "dog", "fish", "tree");
                _vimBuffer.ProcessNotation("lc/tr<CR><Esc>u");
                Assert.Equal(new[] { "cat", "dog", "fish", "tree" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(0, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Even though the caret starts in the middle of the word it should be reset back to
            /// the start of the motion in an undo
            /// </summary>
            [Fact]
            public void UndoWholeWord()
            {
                Create("big cat dog");
                _textView.MoveCaretTo(5);
                _vimBuffer.ProcessNotation("caw<Esc>u");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Issue1128()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("cw<Esc>u");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When a 'cw' command is repeated once with '.', 'uu' should undo both
            /// </summary>
            [Fact]
            public void Issue1266()
            {
                Create("cat cat");
                _vimBuffer.ProcessNotation("cwdog<Esc>w.");
                Assert.Equal("dog dog", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("uu");
                Assert.Equal("cat cat", _textView.GetLine(0).GetText());
            }
        }

        public sealed class UndoLineTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Undo line should work when all edits occur one the same line
            /// </summary>
            [Fact]
            public void UndoLine_Basic()
            {
                Create("aaa bbb ccc", "ddd eee fff");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("1Gwixxx <Esc>ww.");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("U");
                Assert.Equal("aaa bbb ccc", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Undo line should work even when the caret has moved off of the line
            /// </summary>
            [Fact]
            public void UndoLine_WithMotionAfterChanges()
            {
                Create("aaa bbb ccc", "ddd eee fff");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("1Gwixxx <Esc>ww.G");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("U");
                Assert.Equal("aaa bbb ccc", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Undo line twice should reapply the line changes
            /// </summary>
            [Fact]
            public void UndoLine_Twice()
            {
                Create("aaa bbb ccc", "ddd eee fff");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("1Gwixxx <Esc>ww.");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("U");
                Assert.Equal("aaa bbb ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("U");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Undo line should itself be undoable and redoable
            /// </summary>
            [Fact]
            public void UndoLine_FollowedByUndoAndRedo()
            {
                Create("aaa bbb ccc", "ddd eee fff");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("1Gwixxx <Esc>ww.");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("U");
                Assert.Equal("aaa bbb ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("u");
                Assert.Equal("aaa xxx bbb xxx ccc", _textView.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("<C-r>");
                Assert.Equal("aaa bbb ccc", _textView.GetLine(0).GetText());
            }
        }

        public abstract class IncrementalSearchTest : NormalModeIntegrationTest
        {
            public sealed class StandardTest : IncrementalSearchTest
            {
                [Fact]
                public void VeryNoMagic()
                {
                    Create("dog", "cat");
                    _vimBuffer.Process(@"/\Vog", enter: true);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Make sure the caret goes to column 0 on the next line even if one of the 
                /// motion adjustment applies (:help exclusive-linewise)
                /// </summary>
                [Fact]
                public void CaretOnColumnZero()
                {
                    Create("hello", "world");
                    _textView.MoveCaretTo(2);
                    _vimBuffer.Process("/world", enter: true);
                    Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                }

                /// <summary>
                /// Make sure we respect the \c marker over the 'ignorecase' option even if it appears
                /// at the end of the string
                /// </summary>
                [Fact]
                public void CaseInsensitiveAtEndOfSearhString()
                {
                    Create("cat dog bear");
                    _vimBuffer.Process("/DOG");
                    Assert.True(_vimBuffer.IncrementalSearch.CurrentSearchResult.IsNotFound);
                    _vimBuffer.Process(@"\c", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Make sure we respect the \c marker over the 'ignorecase' option even if it appears
                /// in the middle of the string
                /// </summary>
                [Fact]
                public void CaseInsensitiveInMiddleOfSearhString()
                {
                    Create("cat dog bear");
                    _vimBuffer.Process(@"/D\cOG", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void CaseSensitive()
                {
                    Create("dogDOG", "cat");
                    _vimBuffer.Process(@"/\COG", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// The case option in the search string should take precedence over the 
                /// ignore case option
                /// </summary>
                [Fact]
                public void CaseSensitiveAgain()
                {
                    Create("hello dog DOG");
                    _globalSettings.IgnoreCase = true;
                    _vimBuffer.Process(@"/\CDOG", enter: true);
                    Assert.Equal(10, _textView.GetCaretPoint());
                }

                [Fact]
                public void HandlesEscape()
                {
                    Create("dog");
                    _vimBuffer.Process("/do");
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void HandlesEscapeInOperator()
                {
                    Create("dog");
                    _vimBuffer.Process("d/do");
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void UsedAsOperatorMotion()
                {
                    Create("dog cat tree");
                    _vimBuffer.Process("d/cat", enter: true);
                    Assert.Equal("cat tree", _textView.GetLine(0).GetText());
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void DontMoveCaretDuringSearch()
                {
                    Create("dog cat tree");
                    _vimBuffer.Process("/cat");
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void MoveCaretAfterEnter()
                {
                    Create("dog cat tree");
                    _vimBuffer.Process("/cat", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Verify a couple of searches for {} work as expected
                /// </summary>
                [Fact]
                public void Braces()
                {
                    Create("func() {   }");
                    Action<string, int> doSearch =
                        (pattern, position) =>
                        {
                            _textView.MoveCaretTo(0);
                            _vimBuffer.Process(pattern);
                            _vimBuffer.Process(VimKey.Enter);
                            Assert.Equal(position, _textView.GetCaretPoint().Position);
                        };
                    doSearch(@"/{", 7);
                    doSearch(@"/}", 11);

                    _assertOnErrorMessage = false;
                    doSearch(@"/\<{\>", 0);  // Should fail
                    doSearch(@"/\<}\>", 0);  // Should fail
                }

                /// <summary>
                /// Verify we can use the \1 in an incremental search for matches
                /// </summary>
                [Fact]
                public void GroupingMatch()
                {
                    Create("dog", "dog::dog", "dog");
                    _vimBuffer.Process(@"/\(dog\)::\1", enter: true);
                    Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                }

                /// <summary>
                /// Unless kDivide is mapped to another key it should be processed exactly as 
                /// / is processed
                /// </summary>
                [Fact]
                public void KeypadDivideShouldBeginSearch()
                {
                    Create("cat dog");
                    _vimBuffer.ProcessNotation("<kDivide>a", enter: true);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Searching for an unmatched bracket shouldn't require any escapes
                /// </summary>
                [Fact]
                public void UnmatchedOpenBracket()
                {
                    Create("int[]");
                    _vimBuffer.ProcessNotation("/[", enter: true);
                    Assert.Equal(3, _textView.GetCaretPoint());
                }

                /// <summary>
                /// Searching for an unmatched bracket shouldn't require any escapes
                /// </summary>
                [Fact]
                public void UnmatchedCloseBracket()
                {
                    Create("int[]");
                    _vimBuffer.ProcessNotation("/]", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint());
                }

                /// <summary>
                /// A bracket pair which has no content should still match literally and not as a 
                /// character set atom
                /// </summary>
                [Fact]
                public void BracketPairWithNoContents()
                {
                    Create("int[]");
                    _vimBuffer.ProcessNotation("/[]", enter: true);
                    Assert.Equal(3, _textView.GetCaretPoint());
                }

                [Fact]
                public void BackwardsSlashIsUsedDirectly()
                {
                    Create("cat", "cat/", "dog");
                    _textView.MoveCaretToLine(2);
                    _vimBuffer.ProcessNotation("?cat/", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void ForwardQuestionIsUsedDirectly()
                {
                    Create("tree", "cat", "cat?", "dog");
                    _vimBuffer.ProcessNotation("/cat?", enter: true);
                    Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void ErrorUnmatchedOpenParen()
                {
                    Create("");
                    _assertOnErrorMessage = false;
                    var fired = false;
                    _vimBuffer.ErrorMessage += (_, e) =>
                        {
                            Assert.Equal(e.Message, Resources.Regex_UnmatchedParen);
                            fired = true;
                        };

                    _vimBuffer.Process(@"/\v(9", enter: true);
                    Assert.True(fired);
                }

                [Fact]
                public void ErrorUnmatchedOpenBrace()
                {
                    Create("");
                    _assertOnErrorMessage = false;
                    var fired = false;
                    _vimBuffer.ErrorMessage += (_, e) =>
                        {
                            Assert.Equal(e.Message, Resources.Regex_UnmatchedBrace);
                            fired = true;
                        };

                    _vimBuffer.Process(@"/\v{9", enter: true);
                    Assert.True(fired);
                }

                [Fact]
                public void Issue1392()
                {
                    Create(" MyOwnData MyOwnData MyOwnData");
                    _vimBuffer.ProcessNotation(@"/\w\+Data", enter: true);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                    _vimBuffer.ProcessNotation(@"/\w\+Data", enter: true);
                    Assert.Equal(11, _textView.GetCaretPoint().Position);
                    _vimBuffer.ProcessNotation(@"/\w\+Data", enter: true);
                    Assert.Equal(21, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class OffsetTest : IncrementalSearchTest
            {
                [Fact]
                public void LineBelowImplicitCount()
                {
                    Create("the big", "cat", "dog");
                    _vimBuffer.ProcessNotation("/big/+", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                    Assert.Equal("big", _vimData.LastSearchData.Pattern);
                }

                [Fact]
                public void LineBelowExplicitCount()
                {
                    Create("the big", "cat", "dog");
                    _vimBuffer.ProcessNotation("/big/+1", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                    Assert.Equal("big", _vimData.LastSearchData.Pattern);
                }

                /// <summary>
                /// When the count is too big the caret should move to the start of the last line
                /// in the buffer
                /// </summary>
                [Fact]
                public void LineBelowExplicitCountTooBig()
                {
                    Create("the big", "cat", "dog");
                    _vimBuffer.ProcessNotation("/big/+100", enter: true);
                    Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
                    Assert.Equal("big", _vimData.LastSearchData.Pattern);
                }

                [Fact]
                public void LineBelowExplicitCountNoPlus()
                {
                    Create("the big", "cat", "dog");
                    _vimBuffer.ProcessNotation("/big/1", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                    Assert.Equal("big", _vimData.LastSearchData.Pattern);
                }

                [Fact]
                public void LineAboveImplicitCount()
                {
                    Create("the big", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/dog/-", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void LineAboveImplicitCount2()
                {
                    Create("the big", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/fish/-", enter: true);
                    Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void LineAboveExplicitCount()
                {
                    Create("the big", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/fish/-1", enter: true);
                    Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void LineAboveExplicitCountTooBig()
                {
                    Create("the big", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/fish/-1000", enter: true);
                    Assert.Equal(_textBuffer.GetLine(0).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void EndNoCount()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/e", enter: true);
                    Assert.Equal(6, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void EndExplicitCount1()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/e0", enter: true);
                    Assert.Equal(6, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void EndExplicitCount2()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/e1", enter: true);
                    Assert.Equal(7, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void EndExplicitCount3()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/e+1", enter: true);
                    Assert.Equal(7, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// New line characters don't actually count as characters when considering 
                /// search offsets.  Instead we treat them as no character and count everything
                /// else
                /// </summary>
                [Fact]
                public void EndExplicitCountExceedsLineLength()
                {
                    Create("test", "cat", "dog", "fish");
                    _assertOnWarningMessage = false;
                    _vimBuffer.ProcessNotation("/test/e1", enter: true);
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                }

                [Fact]
                public void EndExplicitCountExceedsLineLength2()
                {
                    Create("test", "cat", "dog", "fish");
                    _assertOnWarningMessage = false;
                    _vimBuffer.ProcessNotation("/test/e2", enter: true);
                    Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
                }

                [Fact]
                public void BeginImplicitCount()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/s", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void BeginExplicitCount1()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/s0", enter: true);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void BeginExplicitCount2()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/s1", enter: true);
                    Assert.Equal(5, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void BeginExplicitCount3()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/s-1", enter: true);
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void BeginExplicitCountAlternateSyntax()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/b-1", enter: true);
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void BeginBackwardsExplicitCount()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _textView.MoveCaretToLine(3);
                    _vimBuffer.ProcessNotation("?dog?b1", enter: true);
                    Assert.Equal(_textBuffer.GetPointInLine(2, 1), _textView.GetCaretPoint());
                }

                [Fact]
                public void Search()
                {
                    Create("the big dog", "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("/big/;/dog", enter: true);
                    Assert.Equal(8, _textView.GetCaretPoint().Position);
                    Assert.Equal("dog", _vimData.LastSearchData.Pattern);
                }
            }
        }

        public sealed class InsertLineBelowCaretTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Ensure the text inserted is repeated after the Escape
            /// </summary>
            [Fact]
            public void WithCount()
            {
                Create("dog", "bear");
                _vimBuffer.Process("2o");
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal("bear", _textView.GetLine(3).GetText());
                Assert.Equal(_textView.GetLine(2).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that we use the proper line ending when inserting a new line vs. simply choosing 
            /// to use Environment.NewLine
            /// </summary>
            [Fact]
            public void AlternateNewLine()
            {
                Create("");
                _textBuffer.Replace(new Span(0, 0), "cat\ndog");
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("o");
                Assert.Equal("cat\n", _textBuffer.GetLine(0).ExtentIncludingLineBreak.GetText());
                Assert.Equal("\n", _textBuffer.GetLine(1).ExtentIncludingLineBreak.GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).ExtentIncludingLineBreak.GetText());
            }

            /// <summary>
            /// An 'o' command which starts on a folded line should insert the line after the fold
            /// </summary>
            [Fact]
            public void FromFold()
            {
                Create("cat", "dog", "fish", "tree");
                _foldManager.CreateFold(_textView.GetLineRange(1, 2));
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("o");
                Assert.Equal("fish", _textBuffer.GetLine(2).GetText());
                Assert.Equal("", _textBuffer.GetLine(3).GetText());
            }

            /// <summary>
            /// The 'o' command should always position the caret on the line below even when it's the
            /// last line in the buffer
            /// </summary>
            [Fact]
            public void LastLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("o");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the caret is properly positioned on the new line and not the last
            /// line
            /// 
            /// Issue 944
            /// </summary>
            [Fact]
            public void LastLineBlank()
            {
                Create("cat", "dog", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("o");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class MaintainCaretColumnTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Simple maintain of the caret column going down
            /// </summary>
            [Fact]
            public void Down()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                _textView.MoveCaretTo(8);
                _vimBuffer.Process('j');
                Assert.Equal(_textView.GetPointInLine(1, 4), _textView.GetCaretPoint());
                _vimBuffer.Process('j');
                Assert.Equal(_textView.GetPointInLine(2, 8), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Simple maintain of the caret column going up
            /// </summary>
            [Fact]
            public void Up()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                _textView.MoveCaretTo(_textView.GetPointInLine(2, 8));
                _vimBuffer.Process('k');
                Assert.Equal(_textView.GetPointInLine(1, 4), _textView.GetCaretPoint());
                _vimBuffer.Process('k');
                Assert.Equal(_textView.GetPointInLine(0, 8), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The column should not be maintained once the caret goes any other direction
            /// </summary>
            [Fact]
            public void ResetOnMove()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                _textView.MoveCaretTo(_textView.GetPointInLine(2, 8));
                _vimBuffer.Process("kh");
                Assert.Equal(_textView.GetPointInLine(1, 3), _textView.GetCaretPoint());
                _vimBuffer.Process('k');
                Assert.Equal(_textView.GetPointInLine(0, 3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the caret column is properly maintained when we have to account for mixed
            /// tabs and spaces on the preceeding line
            /// </summary>
            [Fact]
            public void MixedTabsAndSpaces()
            {
                Create("    alpha", "\tbrought", "tac", "    dog");
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(4);
                foreach (var c in "abcd")
                {
                    Assert.Equal(c.ToString(), _textView.GetCaretPoint().GetChar().ToString());
                    _vimBuffer.Process('j');
                }
            }

            /// <summary>
            /// When spaces don't divide evenly into tabs the transition into a tab
            /// should land on the tab
            /// </summary>
            [Fact]
            public void SpacesDoNotDivideToTabs()
            {
                Create("    alpha", "\tbrought", "cat");
                _localSettings.TabStop = 4;
                _textView.MoveCaretTo(2);
                Assert.Equal(' ', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process('j');
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.Process('j');
                Assert.Equal(_textBuffer.GetPointInLine(2, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When spaces overlap a tab stop length we need to modulus and apply the 
            /// remaining spaces
            /// </summary>
            [Fact]
            public void SpacesOverlapTabs()
            {
                Create("    alpha", "\tbrought", "cat");
                _localSettings.TabStop = 2;
                _textView.MoveCaretTo(4);
                Assert.Equal('a', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process('j');
                Assert.Equal(_textBuffer.GetPointInLine(1, 3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When using the end of line motion the caret should maintain a relative end of
            /// line position instead of a fixed position at the current line
            /// </summary>
            [Fact]
            public void EndOfLineMotionDown()
            {
                Create("cat", "tree");
                _vimBuffer.ProcessNotation("$");
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
                _vimBuffer.ProcessNotation("j");
                Assert.Equal(3, _textView.GetCaretPoint().GetColumn().Column);
            }

            [Fact]
            public void EndOfLineMotionUp()
            {
                Create("tree", "cat");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("$");
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
                _vimBuffer.ProcessNotation("k");
                Assert.Equal(3, _textView.GetCaretPoint().GetColumn().Column);
            }

            /// <summary>
            /// The ve=onemore setting shouldn't play a role here.  The $ motion won't go past
            /// the end of the line even if ve=onemore and the movement down and up shoudn't affect
            /// that
            /// </summary>
            [Fact]
            public void EndOfLineMotionWithVirtualEditOneMore()
            {
                Create("cat", "tree");
                _globalSettings.VirtualEdit = "onemore";
                Assert.True(_globalSettings.IsVirtualEditOneMore);
                _vimBuffer.ProcessNotation("$");
                Assert.Equal(2, _textView.GetCaretPoint().GetColumn().Column);
                _vimBuffer.ProcessNotation("j");
                Assert.Equal(3, _textView.GetCaretPoint().GetColumn().Column);
            }
        }

        public sealed class ChangeCaseMotionTest : NormalModeIntegrationTest
        {
            [Fact]
            public void UpperOverWord()
            {
                Create("cat dog");
                _vimBuffer.Process("gUw");
                Assert.Equal("CAT dog", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void LowerOverWord()
            {
                Create("CAT dog");
                _vimBuffer.Process("guw");
                Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void Rot13OverWord()
            {
                Create("cat dog");
                _vimBuffer.Process("g?w");
                Assert.Equal("png dog", _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class PutAfterTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// When pasting from the clipboard where the text doesn't end in a new line it
            /// should be treated as characterwise paste
            /// </summary>
            [Fact]
            public void ClipboardWithoutNewLine()
            {
                Create("hello world", "again");
                _textView.MoveCaretTo(5);
                _clipboardDevice.Text = "big ";
                _vimBuffer.Process("\"+p");
                Assert.Equal("hello big world", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When pasting from the clipboard where the text does end in a new line it 
            /// should be treated as a linewise paste
            /// </summary>
            [Fact]
            public void ClipboardWithNewLine()
            {
                Create("hello world", "again");
                _textView.MoveCaretTo(5);
                _clipboardDevice.Text = "big " + Environment.NewLine;
                _vimBuffer.Process("\"+p");
                Assert.Equal("hello world", _textView.GetLine(0).GetText());
                Assert.Equal("big ", _textView.GetLine(1).GetText());
                Assert.Equal("again", _textView.GetLine(2).GetText());
            }

            /// <summary>
            /// A putafter at the end of the line should still put the text after the caret
            /// </summary>
            [Fact]
            public void EndOfLine()
            {
                Create("dog");
                _textView.MoveCaretTo(2);
                Assert.Equal('g', _textView.GetCaretPoint().GetChar());
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process('p');
                Assert.Equal("dogcat", _textView.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// A putafter on an empty line is the only thing that shouldn't move the caret
            /// </summary>
            [Fact]
            public void EmptyLine()
            {
                Create("");
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process('p');
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned at the start of the inserted line
            /// </summary>
            [Fact]
            public void LineWiseSimpleString()
            {
                Create("dog", "cat", "bear", "tree");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("pig", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetCaretPoint(), _textView.GetLine(1).Start);
            }

            /// <summary>
            /// Caret should be positioned at the start of the indent even when autoindent is off
            /// </summary>
            [Fact]
            public void LineWiseWithIndent()
            {
                Create("dog", "cat", "bear", "tree");
                UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
                _vimBuffer.LocalSettings.AutoIndent = false;
                _vimBuffer.Process("p");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("  pig", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetCaretPoint(), _textView.GetLine(1).Start.Add(2));
            }

            /// <summary>
            /// Caret should be positioned on the last character of the inserted text
            /// </summary>
            [Fact]
            public void CharacterWiseSimpleString()
            {
                Create("dog", "cat", "bear", "tree");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("dpigog", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When putting a character wise selection which spans over multiple lines into 
            /// the ITextBuffer the caret is positioned at the start of the text and not 
            /// after it as it is with most put operations
            /// </summary>
            [Fact]
            public void CharacterWise_MultipleLines()
            {
                Create("dog", "cat");
                UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be");
                _vimBuffer.Process("p");
                Assert.Equal("dtree", _textView.GetLine(0).GetText());
                Assert.Equal("beog", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned after the last character of the inserted text
            /// </summary>
            [Fact]
            public void CharacterWiseSimpleString_WithCaretMove()
            {
                Create("dog", "cat", "bear", "tree");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dpigog", _textView.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The caret should be positioned at the last character of the first block string
            /// inserted text
            /// </summary>
            [Fact]
            public void BlockOverExisting()
            {
                Create("dog", "cat", "bear", "tree");
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("daaog", _textView.GetLine(0).GetText());
                Assert.Equal("cbbat", _textView.GetLine(1).GetText());
                Assert.Equal("bear", _textView.GetLine(2).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The new text should be on new lines at the same indetn and the caret posion should
            /// be the same as puting over existing lines
            /// </summary>
            [Fact]
            public void BlockOnNewLines()
            {
                Create("dog");
                _textView.MoveCaretTo(1);
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("doaag", _textView.GetLine(0).GetText());
                Assert.Equal("  bb", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This should cause the cursor to be put on the first line after the inserted 
            /// lines
            /// </summary>
            [Fact]
            public void LineWise_WithCaretMove()
            {
                Create("dog", "cat");
                UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("pig", _textView.GetLine(1).GetText());
                Assert.Equal("tree", _textView.GetLine(2).GetText());
                Assert.Equal("cat", _textView.GetLine(3).GetText());
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Putting a word which doesn't span multiple lines with indent is simply no 
            /// different than a typically put after command
            /// </summary>
            [Fact]
            public void WithIndent_Word()
            {
                Create("  dog", "  cat", "fish", "tree");
                UnnamedRegister.UpdateValue("bear", OperationKind.CharacterWise);
                _vimBuffer.Process("]p");
                Assert.Equal(" bear dog", _textView.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Putting a line should cause the indent to be matched in the second line irrespective
            /// of what the original indent was
            /// </summary>
            [Fact]
            public void WithIndent_SingleLine()
            {
                Create("  dog", "  cat", "fish", "tree");
                UnnamedRegister.UpdateValue("bear" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.Process("]p");
                Assert.Equal("  dog", _textView.GetLine(0).GetText());
                Assert.Equal("  bear", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Putting a line should cause the indent to be matched in all of the pasted lines 
            /// irrespective of their original indent
            /// </summary>
            [Fact]
            public void WithIndent_MultipleLines()
            {
                Create("  dog", "  cat");
                UnnamedRegister.UpdateValue("    tree" + Environment.NewLine + "    bear" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.Process("]p");
                Assert.Equal("  dog", _textView.GetLine(0).GetText());
                Assert.Equal("  tree", _textView.GetLine(1).GetText());
                Assert.Equal("  bear", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Putting a character wise block of text which spans multiple lines is the trickiest
            /// version.  It requires that the first line remain unchanged while the subsequent lines
            /// are indeed indented to the proper level
            /// </summary>
            [Fact]
            public void WithIndent_CharcterWiseOverSeveralLines()
            {
                Create("  dog", "  cat");
                UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be", OperationKind.CharacterWise);
                _vimBuffer.Process("]p");
                Assert.Equal(" tree", _textView.GetLine(0).GetText());
                Assert.Equal("  be dog", _textView.GetLine(1).GetText());
                Assert.Equal("  cat", _textView.GetLine(2).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The :put command should normalize the line endings during a put operation
            /// </summary>
            [Fact]
            public void NormalizeLineEndingLinewise()
            {
                Create("tree", "pet");
                UnnamedRegister.UpdateValue("cat\ndog\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal(
                    new[] { "tree", "cat", "dog", "pet" },
                    _textBuffer.GetLines());
                Assert.Equal(Environment.NewLine, _textBuffer.GetLine(1).GetLineBreakText());
            }

            /// <summary>
            /// The :put command should normalize the line endings during a put operation
            /// </summary>
            [Fact]
            public void NormalizeLineEndingCharacterwise()
            {
                Create("tree", "pet");
                UnnamedRegister.UpdateValue("cat\ndog", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal(
                    new[] { "tcat", "dogree", "pet" },
                    _textBuffer.GetLines());
                Assert.Equal(Environment.NewLine, _textBuffer.GetLine(0).GetLineBreakText());
                Assert.Equal(Environment.NewLine, _textBuffer.GetLine(1).GetLineBreakText());
            }

            /// <summary>
            /// When pasting the last line with a count we want to make sure that we add in the new 
            /// line for every paste
            /// </summary>
            [Fact]
            public void PutLastLineWithCount()
            {
                Create("cat");
                _vimBuffer.Process("yy2p");
                Assert.Equal(new[] { "cat", "cat", "cat" }, _textBuffer.GetLines());
            }

            [Fact]
            public void InVirtualSpaceCharacterWise()
            {
                Create("big", "tree");
                UnnamedRegister.UpdateValue("ger");
                _globalSettings.VirtualEdit = "onemore";
                _vimBuffer.Process("lllp");
                Assert.Equal(_textBuffer.GetLines(), new[] { "bigger", "tree" });
            }

            [Fact]
            public void InVirtualSpaceCharacterWiseWithNewLine()
            {
                Create("big", "tree");
                UnnamedRegister.UpdateValue("ger" + Environment.NewLine);
                _globalSettings.VirtualEdit = "onemore";
                _vimBuffer.Process("lllp");
                Assert.Equal(_textBuffer.GetLines(), new[] { "bigger", "", "tree" });
                Assert.True(_textBuffer.CurrentSnapshot.Lines.Take(2).All(x => x.GetLineBreakText() == Environment.NewLine));
            }

            [Fact]
            public void Issue1185()
            {
                Create("cat", "dog", "fish");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yyklllllp");
                Assert.Equal(
                    _textBuffer.GetLines(),
                    new[] { "cat", "dog", "dog", "fish" });
                Assert.True(_textBuffer.CurrentSnapshot.Lines.Take(3).All(x => x.GetLineBreakText() == Environment.NewLine));
            }
        }

        public sealed class PutBeforeTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Caret should be at the start of the inserted text
            /// </summary>
            [Fact]
            public void LineWiseStartOfBuffer()
            {
                Create("dog");
                UnnamedRegister.UpdateValue("pig\n", OperationKind.LineWise);
                _vimBuffer.Process("P");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Caret should be positioned at the start of the indented text
            /// </summary>
            [Fact]
            public void LineWiseStartOfBufferWithIndent()
            {
                Create("dog");
                UnnamedRegister.UpdateValue("  pig\n", OperationKind.LineWise);
                _vimBuffer.Process("P");
                Assert.Equal("  pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be on the first line of the newly inserted lines
            /// </summary>
            [Fact]
            public void LineWiseMiddleOfBuffer()
            {
                Create("dog", "cat");
                _textView.MoveCaretToLine(1);
                UnnamedRegister.UpdateValue("fish\ntree\n", OperationKind.LineWise);
                _vimBuffer.Process("P");
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("fish", _textView.GetLine(1).GetText());
                Assert.Equal("tree", _textView.GetLine(2).GetText());
                Assert.Equal("cat", _textView.GetLine(3).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be on the first line after the inserted lines
            /// </summary>
            [Fact]
            public void LineWise_WithCaretMove()
            {
                Create("dog", "cat");
                UnnamedRegister.UpdateValue("pig\ntree\n", OperationKind.LineWise);
                _vimBuffer.Process("gP");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("tree", _textView.GetLine(1).GetText());
                Assert.Equal("dog", _textView.GetLine(2).GetText());
                Assert.Equal("cat", _textView.GetLine(3).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void CharacterWiseBlockStringOnExistingLines()
            {
                Create("dog", "cat", "bear", "tree");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateBlockValues("a", "b", "c");
                _vimBuffer.Process("P");
                Assert.Equal("adog", _textView.GetLine(0).GetText());
                Assert.Equal("bcat", _textView.GetLine(1).GetText());
                Assert.Equal("cbear", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetCaretPoint(), _textView.GetLine(0).Start);
            }

            /// <summary>
            /// Putting a word which doesn't span multiple lines with indent is simply no 
            /// different than a typically put after command
            /// </summary>
            [Fact]
            public void WithIndent_Word()
            {
                Create("  dog", "  cat", "fish", "tree");
                UnnamedRegister.UpdateValue("bear", OperationKind.CharacterWise);
                _vimBuffer.Process("[p");
                Assert.Equal("bear  dog", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Putting a line should cause the indent to be matched in the second line irrespective
            /// of what the original indent was
            /// </summary>
            [Fact]
            public void WithIndent_SingleLine()
            {
                Create("  dog", "  cat", "fish", "tree");
                UnnamedRegister.UpdateValue("bear" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.Process("[p");
                Assert.Equal("  bear", _textView.GetLine(0).GetText());
                Assert.Equal("  dog", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPointInLine(0, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Putting a line should cause the indent to be matched in all of the pasted lines 
            /// irrespective of their original indent
            /// </summary>
            [Fact]
            public void WithIndent_MultipleLines()
            {
                Create("  dog", "  cat");
                UnnamedRegister.UpdateValue("    tree" + Environment.NewLine + "    bear" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.Process("[p");
                Assert.Equal("  tree", _textView.GetLine(0).GetText());
                Assert.Equal("  bear", _textView.GetLine(1).GetText());
                Assert.Equal("  dog", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetPointInLine(0, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Putting a character wise block of text which spans multiple lines is the trickiest
            /// version.  It requires that the first line remain unchanged while the subsequent lines
            /// are indeed indented to the proper level
            /// </summary>
            [Fact]
            public void WithIndent_CharcterWiseOverSeveralLines()
            {
                Create("  dog", "  cat");
                UnnamedRegister.UpdateValue("tree" + Environment.NewLine + "be", OperationKind.CharacterWise);
                _vimBuffer.Process("[p");
                Assert.Equal("tree", _textView.GetLine(0).GetText());
                Assert.Equal("  be  dog", _textView.GetLine(1).GetText());
                Assert.Equal("  cat", _textView.GetLine(2).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class QuotedStringTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// When the ' motion starts on a quote then vim should look at the entire
            /// line to see if it's the trailing or leading quote
            /// </summary>
            [Fact]
            public void StartOnLeadingQuote()
            {
                Create("'cat', 'dog', 'fish'");
                _textView.MoveCaretTo(7);
                _vimBuffer.Process("di'");
                Assert.Equal("'cat', '', 'fish'", _textBuffer.GetLine(0).GetText());
                Assert.Equal(8, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The same is true if we start on the trailing quote
            /// </summary>
            [Fact]
            public void StartOnTrailingQuote()
            {
                Create("'cat', 'dog', 'fish'");
                _textView.MoveCaretTo(11);
                _vimBuffer.Process("di'");
                Assert.Equal("'cat', '', 'fish'", _textBuffer.GetLine(0).GetText());
                Assert.Equal(8, _textView.GetCaretPoint());
            }

            /// <summary>
            /// If we aren't starting on a quote then we simply don't consider the entire line
            /// and just look for the previous quote
            /// </summary>
            [Fact]
            public void StartInBetweenQuotes()
            {
                Create("'cat', 'dog', 'fish'");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("di'");
                Assert.Equal("'cat''dog', 'fish'", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint());
            }

            [Fact]
            public void BeforeFirstQuote()
            {
                Create("cat 'dog'");
                _vimBuffer.Process("di'");
                Assert.Equal("cat ''", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint());
            }
        }

        public sealed class RepeatCommandTest : NormalModeIntegrationTest
        {
            [Fact]
            public void DeleteWord1()
            {
                Create("the cat jumped over the dog");
                _vimBuffer.Process("dw");
                _vimBuffer.Process(".");
                Assert.Equal("jumped over the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that movement doesn't reset the last edit command
            /// </summary>
            [Fact]
            public void DeleteWord2()
            {
                Create("the cat jumped over the dog");
                _vimBuffer.Process("dw");
                _vimBuffer.Process(VimKey.Right);
                _vimBuffer.Process(VimKey.Left);
                _vimBuffer.Process(".");
                Assert.Equal("jumped over the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// "Delete word with a count
            /// </summary>
            [Fact]
            public void DeleteWord3()
            {
                Create("the cat jumped over the dog");
                _vimBuffer.Process("2dw");
                _vimBuffer.Process(".");
                Assert.Equal("the dog", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void DeleteLine1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("dd");
                _vimBuffer.Process(".");
                Assert.Equal("cat", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void DeleteLine2()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("2dd");
                _vimBuffer.Process(".");
                Assert.Equal("fox", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Repeating a replace char command should move the caret to the end just like
            /// the original command did
            /// </summary>
            [Fact]
            public void ReplaceCharShouldMoveCaret()
            {
                Create("the dog kicked the ball");
                _vimBuffer.Process("3ru");
                Assert.Equal("uuu dog kicked the ball", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(".");
                Assert.Equal("uuu uuu kicked the ball", _textView.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ShiftLeft1()
            {
                Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
                _vimBuffer.LocalSettings.ShiftWidth = 1;
                _vimBuffer.Process("<<");
                _vimBuffer.Process(".");
                Assert.Equal("  bear", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void ShiftLeft2()
            {
                Create("    bear", "    dog", "    cat", "    zebra", "    fox", "    jazz");
                _vimBuffer.LocalSettings.ShiftWidth = 1;
                _vimBuffer.Process("2<<");
                _vimBuffer.Process(".");
                Assert.Equal("  bear", _textView.GetLine(0).GetText());
                Assert.Equal("  dog", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void ShiftRight1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.LocalSettings.ShiftWidth = 1;
                _vimBuffer.Process(">>");
                _vimBuffer.Process(".");
                Assert.Equal("  bear", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void ShiftRight2()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.LocalSettings.ShiftWidth = 1;
                _vimBuffer.Process("2>>");
                _vimBuffer.Process(".");
                Assert.Equal("  bear", _textView.GetLine(0).GetText());
                Assert.Equal("  dog", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void DeleteChar1()
            {
                Create("longer");
                _vimBuffer.Process("x");
                _vimBuffer.Process(".");
                Assert.Equal("nger", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void DeleteChar2()
            {
                Create("longer");
                _vimBuffer.Process("2x");
                _vimBuffer.Process(".");
                Assert.Equal("er", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// After a search operation
            /// </summary>
            [Fact]
            public void DeleteChar3()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("/e", enter: true);
                _vimBuffer.Process("x");
                _vimBuffer.Process("n");
                _vimBuffer.Process(".");
                Assert.Equal("bar", _textView.GetLine(0).GetText());
                Assert.Equal("zbra", _textView.GetLine(3).GetText());
            }

            [Fact]
            public void Put1()
            {
                Create("cat");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
                _vimBuffer.Process("p");
                _vimBuffer.Process(".");
                Assert.Equal("cloloat", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void Put2()
            {
                Create("cat");
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("lo");
                _vimBuffer.Process("2p");
                _vimBuffer.Process(".");
                Assert.Equal("clolololoat", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void JoinLines1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("J");
                _vimBuffer.Process(".");
                Assert.Equal("bear dog cat", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void Change1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("cl");
                _vimBuffer.Process(VimKey.Delete);
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _vimBuffer.Process(VimKey.Down);
                _vimBuffer.Process(".");
                Assert.Equal("ar", _textView.GetLine(0).GetText());
                Assert.Equal("g", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void Change2()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("cl");
                _vimBuffer.Process("u");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _vimBuffer.Process(VimKey.Down);
                _vimBuffer.Process(".");
                Assert.Equal("uear", _textView.GetLine(0).GetText());
                Assert.Equal("uog", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void Substitute1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("s");
                _vimBuffer.Process("u");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _vimBuffer.Process(VimKey.Down);
                _vimBuffer.Process(".");
                Assert.Equal("uear", _textView.GetLine(0).GetText());
                Assert.Equal("uog", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void Substitute2()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("s");
                _vimBuffer.Process("u");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _vimBuffer.Process(VimKey.Down);
                _vimBuffer.Process("2.");
                Assert.Equal("uear", _textView.GetLine(0).GetText());
                Assert.Equal("ug", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void TextInsert1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("i");
                _vimBuffer.Process("abc");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                _vimBuffer.Process(".");
                Assert.Equal("ababccbear", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void TextInsert2()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("i");
                _vimBuffer.Process("abc");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                Assert.Equal("abcabcbear", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void TextInsert3()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("i");
                _vimBuffer.Process("abc");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process(".");
                _vimBuffer.Process(".");
                Assert.Equal("ababccabcbear", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Test the repeating of a command that changes white space to tabs
            /// </summary>
            [Fact]
            public void TextInsert_WhiteSpaceToTab()
            {
                Create("    hello world", "dog");
                _vimBuffer.LocalSettings.TabStop = 4;
                _vimBuffer.LocalSettings.ExpandTab = false;
                _vimBuffer.Process('i');
                _textBuffer.Replace(new Span(0, 4), "\t\t");
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("\tdog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// The first repeat of I should go to the first non-blank
            /// </summary>
            [Fact]
            public void CapitalI1()
            {
                Create("bear", "dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("I");
                _vimBuffer.Process("abc");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
                _vimBuffer.Process(".");
                Assert.Equal("abcdog", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The first repeat of I should go to the first non-blank
            /// </summary>
            [Fact]
            public void CapitalI2()
            {
                Create("bear", "  dog", "cat", "zebra", "fox", "jazz");
                _vimBuffer.Process("I");
                _vimBuffer.Process("abc");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(2));
                _vimBuffer.Process(".");
                Assert.Equal("  abcdog", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(4), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Repeating a 
            /// replace char command from visual mode should not move the caret
            /// </summary>
            [Fact]
            public void ReplaceCharVisual_ShouldNotMoveCaret()
            {
                Create("the dog kicked the ball");
                _vimBuffer.VimData.LastCommand = FSharpOption.Create(StoredCommand.NewVisualCommand(
                    VisualCommand.NewReplaceSelection(KeyInputUtil.CharToKeyInput('b')),
                    VimUtil.CreateCommandData(),
                    StoredVisualSpan.OfVisualSpan(VimUtil.CreateVisualSpanCharacter(_textView.GetLineSpan(0, 3))),
                    CommandFlags.None));
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(".");
                Assert.Equal("tbbbdog kicked the ball", _textView.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure the caret movement occurs as part of the repeat
            /// </summary>
            [Fact]
            public void AppendShouldRepeat()
            {
                Create("{", "}");
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process('a');
                _vimBuffer.Process(';');
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("};", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure the caret movement occurs as part of the repeat
            /// </summary>
            [Fact]
            public void AppendEndOfLineShouldRepeat()
            {
                Create("{", "}");
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process('A');
                _vimBuffer.Process(';');
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal("};", _textView.GetLine(1).GetText());
            }
            /// <summary>
            /// The insert line above command should be linked the the following text change
            /// </summary>
            [Fact]
            public void InsertLineAbove()
            {
                Create("cat", "dog", "tree");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("O  fish");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("  fish", _textView.GetLine(2).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(".");
                Assert.Equal("  fish", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// The insert line below command should be linked the the following text change
            /// </summary>
            [Fact]
            public void InsertLineBelow()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("o  fish");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("  fish", _textView.GetLine(1).GetText());
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process(".");
                Assert.Equal("  fish", _textView.GetLine(3).GetText());
            }

            /// <summary>
            /// The 'o' command used to have a bug which occured when 
            ///
            ///  - Insert mode made no edits
            ///  - The 'o' command put the caret into virtual space
            ///
            /// In that case the next edit command would link with the insert line below 
            /// change in the repeat infrastructure.  Normally the move caret left
            /// operation processed on Escape moved the caret and ended a repeat.  But
            /// the move left from virtual space didn't use a proper command and 
            /// caused repeat to remain open
            /// 
            /// Regression Test for Issue #748
            /// </summary>
            [Fact]
            public void InsertLineBelow_ToVirtualSpace()
            {
                Create("cat", "dog");
                _vimBuffer.Process('o');
                _textView.MoveCaretTo(_textView.GetCaretPoint().Position, 4);
                _vimBuffer.Process(VimKey.Escape);
                _textView.MoveCaretTo(0);
                _vimBuffer.ProcessNotation("cwbear<Esc>");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process('.');
                Assert.Equal("bear", _textBuffer.GetLine(2).GetText());
            }

            [Fact]
            public void DeleteWithIncrementalSearch()
            {
                Create("dog cat bear tree");
                _vimBuffer.Process("d/a", enter: true);
                _vimBuffer.Process('.');
                Assert.Equal("ar tree", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Test the repeat of a repeated command.  Essentially ensure the act of repeating doesn't
            /// disturb the cached LastCommand value
            /// </summary>
            [Fact]
            public void Repeated()
            {
                Create("the fox chased the bird");
                _vimBuffer.Process("dw");
                Assert.Equal("fox chased the bird", _textView.TextSnapshot.GetText());
                _vimBuffer.Process(".");
                Assert.Equal("chased the bird", _textView.TextSnapshot.GetText());
                _vimBuffer.Process(".");
                Assert.Equal("the bird", _textView.TextSnapshot.GetText());
            }

            [Fact]
            public void LinkedTextChange1()
            {
                Create("the fox chased the bird");
                _vimBuffer.Process("cw");
                _vimBuffer.Process("hey ");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
                Assert.Equal("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
            }

            [Fact]
            public void LinkedTextChange2()
            {
                Create("the fox chased the bird");
                _vimBuffer.Process("cw");
                _vimBuffer.Process("hey");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
                Assert.Equal("hey hey chased the bird", _textView.TextSnapshot.GetText());
            }

            [Fact]
            public void LinkedTextChange3()
            {
                Create("the fox chased the bird");
                _vimBuffer.Process("cw");
                _vimBuffer.Process("hey");
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
                Assert.Equal("hey hehey chased the bird", _textView.TextSnapshot.GetText());
            }

            /// <summary>
            /// Make sure the undo layer doesn't flag an empty repeat as an error.  It is always
            /// possible for a repeat to fail 
            /// </summary>
            [Fact]
            public void EmptyCommand()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ctablah<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation(".");
            }
        }

        public sealed class ReplaceCharTest : NormalModeIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat dog");
                _vimBuffer.Process("rb");
                Assert.Equal("bat dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When there is a count involved the caret should be positioned on the final character
            /// that is relpaced
            /// </summary>
            [Fact]
            public void WithCount()
            {
                Create("cat dog");
                _vimBuffer.Process("3ro");
                Assert.Equal("ooo dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When the count exceeds the length of the line then no change should occur and a beep
            /// should be raised
            /// </summary>
            [Fact]
            public void CountTooBig()
            {
                Create("cat", "dog fish tree");
                _vimBuffer.Process("10ro");
                Assert.Equal(1, VimHost.BeepCount);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// There is no known extension which does this yet but it is possible for a subsequent change
            /// to delete the buffer up to the position that we want to move the caret to.  Must account
            /// for that by simply not crashing.  Move to the best possible position 
            /// </summary>
            [Fact]
            public void AfterChangeDeleteTargetCaretPosition()
            {
                Create("cat dog");
                _textView.MoveCaretTo(5);
                var first = true;
                _textBuffer.Changed +=
                    delegate
                    {
                        if (first)
                        {
                            first = false;
                            _textBuffer.Replace(new Span(0, 7), "at");
                        }
                    };
                _vimBuffer.Process("r2");
                Assert.Equal("at", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            /// <summary>
            /// In Vs 2012 if you change a tag name with a simple relpace it will change the matching
            /// tag with a subsequent edit.  This means the ITextSnapshot which returns from the replace
            /// will be a version behind the current version of the ITextView.  Need to make sure we
            /// use the correct ITextSnapshot for the caret positioning
            /// </summary>
            [Fact]
            public void Issue1040()
            {
                Create("<h1>test</h1>");
                _textView.MoveCaretTo(2);
                var first = true;
                _textBuffer.Changed +=
                    delegate
                    {
                        if (first)
                        {
                            first = false;
                            _textBuffer.Replace(new Span(11, 1), "2");
                        }
                    };
                _vimBuffer.Process("r2");
                Assert.Equal("<h2>test</h2>", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When the replace character is a new line we need to respect the line ending of the current
            /// line when inserting the text
            /// </summary>
            [Fact]
            public void Issue1198()
            {
                Create("");
                _textBuffer.SetText("cat\ndog");
                _textView.MoveCaretTo(0);
                Assert.Equal("\n", _textBuffer.GetLine(0).GetLineBreakText());
                _vimBuffer.ProcessNotation("r<Enter>");
                Assert.Equal("\n", _textBuffer.GetLine(0).GetLineBreakText());
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
            }
        }

        public abstract class ScrollWindowTest : NormalModeIntegrationTest
        {
            private static readonly string[] s_lines = KeyInputUtilTest.CharLettersLower.Select(x => x.ToString()).ToArray();
            private readonly int _lastLineNumber = 0;

            protected ScrollWindowTest()
            {
                Create(s_lines);
                _lastLineNumber = _textBuffer.CurrentSnapshot.LineCount - 1;
                _textView.SetVisibleLineCount(5);
                _globalSettings.ScrollOffset = 1;
                _windowSettings.Scroll = 1;
            }

            public sealed class WindowAndCaretTest : ScrollWindowTest
            {
                [Fact]
                public void UpMovesCaret()
                {
                    _textView.DisplayTextLineContainingBufferPosition(_textBuffer.GetLine(1).Start, 0.0, ViewRelativePosition.Top);
                    _textView.MoveCaretToLine(2);
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                    Assert.Equal(0, _textView.GetFirstVisibleLineNumber());
                }

                /// <summary>
                /// The caret should move in this case even if the window itself doesn't scroll
                /// </summary>
                [Fact]
                public void UpMovesCaretWithoutScroll()
                {
                    _textView.MoveCaretToLine(2);
                    _vimBuffer.ProcessNotation("<C-u>");
                    Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                }
            }

            public sealed class WindowOnlyTest : ScrollWindowTest
            {
                public WindowOnlyTest()
                {
                    _globalSettings.ScrollOffset = 0;
                }

                [Fact]
                public void UpDoesNotMoveCaret()
                {
                    _textView.DisplayTextLineContainingBufferPosition(_textBuffer.GetLine(1).Start, 0.0, ViewRelativePosition.Top);
                    _textView.MoveCaretToLine(2);
                    _vimBuffer.ProcessNotation("<C-y>");
                    Assert.Equal(2, _textView.GetCaretLine().LineNumber);
                }

                [Fact]
                public void DownDoesNotMoveCaret()
                {
                    _textView.MoveCaretToLine(2);
                    _vimBuffer.ProcessNotation("<C-e>");
                    Assert.Equal(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
                }

                /// <summary>
                /// When the caret moves off the visible screen we move it to be visible
                /// </summary>
                [Fact]
                public void DownMovesCaretWhenNotVisible()
                {
                    _textView.MoveCaretToLine(0);
                    _vimBuffer.ProcessNotation("<C-e>");
                    Assert.Equal(_textView.GetFirstVisibleLineNumber(), _textView.GetCaretLine().LineNumber);
                }

                /// <summary>
                /// As the lines move off of the screen the caret is moved down to keep it visible.  Make sure 
                /// that we maintain the caret column in these cases 
                /// </summary>
                [Fact]
                public void DownMaintainCaretColumn()
                {
                    _textBuffer.SetText("cat", "", "dog", "a", "b", "c", "d", "e");
                    _textView.MoveCaretToLine(0, 1);
                    _vimBuffer.ProcessNotation("<C-e>");
                    Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
                    _vimBuffer.ProcessNotation("<C-e>");
                    Assert.Equal(_textBuffer.GetPointInLine(2, 1), _textView.GetCaretPoint());
                }

                /// <summary>
                /// When the scroll is to the top of the ITextBuffer the Ctrl-Y command should not cause the 
                /// caret to move.  It should only move as the result of the window scrolling the caret out of
                /// the view 
                /// </summary>
                [Fact]
                public void Issue1202()
                {
                    _textView.MoveCaretToLine(1);
                    _vimBuffer.ProcessNotation("<C-y>");
                    Assert.Equal(1, _textView.GetCaretLine().LineNumber);
                }

                [Fact]
                public void Issue1637()
                {
                    _textBuffer.SetText("abcdefghi".Select(x => x.ToString()).ToArray());
                    _globalSettings.ScrollOffset = 1;
                    _vimBuffer.ProcessNotation("<C-e>");
                    Assert.Equal("c", _textView.GetCaretLine().GetText());
                    Assert.Equal(1, _textView.GetFirstVisibleLineNumber());
                }
            }
        }

        public sealed class ScrollOffsetTest : NormalModeIntegrationTest
        {
            private static readonly string[] s_lines = KeyInputUtilTest.CharLettersLower.Select(x => x.ToString()).ToArray();
            private readonly int _lastLineNumber = 0;

            public ScrollOffsetTest()
            {
                Create(s_lines);
                _lastLineNumber = _textBuffer.CurrentSnapshot.LineCount - 1;
                _textView.SetVisibleLineCount(5);
                _globalSettings.ScrollOffset = 2;
            }

            private void AssertFirstLine(int lineNumber)
            {
                var actual = _textView.GetFirstVisibleLineNumber();
                Assert.Equal(lineNumber, actual);
            }

            private void AssertLastLine(int lineNumber)
            {
                var actual = _textView.GetLastVisibleLineNumber();
                Assert.Equal(lineNumber, actual);
            }

            [Fact]
            public void SimpleMoveDown()
            {
                _textView.ScrollToTop();
                AssertLastLine(4);
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("j");
                AssertLastLine(5);
            }

            [Fact]
            public void SimpleMoveUp()
            {
                var lineNumber = 20;
                _textView.DisplayTextLineContainingBufferPosition(_textBuffer.GetLine(lineNumber).Start, 0.0, ViewRelativePosition.Top);
                _textView.MoveCaretToLine(lineNumber + 2);
                AssertFirstLine(lineNumber);
                _vimBuffer.ProcessNotation("k");
                AssertFirstLine(lineNumber - 1);
            }

            /// <summary>
            /// During an incremental search even though the caret doesn't move we should still position
            /// the scroll as if the caret was at the found search point 
            /// </summary>
            [Fact]
            public void IncrementalSearchForward()
            {
                _globalSettings.IncrementalSearch = true;
                _textView.ScrollToTop();
                _textView.MoveCaretToLine(0);
                _vimBuffer.ProcessNotation("/g");
                AssertLastLine(8);
            }

            [Fact]
            public void ScrollDownLines()
            {
                _textView.ScrollToTop();
                _textView.MoveCaretToLine(2);
                _windowSettings.Scroll = 2;
                _vimBuffer.ProcessNotation("<c-d>");
                AssertFirstLine(2);
                Assert.Equal(4, _textView.GetCaretLine().LineNumber);
            }

            [Fact]
            public void ScrollUpLines()
            {
                var lineNumber = 16;
                _textView.DisplayTextLineContainingBufferPosition(_textBuffer.GetLine(lineNumber).Start, 0.0, ViewRelativePosition.Top);
                _textView.MoveCaretToLine(lineNumber + 2);
                _windowSettings.Scroll = 2;
                AssertFirstLine(lineNumber);
                _vimBuffer.ProcessNotation("<c-u>");
                AssertFirstLine(lineNumber - 2);
                Assert.Equal(lineNumber, _textView.GetCaretLine().LineNumber);
            }

            [Fact]
            public void ScrollLineToTop()
            {
                var lineNumber = 4;
                _textView.SetVisibleLineCount(10);
                _textView.DisplayTextLineContainingBufferPosition(_textBuffer.GetLine(lineNumber).Start, 0.0, ViewRelativePosition.Top);
                _textView.MoveCaretToLine(lineNumber + 4);
                _vimBuffer.ProcessNotation("zt");
                AssertFirstLine(lineNumber + 2);
            }

            /// <summary>
            /// The simple act of moving the caret outside of the context of a vim command shousd cause the scroll 
            /// offset to be respected 
            /// </summary>
            [Fact]
            public void CaretMove()
            {
                _textView.ScrollToTop();
                _textView.MoveCaretToLine(4);
                AssertFirstLine(2);
            }
        }

        public sealed class SmallDeleteTest : NormalModeIntegrationTest
        {
            [Fact]
            public void DeleteThenPaste()
            {
                Create("dog cat");
                _vimBuffer.ProcessNotation(@"x""-p");
                Assert.Equal("odg cat", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Deleting a line shouldn't effect the small delete register
            /// </summary>
            [Fact]
            public void DeleteLine()
            {
                Create("dog", "cat", "tree");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("x");
                Assert.Equal("t", RegisterMap.GetRegister(RegisterName.SmallDelete).StringValue);
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process(@"dd""-p");
                Assert.Equal("ctat", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// As long as the delete is less than one line then the delete should 
            /// update the small delete register
            /// </summary>
            [Fact]
            public void DeleteMoreThanOneCharacter()
            {
                Create("dog", "cat");
                _vimBuffer.ProcessNotation(@"2x""-p");
                Assert.Equal("gdo", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Deleting multiple lines but not ending on a new line should still not update the
            /// small delete register
            /// </summary>
            [Fact]
            public void DeleteLineDoesntEndInNewLine()
            {
                Create("dog", "cat", "tree");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("x");
                Assert.Equal("t", RegisterMap.GetRegister(RegisterName.SmallDelete).StringValue);
                _textView.MoveCaretToLine(0);
                _vimBuffer.Process(@"vjx""-p");
                Assert.Equal("att", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// When the delete occurs into a named register then we should not update the small
            /// delete register
            /// </summary>
            [Fact]
            public void IgnoreNamedRegister()
            {
                Create("dog");
                RegisterMap.GetRegister(RegisterName.SmallDelete).UpdateValue("t");
                _vimBuffer.Process(@"""cx""-p");
                Assert.Equal("otg", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// The search motion should always cause the 1-9 registers to be updated irrespective of the text of the 
            /// delete.
            /// </summary>
            [Fact]
            public void DeleteSmallWithSearchMotion()
            {
                Create("dog");
                RegisterMap.GetRegister(1).UpdateValue("g");
                _vimBuffer.Process(@"d/o", enter: true);
                Assert.Equal("d", RegisterMap.GetRegister(1).StringValue);
                Assert.Equal("g", RegisterMap.GetRegister(2).StringValue);
                Assert.Equal("d", RegisterMap.GetRegister(RegisterName.SmallDelete).StringValue);
            }

            [Fact]
            public void ChangeSmallWithSearchMotion()
            {
                Create("dog");
                RegisterMap.GetRegister(1).UpdateValue("g");
                _vimBuffer.Process(@"c/o", enter: true);
                Assert.Equal("d", RegisterMap.GetRegister(1).StringValue);
                Assert.Equal("g", RegisterMap.GetRegister(2).StringValue);
                Assert.Equal("d", RegisterMap.GetRegister(RegisterName.SmallDelete).StringValue);
            }

            [Fact]
            public void Issue1436()
            {
                Create("cat", "dog", "fish", "tree");

                var span = new SnapshotSpan(
                    _textBuffer.GetLine(1).Start.Add(2),
                    _textBuffer.GetLine(2).End);
                var adhocOutliner = EditorUtilsFactory.GetOrCreateOutliner(_textBuffer);
                adhocOutliner.CreateOutliningRegion(span, SpanTrackingMode.EdgeInclusive, "test", "test");
                OutliningManagerService.GetOutliningManager(_textView).CollapseAll(span, _ => true);

                _textView.MoveCaretTo(_textBuffer.GetLine(2).End);
                _vimBuffer.ProcessNotation("dd");
                Assert.Equal(new[] { "cat", "tree" }, _textBuffer.GetLines());
            }
        }

        public abstract class TagBlocksMotionTest : NormalModeIntegrationTest
        {
            public sealed class DeleteTest : TagBlocksMotionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("<a>   </a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("dit");
                    Assert.Equal("<a></a>", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// The br element is not considered a tag  (help tag-blocks)
                /// </summary>
                [Fact]
                public void NotTag1()
                {
                    Create("<a> <br>  </a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("dit");
                    Assert.Equal("<a></a>", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// The meta element is not considered a tag  (help tag-blocks)
                /// </summary>
                [Fact]
                public void NotTag2()
                {
                    Create("<a> <meta>  </a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("dit");
                    Assert.Equal("<a></a>", _textBuffer.GetLine(0).GetText());
                }

                [Fact]
                public void CaseDoesNotMatter()
                {
                    Create("<a>   </A>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("dit");
                    Assert.Equal("<a></A>", _textBuffer.GetLine(0).GetText());
                }

                [Fact]
                public void SingleItemTagsDoNotMatter()
                {
                    Create("<a> <blah/>  </A>");
                    _textView.MoveCaretTo(6);
                    _vimBuffer.Process("dit");
                    Assert.Equal("<a></A>", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class YankTagBlockTest : TagBlocksMotionTest
            {
                [Fact]
                public void InnerNoCount()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    _textView.MoveCaretTo(7);
                    _vimBuffer.Process("yit");
                    Assert.Equal("cat", UnnamedRegister.StringValue);
                }

                [Fact]
                public void InnerCount()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    _textView.MoveCaretTo(7);
                    _vimBuffer.Process("y2it");
                    Assert.Equal("<b>cat</b><b>dog</b>", UnnamedRegister.StringValue);
                }

                [Fact]
                public void InnerOnTagStart()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("yit");
                    Assert.Equal("cat", UnnamedRegister.StringValue);
                }

                [Fact]
                public void AllOnTagStart()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("yat");
                    Assert.Equal("<b>cat</b>", UnnamedRegister.StringValue);
                }

                [Fact]
                public void AllCount()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("y2at");
                    Assert.Equal("<a><b>cat</b><b>dog</b></a>", UnnamedRegister.StringValue);
                }

                [Fact]
                public void AllBadCount()
                {
                    Create("<a><b>cat</b><b>dog</b></a>");
                    UnnamedRegister.UpdateValue("dog");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("y4at");
                    Assert.Equal(1, VimHost.BeepCount);
                    Assert.Equal("dog", UnnamedRegister.StringValue);
                }
            }
        }

        public sealed class AllSentenceTextObjectTest : NormalModeIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                Create("dog. cat. bear.");
                _vimBuffer.Process("yas");
                Assert.Equal("dog. ", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// When starting in the white space include it in the motion instead of the trailing
            /// white space
            /// </summary>
            [Fact]
            public void FromWhiteSpace()
            {
                Create("dog. cat. bear.");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("yas");
                Assert.Equal(" cat.", UnnamedRegister.StringValue);
            }

            [Fact]
            public void EmptyLinesAreSentences()
            {
                Create("dog.  ", "", "cat.");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yas");
                Assert.Equal("  " + Environment.NewLine, UnnamedRegister.StringValue);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("P");
                Assert.Equal(
                    new[] { "  ", "dog.  ", "", "cat." },
                    _textBuffer.GetLines());
            }
        }

        public sealed class SentenceTextObject : NormalModeIntegrationTest
        {
            [Fact]
            public void SentenceOnNotFirstColumn()
            {
                Create(" c", "", " d");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void SentenceOnNotFirstColumnSecondLine()
            {
                Create(" f", " c", "", " d");
                _textView.MoveCaretToLine(1, 1);
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Complex()
            {
                Create(" f", "", " c", "", " d");
                _textView.MoveCaretToLine(2, 1);
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetLine(3).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void WhiteSpaceAfterEmptyLine()
            {
                Create("", "  test");
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            [Fact]
            public void WhiteSpaceAfterEmptyLine2()
            {
                Create("", "  test");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            [Fact]
            public void BlanksAfterSentence()
            {
                Create("cat.", "  test");
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
            }

            [Fact]
            public void WhiteSpaceStartOfBuffer()
            {
                Create("  t", "d.", "c", "e");
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(2, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void HtmlBlocks()
            {
                var text =
@"
  <dl>
    <li>Here we are</li>
    <li>Here we are</li>
  </dl>

  <dl>
    <li>Here we are</li>
    <li>Here we are</li>
  </dl>

  <dl>
    <li>Here we are</li>
    <li>Here we are</li>
  </dl>
";

                Create(text);
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(5, 0), _textView.GetCaretPoint());
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(6, 2), _textView.GetCaretPoint());
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(10, 0), _textView.GetCaretPoint());
            }

            /// <summary>
            /// An empty line is a sentence.  It's technically a paragraph but paragraph boundaries
            /// count as sentence boundaries
            /// </summary>
            [Fact]
            public void EmptyLineIsSentence()
            {
                Create("cat", "", " dog");
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(2, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// A blank line is not a sentence boundary.  A blank line is a line which has only white
            /// space as the content.  It's not a paragraph boundary and hence isn't a sentence
            /// </summary>
            [Fact]
            public void BlankLineIsNotSentence()
            {
                Create("cat", " ", " dog");
                _vimBuffer.Process(')');
                Assert.Equal(_textBuffer.GetPointInLine(2, 3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// If the ')' begins in the white space between sentences then we need to move to the 
            /// start of the next sentence.
            /// </summary>
            [Fact]
            public void StartInWhiteSpace()
            {
                Create("cat.  dog.  fish.");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process(')');
                Assert.Equal(6, _textView.GetCaretPoint().Position);
                _vimBuffer.Process(')');
                Assert.Equal(12, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class ShiftLeftTest : NormalModeIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _vimBuffer.LocalSettings.ShiftWidth = 4;
            }

            [Fact]
            public void Simple()
            {
                Create("    cat", "    dog");
                _vimBuffer.Process("<<");
                Assert.Equal(new[] { "cat", "    dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void Multiline()
            {
                Create("    cat", "    dog");
                _vimBuffer.Process("2<<");
                Assert.Equal(new[] { "cat", "dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void EmptyLine()
            {
                Create("", "dog");
                _vimBuffer.Process("<<");
                Assert.Equal(new[] { "", "dog" }, _textBuffer.GetLines());
            }

            /// <summary>
            /// A left shift of a blank line will remove the contents
            /// </summary>
            [Fact]
            public void BlankLine()
            {
                Create(" ", "dog");
                _vimBuffer.Process("<<");
                Assert.Equal(new[] { "", "dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void CountInMiddle()
            {
                Create("    cat", "    dog");
                _vimBuffer.Process("<2<");
                Assert.Equal(new[] { "cat", "dog" }, _textBuffer.GetLines());
            }
        }

        public sealed class ShiftRightTest : NormalModeIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _vimBuffer.LocalSettings.ShiftWidth = 4;
            }

            /// <summary>
            /// The shift right of an empty line should not add any spaces
            /// </summary>
            [Fact]
            public void EmptyLine()
            {
                Create("", "dog");
                _vimBuffer.Process(">>");
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// The shift right of an empty line should not add any spaces
            /// </summary>
            [Fact]
            public void IncludeEmptyLine()
            {
                Create("cat", "", "dog");
                _vimBuffer.Process("3>>");
                Assert.Equal("    cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("", _textBuffer.GetLine(1).GetText());
                Assert.Equal("    dog", _textBuffer.GetLine(2).GetText());
            }

            /// <summary>
            /// The shift right of a blank line should add spaces
            /// </summary>
            [Fact]
            public void BlankLine()
            {
                Create(" ", "dog");
                _vimBuffer.Process(">>");
                Assert.Equal("     ", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void CountInMiddle()
            {
                Create("cat", "dog");
                _vimBuffer.Process(">2>");
                Assert.Equal(new[] { "    cat", "    dog" }, _textBuffer.GetLines());
            }
        }

        public sealed class AddTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// Make sure we jump across the blanks to get to the word and that the caret is 
            /// properly positioned
            /// </summary>
            [Fact]
            public void Decimal()
            {
                Create(" 999");
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal(" 1000", _textBuffer.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Negative decimal number
            /// </summary>
            [Fact]
            public void DecimalNegative()
            {
                Create(" -10");
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal(" -9", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Add to the word on the non-first line.  Ensures we are calculating the replacement span
            /// in the correct location
            /// </summary>
            [Fact]
            public void HexSecondLine()
            {
                Create("hello", "  0x42");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal("  0x43", _textBuffer.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(5), _textView.GetCaretPoint());
            }

            [Fact]
            public void HexAllLetters()
            {
                Create("0xff");
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal("0x100", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that we can handle the 0x1a number for add 
            /// </summary>
            [Fact]
            public void Issue982()
            {
                Create("0x1a");
                _vimBuffer.ProcessNotation("<C-a>");
                Assert.Equal("0x1b", _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class NumberedRegisterTest : NormalModeIntegrationTest
        {
            private void AssertRegister(int number, string value, bool addNewLine = true)
            {
                value = addNewLine ? value + Environment.NewLine : value;
                var c = number.ToString()[0];
                var name = RegisterName.OfChar(c).Value;
                Assert.Equal(value, _vimBuffer.RegisterMap.GetRegister(name).StringValue);
            }

            [Fact]
            public void DeleteLine()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.Process("dd");
                AssertRegister(1, "cat");
            }

            [Fact]
            public void DeleteLineMultiple()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.Process("dddd");
                AssertRegister(1, "dog");
                AssertRegister(2, "cat");
            }

            [Fact]
            public void ChangeDoesntUpdate()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.Process("C");
                AssertRegister(1, "", addNewLine: false);
            }

            [Fact]
            public void DeleteTillEndOfLine()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.Process("D");
                AssertRegister(1, "", addNewLine: false);
            }
        }

        public sealed class JumpListTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// A yank of a jump motion should update the jump list
            /// </summary>
            [Fact]
            public void YankMotionShouldUpdate()
            {
                Create("cat", "dog", "cat");
                _vimBuffer.Process("y*");
                Assert.Equal(_textView.GetPoint(0), _jumpList.Jumps.First().Position);
                Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Doing a * on a word that doesn't even match should still update the jump list
            /// </summary>
            [Fact]
            public void NextWordWithNoMatch()
            {
                Create("cat", "dog", "fish");
                var didHit = false;
                _vimBuffer.WarningMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchForwardWrapped, args.Message);
                        didHit = true;
                    };
                _assertOnWarningMessage = false;
                _vimBuffer.Process("*");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.Equal(_textView.GetPoint(0), _textView.GetCaretPoint());
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
                Assert.True(didHit);
            }

            /// <summary>
            /// If a jump to previous occurs on a location which is not in the list and we
            /// are not already traversing the jump list then the location is added
            /// </summary>
            [Fact]
            public void FromLocationNotInList()
            {
                Create("cat", "dog", "fish");
                _jumpList.Add(_textView.GetPoint(0));
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.Equal(_textView.GetLine(0).Start, _textView.GetCaretPoint());
                Assert.Equal(1, _jumpList.CurrentIndex.Value);
                Assert.Equal(2, _jumpList.Jumps.Length);
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('i'));
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void LocalDeclarationShouldAlterJumpList()
            {
                Create("cat", "dog", "fish", "tree");
                _textView.MoveCaretToLine(1);
                _vimHost.GoToLocalDeclarationFunc = (textView, arg) =>
                    {
                        _textView.MoveCaretToLine(0);
                        return true;
                    };
                _vimBuffer.Process("gd");
                Assert.Equal(0, _textView.GetCaretPoint());
                _vimBuffer.Process("``");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void GlobalDeclarationShouldAlterJumpList()
            {
                Create("cat", "dog", "fish", "tree");
                _textView.MoveCaretToLine(1);
                _vimHost.GoToGlobalDeclarationFunc = (textView, arg) =>
                    {
                        _textView.MoveCaretToLine(0);
                        return true;
                    };
                _vimBuffer.Process("gD");
                Assert.Equal(0, _textView.GetCaretPoint());
                _vimBuffer.Process("``");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class MotionMiscTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// [[ motion should put the caret on the target character
            /// </summary>
            [Fact]
            public void Section1()
            {
                Create("hello", "{world");
                _vimBuffer.Process("]]");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// [[ motion should put the caret on the target character
            /// </summary>
            [Fact]
            public void Section2()
            {
                Create("hello", "\fworld");
                _vimBuffer.Process("]]");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Section3()
            {
                Create("foo", "{", "bar");
                _textView.MoveCaretTo(_textView.GetLine(2).End);
                _vimBuffer.Process("[[");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Section4()
            {
                Create("foo", "{", "bar", "baz");
                _textView.MoveCaretTo(_textView.GetLine(3).End);
                _vimBuffer.Process("[[");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Section5()
            {
                Create("foo", "{", "bar", "baz", "jazz");
                _textView.MoveCaretTo(_textView.GetLine(4).Start);
                _vimBuffer.Process("[[");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The ']]' motion should stop on section macros
            /// </summary>
            [Fact]
            public void SectionForwardToMacro()
            {
                Create("cat", "", "bear", ".HU", "sheep");
                _globalSettings.Sections = "HU";
                _vimBuffer.Process("]]");
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Move the caret using the end of word motion repeatedly
            /// </summary>
            [Fact]
            public void MoveEndOfWord()
            {
                Create("the cat chases the dog");
                _vimBuffer.Process("e");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("e");
                Assert.Equal(6, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("e");
                Assert.Equal(13, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("e");
                Assert.Equal(17, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("e");
                Assert.Equal(21, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'w' needs to be able to get off of a blank line
            /// </summary>
            [Fact]
            public void MoveWordAcrossBlankLine()
            {
                Create("dog", "", "cat ball");
                _vimBuffer.Process("w");
                Assert.Equal(_textView.GetPointInLine(1, 0), _textView.GetCaretPoint());
                _vimBuffer.Process("w");
                Assert.Equal(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
                _vimBuffer.Process("w");
                Assert.Equal(_textView.GetPointInLine(2, 4), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The 'w' from a blank should move to the next word
            /// </summary>
            [Fact]
            public void WordFromBlank()
            {
                Create("the dog chased the ball");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("w");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("w");
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'b' from a blank should move to the start of the previous word
            /// </summary>
            [Fact]
            public void WordFromBlankBackward()
            {
                Create("the dog chased the ball");
                _textView.MoveCaretTo(7);
                _vimBuffer.Process("b");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("b");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'b' from the start of a word should move to the start of the previous word
            /// </summary>
            [Fact]
            public void WordFromStartBackward()
            {
                Create("the dog chased the ball");
                _textView.MoveCaretTo(8);
                _vimBuffer.Process("b");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("b");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// See the full discussion in issue #509
            ///
            /// https://github.com/jaredpar/VsVim/issues/509
            ///
            /// Make sure that doing a ""][" from the middle of the line ends on the '}' if it is
            /// preceded by a blank line
            /// </summary>
            [Fact]
            public void MoveSection_RegressionTest_509()
            {
                Create("cat", "", "}");
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("][");
                Assert.Equal(_textView.GetPointInLine(2, 0), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Case is explicitly called out in the ':help exclusive-linewise' portion
            /// of the documentation
            /// </summary>
            [Fact]
            public void ExclusiveLineWise()
            {
                Create("  dog", "cat", "", "pig");
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("d}");
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("pig", _textView.GetLine(1).GetText());
                _vimBuffer.Process("p");
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("  dog", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal("pig", _textView.GetLine(3).GetText());
            }

            /// <summary>
            /// Make sure we move to the column on the current line when there is no count
            /// </summary>
            [Fact]
            public void FirstNonWhiteSpaceOnLine()
            {
                Create(" cat", "  dog", "   fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("_");
                Assert.Equal(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Simple word motion.  Make sure the caret gets put on the start of the next
            /// word
            /// </summary>
            [Fact]
            public void Word()
            {
                Create("cat dog bear");
                _vimBuffer.Process("w");
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                _vimBuffer.Process("w");
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When there is no white space following a word and there is white space before 
            /// and a word on the same line then we grab the white space before the word
            /// </summary>
            [Fact]
            public void AllWord_WhiteSpaceOnlyBefore()
            {
                Create("hello", "cat dog", "  bat");
                _textView.MoveCaretTo(_textView.GetLine(1).Start.Add(4));
                Assert.Equal('d', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process("yaw");
                Assert.Equal(" dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// When starting in the white space it should be included and not the white space
            /// after
            /// </summary>
            [Fact]
            public void AllWord_InWhiteSpaceBeforeWord()
            {
                Create("dog cat tree");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("yaw");
                Assert.Equal(" cat", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Simple yank of a () block 
            /// </summary>
            [Fact]
            public void Block_AllParen_Simple()
            {
                Create("cat (dog) bear");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("ya(");
                Assert.Equal("(dog)", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Simple yank of a () block via the b command
            /// </summary>
            [Fact]
            public void Block_AllParen_SimpleAltKey()
            {
                Create("cat (dog) bear");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("yab");
                Assert.Equal("(dog)", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Simple yank of a () block 
            /// </summary>
            [Fact]
            public void Block_InnerParen_Simple()
            {
                Create("cat (dog) bear");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("yi(");
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }
        }

        public sealed class BackwardEndOfWordMotionTest : NormalModeIntegrationTest
        {
            [Fact]
            public void SimpleWord()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("ge");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void WordMixed()
            {
                Create("cat d!g fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.ProcessNotation("ge");
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void AllWordMixed()
            {
                Create("cat d!g fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.ProcessNotation("gE");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void FirstWordOnLine()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("gEgE");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void WithCount()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("2gE");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void AcrossLines()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("gE");
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            [Fact]
            public void AcrossLines2()
            {
                Create("big cat", "big dog");
                _textView.MoveCaretToLine(1, 5);
                _vimBuffer.ProcessNotation("ge");
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                _vimBuffer.ProcessNotation("ge");
                Assert.Equal(_textBuffer.GetPointInLine(0, 6), _textView.GetCaretPoint());
                _vimBuffer.ProcessNotation("ge");
                Assert.Equal(_textBuffer.GetPointInLine(0, 2), _textView.GetCaretPoint());
            }

            [Fact]
            public void Issue1124()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("yge");
                Assert.Equal("t d", UnnamedRegister.StringValue);
            }
        }

        public sealed class DocumentPercentMotionTest : NormalModeIntegrationTest
        {
            private void CreateTenWords()
            {
                Create("dog", "cat", "fish", "bear", "tree", "dog", "cat", "fish", "bear", "tree");
            }

            private void AssertPercentLine(int number, int expected)
            {
                var motion = string.Format("{0}%", number);
                _vimBuffer.ProcessNotation(motion);

                var lineNumber = _textView.GetCaretLine().LineNumber + 1; // 0 based editor
                Assert.Equal(expected, lineNumber);
            }

            [Fact]
            public void TenWords()
            {
                CreateTenWords();

                AssertPercentLine(1, 1);
                AssertPercentLine(10, 1);
                AssertPercentLine(60, 6);
                AssertPercentLine(80, 8);
                AssertPercentLine(100, 10);
            }

            [Fact]
            public void AlwaysRoundAwayFromZero()
            {
                CreateTenWords();

                AssertPercentLine(11, 2);
                AssertPercentLine(91, 10);
            }

            [Fact]
            public void MoreThan100PercentIsAnError()
            {
                CreateTenWords();

                _textView.MoveCaretToLine(3);
                AssertPercentLine(120, 4);
            }
        }

        public sealed class MiscTest : NormalModeIntegrationTest
        {
            /// <summary>
            /// The backspace key should cancel a replace char
            /// </summary>
            [Fact]
            public void ReplaceChar_BackspaceShouldCancel()
            {
                Create("hello world");
                _vimBuffer.Process('r');
                Assert.True(_normalMode.InReplace);
                _vimBuffer.Process(VimKey.Back);
                Assert.False(_normalMode.InReplace);
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// The delete key should cancel a replace char
            /// </summary>
            [Fact]
            public void ReplaceChar_DeleteShouldCancel()
            {
                Create("hello world");
                _vimBuffer.Process('r');
                Assert.True(_normalMode.InReplace);
                _vimBuffer.Process(VimKey.Back);
                Assert.False(_normalMode.InReplace);
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// A d with Enter should delete the line break
            /// </summary>
            [Fact]
            public void Issue317_1()
            {
                Create("dog", "cat", "jazz", "band");
                _vimBuffer.Process("2d", enter: true);
                Assert.Equal("band", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// "Verify the contents after with a paste
            /// </summary>
            [Fact]
            public void Issue317_2()
            {
                Create("dog", "cat", "jazz", "band");
                _vimBuffer.Process("2d", enter: true);
                _vimBuffer.Process("p");
                Assert.Equal("band", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal("jazz", _textView.GetLine(3).GetText());
            }

            /// <summary>
            /// Plain old Enter should just move the cursor one line
            /// </summary>
            [Fact]
            public void Issue317_3()
            {
                Create("dog", "cat", "jazz", "band");
                _vimBuffer.Process(KeyInputUtil.EnterKey);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void RepeatLastSearch1()
            {
                Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
                _vimBuffer.Process("/pig", enter: true);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                _textView.MoveCaretTo(0);
                _vimBuffer.Process('n');
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void RepeatLastSearch2()
            {
                Create("random text", "pig dog cat", "pig dog cat", "pig dog cat");
                _vimBuffer.Process("/pig", enter: true);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                _vimBuffer.Process('n');
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void RepeatLastSearch3()
            {
                Create("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
                _vimBuffer.Process("/pig", enter: true);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                _textView.MoveCaretTo(_textView.GetLine(2).Start);
                _vimBuffer.Process('N');
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// A change word operation shouldn't delete the whitespace trailing the word
            /// </summary>
            [Fact]
            public void Change_Word()
            {
                Create("dog cat bear");
                _vimBuffer.Process("cw");
                Assert.Equal(" cat bear", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// A change all word operation should delete the whitespace trailing the word.  Really
            /// odd when considering 'cw' doesn't.
            /// </summary>
            [Fact]
            public void Change_AllWord()
            {
                Create("dog cat bear");
                _vimBuffer.Process("caw");
                Assert.Equal("cat bear", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure that we can change the character at the end of a line
            /// </summary>
            [Fact]
            public void Change_CharAtEndOfLine()
            {
                Create("hat", "cat");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = String.Empty;
                _vimBuffer.Process("cl");
                Assert.Equal("ha", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Ensure that we can change the character at the end of a line when 've=onemore'
            /// </summary>
            [Fact]
            public void Change_CharAtEndOfLine_VirtualEditOneMore()
            {
                Create("hat", "cat");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
                _vimBuffer.Process("cl");
                Assert.Equal("ha", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Changing till the end of the line should leave the caret in it's current position
            /// </summary>
            [Fact]
            public void Change_TillEndOfLine_NoVirtualEdit()
            {
                Create("hello", "world");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "";
                _vimBuffer.Process("C");
                Assert.Equal("he", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Changing till the end of the line should leave the caret in it's current position.  The virtual
            /// edit setting shouldn't affect this
            /// </summary>
            [Fact]
            public void Change_TillEndOfLine_VirtualEditOneMore()
            {
                Create("hello", "world");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
                _vimBuffer.Process("C");
                Assert.Equal("he", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Verify that doing a change till the end of the line won't move the cursor
            /// </summary>
            [Fact]
            public void Change_Motion_EndOfLine_NoVirtualEdit()
            {
                Create("hello", "world");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "";
                _vimBuffer.Process("c$");
                Assert.Equal("he", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Verify that doing a change till the end of the line won't move the cursor
            /// </summary>
            [Fact]
            public void Change_Motion_EndOfLine_VirtualEditOneMore()
            {
                Create("hello", "world");
                _textView.MoveCaretTo(2);
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
                _vimBuffer.Process("c$");
                Assert.Equal("he", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure the d#d syntax doesn't apply to other commands like change.  The 'd' suffix in 'd#d' is 
            /// *not* a valid motion
            /// </summary>
            [Fact]
            public void Change_Illegal()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("c2d");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("tree", _textBuffer.GetLine(2).GetText());
            }

            /// <summary>
            /// When virtual edit is disabled and 'x' is used to delete the last character on the line
            /// then the caret needs to move backward to maintain the non-virtual edit position
            /// </summary>
            [Fact]
            public void DeleteChar_EndOfLine_NoVirtualEdit()
            {
                Create("test");
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
                _textView.MoveCaretTo(3);
                _vimBuffer.Process('x');
                Assert.Equal("tes", _textView.GetLineRange(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When virtual edit is enabled and 'x' is used to delete the last character on the line
            /// then the caret should stay in it's current position 
            /// </summary>
            [Fact]
            public void DeleteChar_EndOfLine_VirtualEdit()
            {
                Create("test", "bar");
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(3);
                _vimBuffer.Process('x');
                Assert.Equal("tes", _textView.GetLineRange(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret position should remain unchanged when deleting a character in the middle of 
            /// a word
            /// </summary>
            [Fact]
            public void DeleteChar_MiddleOfWord()
            {
                Create("test", "bar");
                _vimBuffer.LocalSettings.GlobalSettings.VirtualEdit = string.Empty;
                _textView.MoveCaretTo(1);
                _vimBuffer.Process('x');
                Assert.Equal("tst", _textView.GetLineRange(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When virtual edit is not enabled then the delete till end of line should cause the 
            /// caret to move back to the last non-editted character
            /// </summary>
            [Fact]
            public void DeleteTillEndOfLine_NoVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = string.Empty;
                _textView.MoveCaretTo(1);
                _vimBuffer.Process('D');
                Assert.Equal("c", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When virtual edit is enabled then the delete till end of line should not move 
            /// the caret at all
            /// </summary>
            [Fact]
            public void DeleteTillEndOfLine_WithVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(1);
                _vimBuffer.Process('D');
                Assert.Equal("c", _textView.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint());
            }

            /// <summary>
            /// At the end of the ':help d{motion}` entry it lists a special case where the command
            /// becomes linewise.  When it's a multiline delete and there is whitespace before / after
            /// the span.  
            /// </summary>
            [Fact]
            public void DeleteMotionSpecialCase()
            {
                Create(" cat", " dog    ", " fish");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("d/  ", enter: true);
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal(" fish", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure the cursor positions correctly on the next line 
            /// </summary>
            [Fact]
            public void Handle_BraceClose_MiddleOfParagraph()
            {
                Create("dog", "", "cat");
                _vimBuffer.Process("}");
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void Handle_cb_DeleteWhitespaceAtEndOfSpan()
            {
                Create("public static void Main");
                _textView.MoveCaretTo(19);
                _vimBuffer.Process("cb");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("public static Main", _textView.GetLine(0).GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Handle_cl_WithCountShouldDeleteWhitespace()
            {
                Create("dog   cat");
                _vimBuffer.Process("5cl");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(" cat", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void Handle_d_WithMarkLineMotion()
            {
                Create("dog", "cat", "bear", "tree");
                _vimTextBuffer.SetLocalMark(LocalMark.OfChar('a').Value, 1, 0);
                _vimBuffer.Process("d'a");
                Assert.Equal("bear", _textView.GetLine(0).GetText());
                Assert.Equal("tree", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void Handle_d_WithMarkMotion()
            {
                Create("dog", "cat", "bear", "tree");
                _vimTextBuffer.SetLocalMark(LocalMark.OfChar('a').Value, 1, 1);
                _vimBuffer.Process("d`a");
                Assert.Equal("at", _textView.GetLine(0).GetText());
                Assert.Equal("bear", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Even though the motion will include the second line it should not 
            /// be included in the delete operation.  This hits the special case
            /// listed in :help exclusive
            /// </summary>
            [Fact]
            public void Handle_d_WithParagraphMotion()
            {
                Create("dog", "", "cat");
                _vimBuffer.Process("d}");
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void Handle_f_WithTabTarget()
            {
                Create("dog\tcat");
                _vimBuffer.Process("f\t");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Handle_Minus_MiddleOfBuffer()
            {
                Create("dog", "  cat", "bear");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("-");
                Assert.Equal(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Escape should exit one time normal mode and return back to the previous mode
            /// </summary>
            [Fact]
            public void OneTimeNormalMode_EscapeShouldExit()
            {
                Create("");
                _vimBuffer.Process("i");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('o'));
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [Fact]
            public void Handle_s_AtEndOfLine()
            {
                Create("dog", "cat");
                _textView.MoveCaretTo(2);
                _vimBuffer.Process('s');
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal("do", _textView.GetLine(0).GetText());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// This command should only yank from the current line to the end of the file
            /// </summary>
            [Fact]
            public void Handle_yG_NonFirstLine()
            {
                Create("dog", "cat", "bear");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("yG");
                Assert.Equal("cat" + Environment.NewLine + "bear" + Environment.NewLine, _vimBuffer.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// Make sure the caret is properly positioned against a join across 3 lines
            /// </summary>
            [Fact]
            public void Join_CaretPositionThreeLines()
            {
                Create("cat", "dog", "bear");
                _vimBuffer.Process("3J");
                Assert.Equal("cat dog bear", _textView.GetLine(0).GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure the text is repeated
            /// </summary>
            [Fact]
            public void InsertAtEndOfLine_WithCount()
            {
                Create("dog", "bear");
                _vimBuffer.Process("3A");
                _vimBuffer.Process('b');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("dogbbb", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure repeat last char search is functioning
            /// </summary>
            [Fact]
            public void RepeatLastCharSearch_Forward()
            {
                Create("hello", "world");
                _vimBuffer.Process("fr");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(";");
                Assert.Equal(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The repeat last char search command shouldn't toggle itself.  Or in short it should be
            /// possible to scan an entire line in one direction
            /// </summary>
            [Fact]
            public void RepeatLastCharSearch_ManyTimes()
            {
                Create("hello world dog");
                _vimBuffer.VimData.LastCharSearch = FSharpOption.Create(Tuple.Create(CharSearchKind.ToChar, SearchPath.Forward, 'o'));
                _textView.MoveCaretTo(_textView.GetEndPoint().Subtract(1));
                _vimBuffer.Process(',');
                Assert.Equal(SearchPath.Forward, _vimBuffer.VimData.LastCharSearch.Value.Item2);
                Assert.Equal(13, _textView.GetCaretPoint().Position);
                _vimBuffer.Process(',');
                Assert.Equal(SearchPath.Forward, _vimBuffer.VimData.LastCharSearch.Value.Item2);
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Enter should not go through normal mode mapping during an incremental search
            /// </summary>
            [Fact]
            public void Remap_EnterShouldNotMapDuringSearch()
            {
                Create("cat dog");
                _keyMap.MapWithNoRemap("<Enter>", "o<Esc>", KeyRemapMode.Normal);
                _vimBuffer.Process("/dog");
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                Assert.Equal("cat dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure we can remap keys to nop and have them do nothing
            /// </summary>
            [Fact]
            public void Remap_Nop()
            {
                Create("cat");
                _keyMap.MapWithNoRemap("$", "<nop>", KeyRemapMode.Normal);
                _vimBuffer.Process('$');
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Ensure the commands map properly
            /// </summary>
            [Fact]
            public void Remap_Issue474()
            {
                Create("cat", "dog", "bear", "pig", "tree", "fish");
                _vimBuffer.Process(":nnoremap gj J");
                _vimBuffer.Process(VimKey.Enter);
                _vimBuffer.Process(":map J 4j");
                _vimBuffer.Process(VimKey.Enter);
                _vimBuffer.Process("J");
                Assert.Equal(4, _textView.GetCaretLine().LineNumber);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("gj");
                Assert.Equal("cat dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Delete with an append register should concatenate the values
            /// </summary>
            [Fact]
            public void Delete_Append()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.Process("\"cyaw");
                _vimBuffer.Process("j");
                _vimBuffer.Process("\"Cdw");
                Assert.Equal("dogcat", _vimBuffer.RegisterMap.GetRegister('c').StringValue);
                Assert.Equal("dogcat", _vimBuffer.RegisterMap.GetRegister('C').StringValue);
                _vimBuffer.Process("\"cp");
                Assert.Equal("dogcat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure that 'd0' is interpreted correctly as 'd{motion}' and not 'd#d'.  0 is not 
            /// a count
            /// </summary>
            [Fact]
            public void Delete_BeginingOfLine()
            {
                Create("dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("d0");
                Assert.Equal("og", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Deleting a word left at the start of the line results in empty data and
            /// should not cause the register contents to be altered
            /// </summary>
            [Fact]
            public void Delete_LeftAtStartOfLine()
            {
                Create("dog", "cat");
                UnnamedRegister.UpdateValue("hello");
                _vimBuffer.Process("dh");
                Assert.Equal("hello", UnnamedRegister.StringValue);
                Assert.Equal(0, _vimHost.BeepCount);
            }

            /// <summary>
            /// Delete when combined with the line down motion 'j' should delete two lines
            /// since it's deleting the result of the motion from the caret
            ///
            /// Convered by issue 288
            /// </summary>
            [Fact]
            public void Delete_LineDown()
            {
                Create("abc", "def", "ghi", "jkl");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("dj");
                Assert.Equal("ghi", _textView.GetLine(0).GetText());
                Assert.Equal("jkl", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When a delete of a search motion which wraps occurs a warning message should
            /// be displayed
            /// </summary>
            [Fact]
            public void Delete_SearchWraps()
            {
                Create("dog", "cat", "tree");
                var didHit = false;
                _textView.MoveCaretToLine(1);
                _assertOnWarningMessage = false;
                _vimBuffer.WarningMessage +=
                    (_, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchForwardWrapped, args.Message);
                        didHit = true;
                    };
                _vimBuffer.Process("d/dog", enter: true);
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("tree", _textView.GetLine(1).GetText());
                Assert.True(didHit);
            }

            /// <summary>
            /// Delete a word at the end of the line.  It should not delete the line break
            /// </summary>
            [Fact]
            public void Delete_WordEndOfLine()
            {
                Create("the cat", "chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("dw");
                Assert.Equal("the ", _textView.GetLine(0).GetText());
                Assert.Equal("chased the bird", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Delete a word at the end of the line where the next line doesn't start in column
            /// 0.  This should still not cause the end of the line to delete
            /// </summary>
            [Fact]
            public void Delete_WordEndOfLineNextStartNotInColumnZero()
            {
                Create("the cat", "  chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("dw");
                Assert.Equal("the ", _textView.GetLine(0).GetText());
                Assert.Equal("  chased the bird", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Delete across a line where the search ends in white space but not inside of 
            /// column 0
            /// </summary>
            [Fact]
            public void Delete_SearchAcrossLineNotInColumnZero()
            {
                Create("the cat", "  chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("d/cha", enter: true);
                Assert.Equal("the chased the bird", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Delete across a line where the search ends in column 0 of the next line
            /// </summary>
            [Fact]
            public void Delete_SearchAcrossLineIntoColumnZero()
            {
                Create("the cat", "chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("d/cha", enter: true);
                Assert.Equal("the ", _textView.GetLine(0).GetText());
                Assert.Equal("chased the bird", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Don't delete the new line when doing a 'daw' at the end of the line
            /// </summary>
            [Fact]
            public void Delete_AllWordEndOfLineIntoColumnZero()
            {
                Create("the cat", "chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("daw");
                Assert.Equal("the", _textView.GetLine(0).GetText());
                Assert.Equal("chased the bird", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Delete a word at the end of the line where the next line doesn't start in column
            /// 0.  This should still not cause the end of the line to delete
            /// </summary>
            [Fact]
            public void Delete_AllWordEndOfLineNextStartNotInColumnZero()
            {
                Create("the cat", "  chased the bird");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("daw");
                Assert.Equal("the", _textView.GetLine(0).GetText());
                Assert.Equal("  chased the bird", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// When virtual edit is enabled then deletion should not cause the caret to 
            /// move if it would otherwise be in virtual space
            /// </summary>
            [Fact]
            public void Delete_WithVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = "onemore";
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("dl");
                Assert.Equal("ca", _textView.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When virtual edit is not enabled then deletion should cause the caret to 
            /// move if it would end up in virtual space
            /// </summary>
            [Fact]
            public void Delete_NoVirtualEdit()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = string.Empty;
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("dl");
                Assert.Equal("ca", _textView.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure deleting the last line changes the line count in the buffer
            /// </summary>
            [Fact]
            public void DeleteLines_OnLastLine()
            {
                Create("foo", "bar");
                _textView.MoveCaretTo(_textView.GetLine(1).Start);
                _vimBuffer.Process("dd");
                Assert.Equal("foo", _textView.TextSnapshot.GetText());
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Delete lines with the special d#d count syntax
            /// </summary>
            [Fact]
            public void DeleteLines_Special_Simple()
            {
                Create("cat", "dog", "bear", "fish");
                _vimBuffer.Process("d2d");
                Assert.Equal("bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textBuffer.CurrentSnapshot.LineCount);
            }

            /// <summary>
            /// Delete lines with both counts and make sure the counts are multiplied together
            /// </summary>
            [Fact]
            public void DeleteLines_Special_TwoCounts()
            {
                Create("cat", "dog", "bear", "fish", "tree");
                _vimBuffer.Process("2d2d");
                Assert.Equal("tree", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
            }

            /// <summary>
            /// The caret should be returned to the original first line when undoing a 'dd'
            /// command
            /// </summary>
            [Fact]
            public void DeleteLines_Undo()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process("ddu");
                Assert.Equal(new[] { "cat", "dog", "fish" }, _textBuffer.GetLines());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Subtract a negative decimal number
            /// </summary>
            [Fact]
            public void SubtractFromWord_Decimal_Negative()
            {
                Create(" -10");
                _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('x'));
                Assert.Equal(" -11", _textBuffer.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure we handle the 'gv' command to switch to the previous visual mode
            /// </summary>
            [Fact]
            public void SwitchPreviousVisualMode_Line()
            {
                Create("cats", "dogs", "fish");
                var visualSelection = VisualSelection.NewLine(
                    _textView.GetLineRange(0, 1),
                    SearchPath.Forward,
                    1);
                _vimTextBuffer.LastVisualSelection = FSharpOption.Create(visualSelection);
                _vimBuffer.Process("gv");
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive, tabStop: 4));
            }

            /// <summary>
            /// Make sure the caret is positioned properly during undo
            /// </summary>
            [Fact]
            public void Undo_DeleteAllWord()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("daw");
                _vimBuffer.Process("u");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Undoing a change lines for a single line should put the caret at the start of the
            /// line which was changed
            /// </summary>
            [Fact]
            public void Undo_ChangeLines_OneLine()
            {
                Create("  cat");
                _textView.MoveCaretTo(4);
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("cc");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process("u");
                Assert.Equal("  cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Undoing a change lines for a multiple lines should put the caret at the start of the
            /// second line which was changed.  
            /// </summary>
            [Fact]
            public void Undo_ChangeLines_MultipleLines()
            {
                Create("dog", "  cat", "  bear", "  tree");
                _textView.MoveCaretToLine(1);
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("3cc");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process("u");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(_textView.GetPointInLine(2, 2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Need to ensure that ^ run from the first line doesn't register as an 
            /// error.  This ruins the ability to do macro playback
            /// </summary>
            [Fact]
            public void Issue909()
            {
                Create("  cat");
                _textView.MoveCaretTo(2);
                _vimBuffer.Process("^");
                Assert.Equal(0, _vimHost.BeepCount);
            }

            [Fact]
            public void Issue960()
            {
                Create(@"""aaa"", ""bbb"", ""ccc""");
                _textView.MoveCaretTo(7);
                Assert.Equal('\"', _textView.GetCaretPoint().GetChar());
                _vimBuffer.Process(@"di""");
                Assert.Equal(@"""aaa"", """", ""ccc""", _textBuffer.GetLine(0).GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The word forward motion has special rules on how to handle motions that end on the 
            /// first character of the next line and have blank lines above.  Make sure we handle
            /// the case where the blank line is the originating line
            /// </summary>
            [Fact]
            public void DeleteWordOnBlankLineFromEnd()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Process("dw");
                Assert.Equal(new[] { "cat", "  ", "dog" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Similar to above but delete from the middle and make sure we take 2 characters with
            /// the delet instead of 1
            /// </summary>
            [Fact]
            public void DeleteWordOnBlankLineFromMiddle()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 1);
                _vimBuffer.Process("dw");
                Assert.Equal(new[] { "cat", " ", "dog" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(1, 0), _textView.GetCaretPoint());
            }

            [Fact]
            public void DeleteWordOnDoubleBlankLineFromEnd()
            {
                Create("cat", "   ", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Process("dw");
                Assert.Equal(new[] { "cat", "  ", "   ", "dog" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            [Fact]
            public void InnerBlockYankAndPasteIsLinewise()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("yi}");
                Assert.True(UnnamedRegister.OperationKind.IsLineWise);
                _vimBuffer.ProcessNotation("p");
                Assert.Equal(
                    new[] { "  statement;", "  statement;" },
                    _textBuffer.GetLineRange(startLine: 2, endLine: 3).Lines.Select(x => x.GetText()));
            }

            [Fact]
            public void Issue1614()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");
                _localSettings.ShiftWidth = 2;
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation(">i{");
                Assert.Equal("    statement;", _textBuffer.GetLine(2).GetText());
            }

            [Fact]
            public void Issue1738()
            {
                Create("dog", "tree");
                _globalSettings.ClipboardOptions = ClipboardOptions.Unnamed;
                _vimBuffer.Process("yy");
                Assert.Equal("dog" + Environment.NewLine, RegisterMap.GetRegister(0).StringValue);
                Assert.Equal("dog" + Environment.NewLine, RegisterMap.GetRegister(RegisterName.NewSelectionAndDrop(SelectionAndDropRegister.Star)).StringValue);
            }

            [Fact]
            public void Issue1827()
            {
                Create("penny", "dog");
                RegisterMap.SetRegisterValue(0, "cat");
                _vimBuffer.Process("dd");
                Assert.Equal("cat", RegisterMap.GetRegister(0).StringValue);
                Assert.Equal("penny" + Environment.NewLine, RegisterMap.GetRegister(1).StringValue);
            }
        }
    }
}
