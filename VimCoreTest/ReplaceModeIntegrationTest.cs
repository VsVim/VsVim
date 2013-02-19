using EditorUtils;
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

        protected void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
        }

        public sealed class RepeatEdit : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Repeat of a replace mode edit should result in a replacement not an insert
            /// </summary>
            [Fact]
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
            [Fact]
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
            [Fact]
            public void InsertTextAsCommand()
            {
                Create("dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("catcat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            [Fact]
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
            [Fact]
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

        public sealed class Misc : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Typing forward in replace mode should overwrite 
            /// </summary>
            [Fact]
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
            [Fact]
            public void TypePastEndOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                _vimBuffer.Process("big tree");
                Assert.Equal("big tree", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
            }
        }
    }
}
