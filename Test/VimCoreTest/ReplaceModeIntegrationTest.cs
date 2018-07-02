using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class ReplaceModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;

        protected void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _globalSettings = Vim.GlobalSettings;
        }

        public sealed class RepeatEdit : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Repeat of a replace mode edit should result in a replacement not an insert
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation(@"<S-R>abc<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal(
                    new string[] { "abc", "abc" },
                    _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.False(_textView.Options.GetOptionValue(DefaultTextViewOptions.OverwriteModeId));
            }

            /// <summary>
            /// Repeat of a replace mode edit on a shorter line should extend the line to encompass
            /// the value
            /// </summary>
            [WpfFact]
            public void ShorterLine()
            {
                Create("cat", "d");
                _vimBuffer.ProcessNotation(@"<S-R>abc<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                Assert.Equal(
                    new string[] { "abc", "abc" },
                    _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.False(_textView.Options.GetOptionValue(DefaultTextViewOptions.OverwriteModeId));
            }
        }

        public sealed class ReplaceWithCount : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Replace mode should respect the insert count 
            /// </summary>
            [WpfFact]
            public void InsertTextAsCommand()
            {
                Create("dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("catcat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void InsertOver()
            {
                Create("fish tree");
                _vimBuffer.ProcessNotation(@"2<S-R>cat<Esc>");
                Assert.Equal("catcatree", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When repeating the insert it should write over the remaining text vs. inserting
            /// </summary>
            [WpfFact]
            public void InsertOverAsCommand()
            {
                Create("fish tree");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("catcatree", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class ReplaceUndoTest : ReplaceModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat dog bat rat", "cow gnu fox yak");
                _textView.MoveCaretTo(4);
                _vimBuffer.ProcessNotation("<S-R>DOG BAT<BS><BS><BS>");
                Assert.Equal("cat DOG bat rat", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void BackspaceToPreviousLine()
            {
                Create("cat dog bat rat", "cow gnu fox yak");
                _globalSettings.Backspace = "indent,eol,start";
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<S-R><BS><BS><BS><BS><BS><BS><BS><BS>BAT");
                Assert.Equal("cat dog BAT rat", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void BackspaceAfterMoving()
            {
                Create("cat dog bat rat", "cow gnu fox yak");
                _vimBuffer.ProcessNotation("<S-R>CAT<Right><Left><BS><BS><BS>");
                Assert.Equal("CAT dog bat rat", _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class ReplaceCharacterAboveTest : ReplaceModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal("cog", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void Multiple()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<S-R>");
                for (var i = 0; i < 3; i++)
                {
                    _vimBuffer.ProcessNotation("<C-y>");
                }
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void NothingAbove()
            {
                Create("", "dog");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [WpfFact]
            public void FirstLine()
            {
                Create("", "dog");
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-y>");
                Assert.Equal(1, VimHost.BeepCount);
            }
        }

        public sealed class ReplaceCharacterBelowTest : ReplaceModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal("dat", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void Multiple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("<S-R>");
                for (var i = 0; i < 3; i++)
                {
                    _vimBuffer.ProcessNotation("<C-e>");
                }
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            [WpfFact]
            public void NothingBelow()
            {
                Create("cat", "");
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal(1, VimHost.BeepCount);
            }

            [WpfFact]
            public void LastLine()
            {
                Create("cat", "");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation("<S-R>");
                _vimBuffer.ProcessNotation("<C-e>");
                Assert.Equal(1, VimHost.BeepCount);
            }
        }

        public sealed class Misc : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Typing forward in replace mode should overwrite 
            /// </summary>
            [WpfFact]
            public void TypeForwardShouldReplace()
            {
                Create("hello world");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                _vimBuffer.Process("again");
                Assert.Equal("again world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Typing past the end of the line should extend it
            /// </summary>
            [WpfFact]
            public void TypePastEndOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                _vimBuffer.Process("big tree");
                Assert.Equal("big tree", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void PasteOverwritesFullLengthOfRegister()
            {
                Create("12345 world");
                UnnamedRegister.UpdateValue("hello");
                _vimBuffer.ProcessNotation("<S-R>");
                Assert.Equal(ModeKind.Replace, _vimBuffer.Mode.ModeKind);
                _vimBuffer.ProcessNotation("<C-r>\"");
                Assert.Equal("hello world", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
                Assert.Equal("hello", UnnamedRegister.StringValue);
            }

            [WpfFact]
            public void PasteOverwritesFullLengthOfRegisterPastEndOfLine()
            {
                Create("123");
                UnnamedRegister.UpdateValue("hello");
                _vimBuffer.ProcessNotation("<S-R>");
                Assert.Equal(ModeKind.Replace, _vimBuffer.Mode.ModeKind);
                _vimBuffer.ProcessNotation("<C-r>\"");
                Assert.Equal("hello", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
                Assert.Equal("hello", UnnamedRegister.StringValue);
            }
        }
    }
}
