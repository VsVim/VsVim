using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class TextSelectionUtilTest : VimTestBase
    {
        private Mock<ITextSelection> _selection;

        public TextSelectionUtilTest()
        {
            _selection = new Mock<ITextSelection>(MockBehavior.Strict);
            _selection.SetupGet(x => x.IsEmpty).Returns(false);
        }

        /// <summary>
        /// No selection should equal no span
        /// </summary>
        [Fact]
        public void GetOverarchingSpan1()
        {
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            Assert.True(TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).IsNone());
            _selection.Verify();
        }

        /// <summary>
        /// No spans should have no overarching
        /// </summary>
        [Fact]
        public void GetOverarchingSpan2()
        {
            var spans = new NormalizedSnapshotSpanCollection();
            _selection.SetupGet(x => x.SelectedSpans).Returns(spans).Verifiable();
            Assert.True(TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).IsNone());
            _selection.Verify();
        }

        [Fact]
        public void GetOverarchingSpan3()
        {
            var buffer = CreateTextBuffer("foo bar");
            var span = buffer.GetLineRange(0).Extent;
            var col = new NormalizedSnapshotSpanCollection(span);
            _selection.SetupGet(x => x.SelectedSpans).Returns(col).Verifiable();
            Assert.Equal(span, TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).Value);
            _selection.Verify();
        }

        [Fact]
        public void GetOverarchingSpan4()
        {
            var buffer = CreateTextBuffer("foo", "baz", "bar");
            var span1 = buffer.GetLineRange(0).Extent;
            var span2 = buffer.GetLineRange(0, 1).Extent;
            var col = new NormalizedSnapshotSpanCollection(new SnapshotSpan[] { span1, span2 });
            _selection.SetupGet(x => x.SelectedSpans).Returns(col).Verifiable();
            Assert.Equal(buffer.GetLineRange(0, 1).Extent, TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).Value);
            _selection.Verify();
        }

    }
}
