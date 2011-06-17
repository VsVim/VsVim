using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.SubstituteConfirm;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SubstituteConfirmTaggerTest
    {
        private MockRepository _factory;
        private Mock<ISubstituteConfirmMode> _mode;
        private ITextBuffer _textBuffer;
        private SubstituteConfirmTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _mode = _factory.Create<ISubstituteConfirmMode>();
            _textBuffer = EditorUtil.CreateTextBuffer("cat", "dog", "bird", "tree");
            _taggerRaw = new SubstituteConfirmTagger(_textBuffer, _mode.Object);
            _tagger = _taggerRaw;
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
            Assert.IsFalse(_taggerRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Test]
        public void GetTags_WithCurrent()
        {
            var span = _textBuffer.GetLine(0).Extent;
            SetAndRaiseCurrentMatch(span);

            var tagSpan = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection()).Single();
            Assert.AreEqual(span, tagSpan.Span);
        }

        [Test]
        public void GetTags_WithCurrentReset()
        {
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            SetAndRaiseCurrentMatch(null);
            Assert.IsFalse(_taggerRaw.GetTags(new NormalizedSnapshotSpanCollection()).Any());
        }

        [Test]
        public void TagsChanged_OnCurrentChanged()
        {
            var didSee = false;
            _tagger.TagsChanged += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(_textBuffer.GetLine(0).Extent);
            Assert.IsTrue(didSee);
        }

        [Test]
        public void TagsChanged_OnCurrentReset()
        {
            var didSee = false;
            _tagger.TagsChanged += delegate { didSee = true; };
            SetAndRaiseCurrentMatch(null);
            Assert.IsTrue(didSee);
        }
    }
}
