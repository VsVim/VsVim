using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.UnitTest;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public class WpfIntegrationTest : VimTestBase
    {
        protected VimKeyProcessor _vimKeyProcessor;
        protected KeyboardInputSimulation _simulation;
        protected IVimBuffer _vimBuffer;
        protected ITextBuffer _textBuffer;

        public WpfIntegrationTest()
        {

        }

        protected void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textBuffer = _vimBuffer.TextBuffer;
            _vimKeyProcessor = new VimKeyProcessor(_vimBuffer, KeyUtil);
            _simulation = new KeyboardInputSimulation((IWpfTextView)_vimBuffer.TextView);
            _simulation.KeyProcessors.Add(_vimKeyProcessor);
        }

        public sealed class NormalTest : WpfIntegrationTest
        {
            [Fact]
            public void SimpleDelete()
            {
                Create("cat", "dog");
                _simulation.Run("dd");
                Assert.Equal(new[] { "dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void HandleEscape()
            {
                Create("cat");
                _simulation.RunNotation("i");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                _simulation.RunNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }
        }

        public sealed class InsertTest : WpfIntegrationTest
        {
            [Fact]
            public void AltInput()
            {
                Create("");
                _simulation.Run("iÁ");
                Assert.Equal("Á", _textBuffer.GetLine(0).GetText());
            }
        }
    }
}
