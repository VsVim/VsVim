using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public class SubstituteConfirmTaggerSourceTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly Mock<ISubstituteConfirmMode> _mode;
        private readonly ITextBuffer _textBuffer;
        private readonly SubstituteConfirmTaggerSource _taggerSourceRaw;
        private readonly IBasicTaggerSource<TextMarkerTag> _taggerSource;

        public SubstituteConfirmTaggerSourceTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _mode = _factory.Create<ISubstituteConfirmMode>();
            _textBuffer = CreateTextBuffer("cat", "dog", "bird", "tree");
            _taggerSourceRaw = new SubstituteConfirmTaggerSource(_textBuffer, _mode.Object);
            _taggerSource = _taggerSourceRaw;
        }

        private void SetAndRaiseCurrentMatch(SnapshotSpan? span)
        {
            var option = span.HasValue
                ? FSharpOption.Create(span.Value)
                : FSharpOption<SnapshotSpan>.None;
            _mode
                .SetupGet(x => x.CurrentMatch)
                .Returns(option)
                .Verifiable();
            _mode.Raise(x => x.CurrentMatchChanged += null, null, option);
        }

        [Fact]
        public void GetTags_Initial()
        {
            Assert.False(_taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Fact]
        public void GetTags_WithCurrent()
        {
            var span = _textBuffer.GetLine(0).Extent;
            SetAndRaiseCurrentMatch(span);

            var tagSpan = _taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Single();
            Assert.Equal(span, tagSpan.Span);
        }

        [Fact]
        public void GetTags_WithCurrentReset()
        {
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            SetAndRaiseCurrentMatch(null);
            Assert.False(_taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Fact]
        public void Changed_OnCurrentChanged()
        {
            var didSee = false;
            _taggerSource.Changed += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            Assert.True(didSee);
        }

        [Fact]
        public void Changed_OnCurrentReset()
        {
            var didSee = false;
            _taggerSource.Changed += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(null);
            Assert.True(didSee);
        }
    }
}
