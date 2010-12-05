using System;
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

            // Have adatper ignore by default
            _adapter = _factory.Create<IExternalEditorAdapter>(MockBehavior.Strict);
            _adapter.Setup(x => x.TryCreateExternalEditMarker(It.IsAny<ITag>(), It.IsAny<SnapshotSpan>())).Returns((ExternalEditMarker?) null);
            _adapter.Setup(x => x.TryCreateExternalEditMarker(It.IsAny<IVsTextLineMarker>(), It.IsAny<ITextSnapshot>())).Returns((ExternalEditMarker?) null);

            _aggregator = _factory.Create<ITagAggregator<ITag>>(MockBehavior.Strict);
            SetupTags();

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

        private void SetupAdapterCreateTagAsSnippet()
        {
            _adapter
                .Setup(x => x.TryCreateExternalEditMarker(It.IsAny<ITag>(), It.IsAny<SnapshotSpan>()))
                .Returns<ITag, SnapshotSpan>((_, span) => new ExternalEditMarker(ExternalEditKind.Snippet, span));
        }

        private void SetupAdapterCreateMarkerAsSnippet()
        {
            _adapter
                .Setup(x => x.TryCreateExternalEditMarker(It.IsAny<IVsTextLineMarker>(), It.IsAny<ITextSnapshot>()))
                .Returns<IVsTextLineMarker, ITextSnapshot>((marker, snapshot) =>
                   {
                       var span = marker.GetCurrentSpan(snapshot).Value;
                       return new ExternalEditMarker(ExternalEditKind.Snippet, span);
                   });
        }

        [Test]
        public void GetExternalEditMarkers_WithIgnoredTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
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
            SetupAdapterCreateTagAsSnippet();
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
            SetupAdapterCreateMarkerAsSnippet();
            var result = _monitor.GetExternalEditMarkers(_textBuffer.GetExtent());
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(span, result.Single().Span);
        }

        [Test]
        public void SwitchMode_NoActionOutsideExternalEdit()
        {
            Setup("cat", "tree", "dog");
            _buffer.Raise(x => x.SwitchedMode += null, null, _factory.Create<IMode>().Object);
        }

        [Test]
        public void SwitchMode_InExternalEditSaveTagsAsIgnore()
        {
            Setup("cat", "tree", "dog");
            _monitor.InExternalEdit = true;
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            SetupAdapterCreateTagAsSnippet();
            _buffer.Raise(x => x.SwitchedMode += null, null, null);
            var list = _monitor.IgnoredMarkers.ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(span, list.Single().Span);
        }

        [Test]
        public void SwitchMode_InExternalEditSaveMarkersAsIgnore()
        {
            Setup("cat", "tree", "dog");
            _monitor.InExternalEdit = true;
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTextMarkers(15, span);
            SetupAdapterCreateMarkerAsSnippet();
            _buffer.Raise(x => x.SwitchedMode += null, null, null);
            var list = _monitor.IgnoredMarkers.ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(span, list.Single().Span);
        }

        [Test]
        public void LayoutChanged_InExternalEditDoNothing()
        {
            Setup("cat", "tree", "dog");
            _monitor.InExternalEdit = true;
            _textView.Raise(x => x.LayoutChanged += null, null, null);
        }

        [Test]
        public void LayoutChanged_InRecursiveLayoutDoNothing()
        {
            Setup("cat", "tree", "dog");
            _textView.SetupGet(x => x.TextViewLines).Throws(new InvalidOperationException()).Verifiable();
            _textView.Raise(x => x.LayoutChanged += null, null, null);
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_VisibleLinesHaveNoMarkersShouldClearIgnored()
        {
            Setup("cat", "tree", "dog");
            _monitor.IgnoredMarkers = new List<ExternalEditMarker> {new ExternalEditMarker(ExternalEditKind.Snippet, _textBuffer.GetLine(0).Extent)};
            var range = _textBuffer.GetLineRange(0);
            var col = MockObjectFactory.CreateTextViewLineCollection(range, _factory);
            _textView.SetupGet(x => x.TextViewLines).Returns(col.Object).Verifiable();
            _textView.Raise(x => x.LayoutChanged += null, null, null);
            Assert.AreEqual(0, _monitor.IgnoredMarkers.Count());
            _factory.Verify();
        }

    }
}
