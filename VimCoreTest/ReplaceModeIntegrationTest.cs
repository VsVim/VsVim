using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;

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

        [TestFixture]
        public sealed class RepeatEdit : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Repeat of a replace mode edit should result in a replacement not an insert
            /// </summary>
            [Test]
            public void Simple()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation(@"<S-R>abc<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                CollectionAssert.AreEqual(
                    new string[] { "abc", "abc" },
                    _textBuffer.GetLines());
                Assert.AreEqual(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.IsFalse(_textView.Options.GetOptionValue(DefaultTextViewOptions.OverwriteModeId));
            }

            /// <summary>
            /// Repeat of a replace mode edit on a shorter line should extend the line to encompass
            /// the value
            /// </summary>
            [Test]
            public void ShorterLine()
            {
                Create("cat", "d");
                _vimBuffer.ProcessNotation(@"<S-R>abc<Esc>");
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process('.');
                CollectionAssert.AreEqual(
                    new string[] { "abc", "abc" },
                    _textBuffer.GetLines());
                Assert.AreEqual(_textBuffer.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.IsFalse(_textView.Options.GetOptionValue(DefaultTextViewOptions.OverwriteModeId));
            }
        }

        [TestFixture]
        public sealed class ReplaceWithCount : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Replace mode should respect the insert count 
            /// </summary>
            [Test]
            public void InsertTextAsCommand()
            {
                Create("dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.AreEqual("catcat", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(5, _textView.GetCaretPoint().Position);
            }

            [Test]
            public void InsertOver()
            {
                Create("fish tree");
                _vimBuffer.ProcessNotation(@"2<S-R>cat<Esc>");
                Assert.AreEqual("catcatree", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When repeating the insert it should write over the remaining text vs. inserting
            /// </summary>
            [Test]
            public void InsertOverAsCommand()
            {
                Create("fish tree");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
                _vimBuffer.Process("cat");
                _vimBuffer.Process(VimKey.Escape);
                Assert.AreEqual("catcatree", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(5, _textView.GetCaretPoint().Position);
            }
        }

        [TestFixture]
        public sealed class Misc : ReplaceModeIntegrationTest
        {
            /// <summary>
            /// Typing forward in replace mode should overwrite 
            /// </summary>
            [Test]
            public void TypeForwardShouldReplace()
            {
                Create("hello world");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                _vimBuffer.Process("again");
                Assert.AreEqual("again world", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Typing past the end of the line should extend it
            /// </summary>
            [Test]
            public void TypePastEndOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
                _vimBuffer.Process("big tree");
                Assert.AreEqual("big tree", _textBuffer.GetLine(0).GetText());
                Assert.AreEqual("dog", _textBuffer.GetLine(1).GetText());
            }
        }
    }
}
