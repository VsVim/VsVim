using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class SelectModeIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextView _textView;
        protected ITextBuffer _textBuffer;
        protected ITextSelection _textSelection;
        protected IVimGlobalSettings _globalSettings;
        protected TestableSynchronizationContext _context;

        protected void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textBuffer = _vimBuffer.TextBuffer;
            _globalSettings = _vimBuffer.GlobalSettings;
            _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
            _textSelection = _textView.Selection;
            _context = new TestableSynchronizationContext();
            _context.Install();
        }

        protected void EnterSelect(int start, int length)
        {
            var span = new SnapshotSpan(_textBuffer.CurrentSnapshot, start, length);
            _textView.SelectAndMoveCaret(span);
            _context.RunAll();
            Assert.Equal(ModeKind.Select, _vimBuffer.ModeKind);
        }

        public sealed class Enter : SelectModeIntegrationTest
        {
            [Fact]
            public void SelectOfText()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.Equal(ModeKind.Select, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Extending the selection should just ask select mode to reset it's information
            /// </summary>
            [Fact]
            public void ExtendSelection()
            {
                Create("cat dog");
                _textSelection.Select(0, 3);
                _context.RunAll();
                Assert.Equal(ModeKind.Select, _vimBuffer.ModeKind);
                _textSelection.Select(0, 5);
                Assert.False(_context.IsEmpty);
                _context.RunAll();
                Assert.Equal(ModeKind.Select, _vimBuffer.ModeKind);
            }
        }

        public sealed class Edit : SelectModeIntegrationTest
        {
            [Fact]
            public void SimpleAlpha()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('o');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("To time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void SimpleNumeric()
            {
                Create("Test time");
                EnterSelect(1, 3);
                _vimBuffer.Process('3');
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("T3 time", _textBuffer.GetLine(0).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void SimpleEnter()
            {
                Create("dog go time");
                EnterSelect(4, 3);
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal(new[] { "dog ", "time" }, _textBuffer.GetLines());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// The delete key should delete the selection and not insert any text into the 
            /// line
            ///
            /// issue #911
            /// </summary>
            [Fact]
            public void DeleteKey()
            {
                Create("dog cat bear");
                EnterSelect(0, 4);
                _vimBuffer.Process(VimKey.Delete);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The backspace key should delete the selection and not insert any text into the 
            /// line
            /// </summary>
            [Fact]
            public void BackspaceKey()
            {
                Create("dog cat bear");
                EnterSelect(0, 4);
                _vimBuffer.Process(VimKey.Back);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("cat bear", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class Misc : SelectModeIntegrationTest
        {
            /// <summary>
            /// Make sure that escape will leave select mode
            /// </summary>
            [Fact]
            public void Escape()
            {
                Create("cat dog");
                EnterSelect(0, 3);
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }
        }
    }
}
