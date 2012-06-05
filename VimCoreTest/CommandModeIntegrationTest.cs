using System;
using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    public abstract class CommandModeIntegrationTestBase : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected MockVimHost _vimHost;
        protected string _lastStatus;

        public void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _vimBuffer.StatusMessage += (sender, args) => { _lastStatus = args.Message; };
            _textView = _vimBuffer.TextView;
            _textBuffer = _textView.TextBuffer;
            _vimHost = VimHost;
        }

        protected void RunCommand(string command)
        {
            _vimBuffer.Process(':');
            _vimBuffer.Process(command, enter: true);
        }

        protected void RunCommandRaw(string command)
        {
            _vimBuffer.Process(command, enter: true);
        }
    }

    /// <summary>
    /// Summary description for CommandModeTest
    /// </summary>
    public sealed class CommandModeIntegrationTest : CommandModeIntegrationTestBase
    {
        public sealed class CopyToTests : CommandModeIntegrationTestBase
        {
            /// <summary>
            /// Copying a line to a given line should put it at that given line
            /// </summary>
            [Fact]
            public void ItDisplacesToTheLineBelowWhenTargetedAtCurrentLine()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 1");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [Fact]
            public void ItCanJumpLongRanges()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Check the copy command via the 't' synonym
            /// </summary>
            [Fact]
            public void The_t_SynonymWorksAlso()
            {
                Create("cat", "dog", "bear");
                RunCommand("t 2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Copying a line to a range should cause it to copy to the first line 
            /// in the range
            /// </summary>
            [Fact]
            public void CopyingASingleLineToARangeDuplicatesTheLine()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 1,2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
            }

            [Fact]
            public void PositiveRelativeReferencesUsingDotWork()
            {
                Create("cat", "dog", "bear");
                _textView.MoveCaretToLine(1);
                RunCommand("co .");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [Fact]
            public void PositiveRelativeReferencesWork()
            {
                Create("cat", "dog", "bear");
                RunCommand("co +1");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [Fact]
            public void NegativeRelativeReferencesWork()
            {
                // Added goose to simplify this test case. Look further for an issue with last line endlines 
                Create("cat", "dog", "bear", "goose");
                _textView.MoveCaretToLine(2);
                RunCommand("co -2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [Fact]
            public void CopyingPastLastLineInsertsAnImplicitNewline()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 3");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(2).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(3).GetText());
            }

        }

        public sealed class MoveToTests : CommandModeIntegrationTestBase
        {

            [Fact]
            public void SimpleCaseOfMovingLineOneBelow()
            {
                Create("cat", "dog", "bear");

                RunCommand("m 2");
                Assert.Equal(_textBuffer.GetLine(0).GetText(), "dog");
                Assert.Equal(_textBuffer.GetLine(1).GetText(), "cat");
                Assert.Equal(_textBuffer.GetLine(2).GetText(), "bear");
            }

            /// <summary>
            /// The last line in the file seems to be an exception because it doesn't have a 
            /// newline at the end
            /// </summary>
            [Fact]
            public void MoveToLastLineInFile()
            {
                Create("cat", "dog", "bear");

                RunCommand("m 3");
                Assert.Equal(_textBuffer.GetLine(0).GetText(), "dog");
                Assert.Equal(_textBuffer.GetLine(1).GetText(), "bear");
                Assert.Equal(_textBuffer.GetLine(2).GetText(), "cat");
            }

        }

        [Fact]
        public void JumpLine1()
        {
            Create("a", "b", "c", "d");
            RunCommand("0");
            Assert.Equal(0, _textView.Caret.Position.BufferPosition.Position);
            RunCommand("1");
            Assert.Equal(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Fact]
        public void JumpLine2()
        {
            Create("a", "b", "c", "d");
            RunCommand("2");
            Assert.Equal(_textView.TextSnapshot.GetLineFromLineNumber(1).Start, _textView.Caret.Position.BufferPosition);
        }

        [Fact]
        public void JumpLineLastWithNoWhiteSpace()
        {
            Create("dog", "cat", "tree");
            RunCommand("$");
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.Equal(last.Start, _textView.GetCaretPoint());
        }

        [Fact]
        public void JumpLineLastWithWhiteSpace()
        {
            Create("dog", "cat", "  tree");
            RunCommand("$");
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.Equal(last.Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Make sure that we don't crash or print anything when :map is run with no mappings
        /// </summary>
        [Fact]
        public void KeyMap_NoMappings()
        {
            Create("");
            RunCommand("map");
            Assert.Equal("", _lastStatus);
        }

        /// <summary>
        /// In Vim it's legal to unmap a key command with the expansion
        /// </summary>
        [Fact]
        public void KeyMap_UnmapByExpansion()
        {
            Create("");
            RunCommand("imap cat dog");
            Assert.Equal(1, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
            RunCommand("iunmap dog");
            Assert.Equal(0, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
        }

        /// <summary>
        /// The ! in unmap should cause it to umap command and insert commands.  Make sure it
        /// works for unmap by expansion as well
        /// </summary>
        [Fact]
        public void KeyMap_UnmapByExpansionUsingBang()
        {
            Create("");
            RunCommand("imap cat dog");
            Assert.Equal(1, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
            RunCommand("unmap! dog");
            Assert.Equal(0, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
        }

        /// <summary>
        /// Suppress errors shouldn't print anything
        /// </summary>
        [Fact]
        public void Substitute1()
        {
            Create("cat", "dog");
            var sawError = false;
            _vimBuffer.ErrorMessage += delegate { sawError = true; };
            RunCommand("s/z/o/e");
            Assert.False(sawError);
        }

        /// <summary>
        /// Simple search and replace
        /// </summary>
        [Fact]
        public void Substitute2()
        {
            Create("cat bat", "dag");
            RunCommand("s/a/o/g 2");
            Assert.Equal("cot bot", _textBuffer.GetLine(0).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// Repeat of the last search with a new flag
        /// </summary>
        [Fact]
        public void Substitute3()
        {
            Create("cat bat", "dag");
            _vimBuffer.VimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "o", SubstituteFlags.None));
            RunCommand("s g 2");
            Assert.Equal("cot bot", _textBuffer.GetLine(0).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// Testing the print option
        /// </summary>
        [Fact]
        public void Substitute4()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
            RunCommand("s/a/b/p");
            Assert.Equal("cbt bat", message);
        }

        /// <summary>
        /// Testing the print number option
        /// </summary>
        [Fact]
        public void Substitute6()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
            RunCommand("s/a/b/#");
            Assert.Equal("  1 cbt bat", message);
        }

        /// <summary>
        /// Testing the print list option
        /// </summary>
        [Fact]
        public void Substitute7()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
            RunCommand("s/a/b/l");
            Assert.Equal("cbt bat$", message);
        }

        /// <summary>
        /// Verify we handle escaped back slashes correctly
        /// </summary>
        [Fact]
        public void Substitute_WithBackslashes()
        {
            Create(@"\\\\abc\\\\def");
            RunCommand(@"s/\\\{4\}/\\\\/g");
            Assert.Equal(@"\\abc\\def", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Convert a set of spaces into tabs with the '\t' replacement
        /// </summary>
        [Fact]
        public void Substitute_TabsForSpaces()
        {
            Create("    ");
            RunCommand(@"s/  /\t");
            Assert.Equal("\t  ", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Convert spaces into new lines with the '\r' replacement
        /// </summary>
        [Fact]
        public void Substitute_SpacesToNewLine()
        {
            Create("dog chases cat");
            RunCommand(@"s/ /\r/g");
            Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            Assert.Equal("chases", _textBuffer.GetLine(1).GetText());
            Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// Using the search forward feature which hits a match.  Search should start after the range
        /// so the first match will be after it 
        /// </summary>
        [Fact]
        public void Search_ForwardWithMatch()
        {
            Create("cat", "dog", "cat", "fish");
            RunCommand("1,2/cat");
            Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Fact]
        public void Substitute_DefaultsToMagicMode()
        {
            Create("a.c", "abc");
            RunCommand(@"%s/a\.c/replaced/g");
            Assert.Equal(_textBuffer.GetLine(0).GetText(), "replaced");
            Assert.Equal(_textBuffer.GetLine(1).GetText(), "abc");
        }

        /// <summary>
        /// Make sure the "\1" does a group substitution instead of pushing in the literal 1
        /// </summary>
        [Fact]
        public void Substitute_ReplaceWithGroup()
        {
            Create(@"cat (dog)");
            RunCommand(@"s/(\(\w\+\))/\1/");
            Assert.Equal(@"cat dog", _textBuffer.GetLine(0).GetText());
        }

        [Fact]
        public void Substitute_NewlinesCanBeReplaced()
        {
            Create("foo", "bar");
            RunCommand(@"%s/\n/ /");
            Assert.Equal(_textBuffer.GetLine(0).GetText(), "foo bar");
        }

        /// <summary>
        /// Using the search forward feature which doesn't hit a match in the specified path.  Should 
        /// raise a warning
        /// </summary>
        [Fact]
        public void Search_ForwardWithNoMatchInPath()
        {
            Create("cat", "dog", "cat", "fish");
            var didHit = false;
            _vimBuffer.LocalSettings.GlobalSettings.WrapScan = false;
            _vimBuffer.ErrorMessage +=
                (sender, args) =>
                {
                    Assert.Equal(Resources.Common_SearchHitBottomWithout("cat"), args.Message);
                    didHit = true;
                };
            RunCommand("1,3/cat");
            Assert.True(didHit);
        }

        /// <summary>
        /// No match in the buffer should raise a different message
        /// </summary>
        [Fact]
        public void Search_ForwardWithNoMatchInBuffer()
        {
            Create("cat", "dog", "cat", "fish");
            var didHit = false;
            _vimBuffer.ErrorMessage +=
                (sender, args) =>
                {
                    Assert.Equal(Resources.Common_PatternNotFound("pig"), args.Message);
                    didHit = true;
                };
            RunCommand("1,2/pig");
            Assert.True(didHit);
        }

        /// <summary>
        /// Covers #763 where the default search for substitute uses the last substitute
        /// instead of the last search
        /// </summary>
        [Fact]
        public void SubstituteThenSearchThenSubstitute_UsesPatternFromLastSearch()
        {
            Create("foo", "bar");
            
            RunCommandRaw(":%s/foo/foos");
            RunCommandRaw("/bar");
            RunCommandRaw(":%s//baz");

            Assert.Equal(_textBuffer.GetLine(1).Extent.GetText(), "baz");
        }

        [Fact]
        public void SubstituteThenSearchThenSubstitute_UsesPatternFromLastSubstitute()
        {
            Create("foo foo foo");
            
            RunCommandRaw(":%s/foo/bar");
            RunCommandRaw("/bar");
            // Do same substitute as the last substitute, but global this time
            RunCommandRaw(":%&g");

            Assert.Equal(_textBuffer.GetLine(0).Extent.GetText(), "bar bar bar");
        }

        /// <summary>
        /// Baseline to make sure I don't break anything while fixing #763
        /// </summary>
        [Fact]
        public void SubstituteThenSubstitute_UsesPatternFromLastSubstitute()
        {
            Create("foo", "bar");
            
            RunCommandRaw(":%s/foo/foos");
            RunCommandRaw(":%s//baz");

            Assert.Equal(_textBuffer.GetLine(0).Extent.GetText(), "bazs");
        }

        [Fact]
        public void SwitchTo()
        {
            Create("");
            _vimBuffer.Process(':');
            Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
        }

        [Fact]
        public void SwitchOut()
        {
            Create("");
            RunCommand("e foo");
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        [Fact]
        public void SwitchOutFromBackspace()
        {
            Create("");
            _vimBuffer.Process(':');
            _vimBuffer.Process(VimKey.Back);
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        [Fact]
        public void Yank_WithRange()
        {
            Create("cat", "dog", "fish");
            _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 0);
            _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 1, 0);
            RunCommand("'a,'by");
            Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, Vim.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
        }

    }
}
