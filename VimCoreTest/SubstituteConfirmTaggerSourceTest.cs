using System.Linq;
using EditorUtils;
using EditorUtils.UnitTest;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public class SubstituteConfirmTaggerSourceTest : VimTestBase
    {
        private MockRepository _factory;
        private Mock<ISubstituteConfirmMode> _mode;
        private ITextBuffer _textBuffer;
        private SubstituteConfirmTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<TextMarkerTag> _taggerSource;

        [SetUp]
        public void SetUp()
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

        [Test]
        public void GetTags_Initial()
        {
            Assert.IsFalse(_taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Test]
        public void GetTags_WithCurrent()
        {
            var span = _textBuffer.GetLine(0).Extent;
            SetAndRaiseCurrentMatch(span);

            var tagSpan = _taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Single();
            Assert.AreEqual(span, tagSpan.Span);
        }

        [Test]
        public void GetTags_WithCurrentReset()
        {
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            SetAndRaiseCurrentMatch(null);
            Assert.IsFalse(_taggerSourceRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Test]
        public void Changed_OnCurrentChanged()
        {
            var didSee = false;
            _taggerSource.Changed += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            Assert.IsTrue(didSee);
        }

        [Test]
        public void Changed_OnCurrentReset()
        {
            var didSee = false;
            _taggerSource.Changed += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(null);
            Assert.IsTrue(didSee);
        }
    }
}
