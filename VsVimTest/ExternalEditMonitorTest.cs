using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.ExternalEdit;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class ExternalEditMonitorTest
    {
        private MockRepository _factory;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<IExternalEditorAdapter> _adapter;
        private Mock<ITagAggregator<ITag>> _aggregator;
        private Mock<IVsTextLines> _vsTextLines;
        private ExternalEditMonitor _monitor;

        public void Setup(params string[] lines)
        {
            Setup(isShimmed: true, lines: lines);
        }

        public void Setup(bool isShimmed = true, params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _textView = MockObjectFactory.CreateTextView(textBuffer: _textBuffer);
            _buffer = MockObjectFactory.CreateVimBuffer(textView: _textView.Object);
            _adapter = _factory.Create<IExternalEditorAdapter>(MockBehavior.Strict);
            _aggregator = _factory.Create<ITagAggregator<ITag>>(MockBehavior.Strict);

            Result<IVsTextLines> result;
            if (isShimmed)
            {
                _vsTextLines = _factory.Create<IVsTextLines>();
                _vsTextLines.SetupNoEnumMarkers();
                result = Result.CreateValue(_vsTextLines.Object);
            }
            else
            {
                result = Result.CreateError();
            }

            var list = new List<IExternalEditorAdapter> {_adapter.Object};
            var adapters = new ReadOnlyCollection<IExternalEditorAdapter>(list);
            _monitor = new ExternalEditMonitor(
                _buffer.Object,
                result,
                adapters,
                _aggregator.Object);
        }

        private void SetupTags(params SnapshotSpan[] tagSpans)
        {
            var list = new List<IMappingTagSpan<ITag>>();
            foreach (var tagSpan in tagSpans)
            {
                var mappingTagSpan = MockObjectFactory.CreateMappingTagSpan(
                    tagSpan,
                    _factory.Create<ITag>().Object,
                    _factory);
                list.Add(mappingTagSpan.Object);
            }

            _aggregator.Setup(x => x.GetTags(It.IsAny<SnapshotSpan>())).Returns(list);
        }

        private void SetupTextMarkers(int tagType, params SnapshotSpan[] spans)
        {
            var list = new List<IVsTextLineMarker>();
            foreach (var span in spans)
            {
                var textSpan = span.ToTextSpan();
                list.Add(MockObjectFactory.CreateVsTextLineMarker(textSpan, tagType, _factory).Object);
            }

            var markerEnum = new MockEnumLineMarkers(list);
            _vsTextLines.SetupEnumMarkers(markerEnum);
        }

        [Test]
        public void GetExternalEditMarkers_WithIgnoredTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            _adapter.Setup(x => x.TryCreateExternalEditMarker(It.IsAny<ITag>(), span)).Returns((ExternalEditMarker?) null).Verifiable();
            var result = _monitor.GetExternalEditMarkers(_textBuffer.GetExtent());
            Assert.AreEqual(0, result.Count);
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditMarkers_WithConsumedTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            _adapter
                .Setup(x => x.TryCreateExternalEditMarker(It.IsAny<ITag>(), span))
                .Returns(new ExternalEditMarker(ExternalEditKind.Snippet, span))
                .Verifiable();
            var result = _monitor.GetExternalEditMarkers(_textBuffer.GetExtent());
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(span, result.Single().Span);
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditMarkers_WithIgnoredMarkers()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTextMarkers(15, span);
            SetupTags();
            _adapter
                .Setup(x => x.TryCreateExternalEditMarker(It.IsAny<IVsTextLineMarker>(), _textBuffer.CurrentSnapshot))
                .Returns((ExternalEditMarker?)null)
                .Verifiable();
            var result = _monitor.GetExternalEditMarkers(_textBuffer.GetExtent());
            Assert.AreEqual(0, result.Count);
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditMarkers_WithConsumedMarkers()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTextMarkers(15, span);
            SetupTags();
            _adapter
                .Setup(x => x.TryCreateExternalEditMarker(It.IsAny<IVsTextLineMarker>(), _textBuffer.CurrentSnapshot))
                .Returns(new ExternalEditMarker(ExternalEditKind.Snippet, span))
                .Verifiable();
            var result = _monitor.GetExternalEditMarkers(_textBuffer.GetExtent());
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(span, result.Single().Span);
        }

        [Test]
        public void SwitchModeNoActionOutsideExternalEdit()
        {
            Setup("cat", "tree", "dog");
            _buffer.Raise(x => x.SwitchedMode += null, null, _factory.Create<IMode>().Object);
        }

    }
}
