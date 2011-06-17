using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.ExternalEdit;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class ExternalEditMonitorTest : VimTestBase
    {
        private MockRepository _factory;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<IExternalEditAdapter> _adapter;
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
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _buffer = MockObjectFactory.CreateVimBuffer(textView: _textView);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false);

            // Have adatper ignore by default
            _adapter = _factory.Create<IExternalEditAdapter>(MockBehavior.Strict);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(false);
            _adapter.Setup(x => x.IsExternalEditMarker(It.IsAny<IVsTextLineMarker>())).Returns(false);

            _aggregator = _factory.Create<ITagAggregator<ITag>>(MockBehavior.Strict);
            SetupTags();

            Result<IVsTextLines> result;
            if (isShimmed)
            {
                _vsTextLines = _factory.Create<IVsTextLines>();
                _vsTextLines.SetupNoEnumMarkers();
                result = Result.CreateSuccess(_vsTextLines.Object);
            }
            else
            {
                result = Result.Error;
            }

            var list = new List<IExternalEditAdapter> { _adapter.Object };
            var adapters = new ReadOnlyCollection<IExternalEditAdapter>(list);
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

        private void SetupMarkers(int tagType, params SnapshotSpan[] spans)
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
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
        }

        private void SetupAdapterCreateMarkerAsSnippet()
        {
            _adapter
                .Setup(x => x.IsExternalEditMarker(It.IsAny<IVsTextLineMarker>()))
                .Returns(true);
        }

        private void SetupExternalEditTag(SnapshotSpan span)
        {
            SetupTags(span);
            SetupAdapterCreateTagAsSnippet();
        }

        private void RaiseLayoutChanged()
        {
            var methodInfo = _textView.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x => x.Name == "RaiseLayoutChangeEvent")
                .Where(x => x.GetParameters().Length == 0)
                .Single();
            methodInfo.Invoke(_textView, null);
        }

        [Test]
        public void GetExternalEditSpans_WithIgnoredTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            var result = _monitor.GetExternalEditSpans();
            Assert.AreEqual(0, result.Count);
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditSpans_WithConsumedTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            SetupAdapterCreateTagAsSnippet();
            var result = _monitor.GetExternalEditSpans();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(span, result.Single());
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditSpans_WithIgnoredMarkers()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupMarkers(15, span);
            var result = _monitor.GetExternalEditSpans();
            Assert.AreEqual(0, result.Count);
            _factory.Verify();
        }

        [Test]
        public void GetExternalEditSpans_WithConsumedMarkers()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupMarkers(15, span);
            SetupAdapterCreateMarkerAsSnippet();
            var result = _monitor.GetExternalEditSpans();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(span, result.Single());
        }

        [Test]
        public void SwitchMode_NoActionOutsideExternalEdit()
        {
            Setup("cat", "tree", "dog");
            var mode = _factory.Create<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            var args = new SwitchModeEventArgs(FSharpOption.Create(mode.Object), null);
            _buffer.Raise(x => x.SwitchedMode += null, null, args);
        }

        /// <summary>
        /// This is a very important test because we often see the transition to visual mode
        /// before the layout and hence would ignore valid edit tags
        /// </summary>
        [Test]
        public void SwitchMode_OldModeIsExternalThenSaveIgnoreTags()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            SetupAdapterCreateTagAsSnippet();
            var mode = _factory.Create<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.ExternalEdit).Verifiable();
            _buffer.Raise(x => x.SwitchedMode += null, null, new SwitchModeEventArgs(FSharpOption.Create(mode.Object), null));
            var list = _monitor.IgnoredMarkers.ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(span, list.Single());
            _factory.Verify();
        }

        [Test]
        public void SwitchMode_OldModeIsNotExternalThenSaveNothing()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupMarkers(15, span);
            SetupAdapterCreateMarkerAsSnippet();
            var mode = _factory.Create<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _buffer.Raise(x => x.SwitchedMode += null, null, new SwitchModeEventArgs(FSharpOption.Create(mode.Object), null));
            var list = _monitor.IgnoredMarkers.ToList();
            Assert.AreEqual(0, list.Count);
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_InExternalEditDoNothingIfEditMarkersStillPresent()
        {
            Setup("cat", "tree", "dog");
            SetupExternalEditTag(_textBuffer.GetLineRange(0).Extent);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.ExternalEdit).Verifiable();
            RaiseLayoutChanged();
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_InExternalEditSwitchToInsertIfNoMarkers()
        {
            Setup("cat", "tree", "dog");
            var span = _textBuffer.GetLineRange(0).Extent;
            SetupTags(span);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.ExternalEdit).Verifiable();
            _buffer.Setup(x => x.SwitchMode(ModeKind.Insert, ModeArgument.None)).Returns(_factory.Create<IMode>().Object).Verifiable();
            RaiseLayoutChanged();
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_VisibleLinesHaveNoMarkersShouldClearIgnored()
        {
            Setup("cat", "tree", "dog");
            _monitor.IgnoredMarkers = new List<SnapshotSpan> { _textBuffer.GetLine(0).Extent };
            RaiseLayoutChanged();
            Assert.AreEqual(0, _monitor.IgnoredMarkers.Count());
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_ShouldMoveIgnoreMarkersForward()
        {
            Setup("cat", "tree", "dog");
            _textBuffer.Replace(new Span(0, 0), "big ");
            var range = _textBuffer.GetLineRange(0);
            _monitor.IgnoredMarkers = new List<SnapshotSpan> { _textBuffer.GetLine(0).Extent };
            SetupExternalEditTag(range.Extent);
            RaiseLayoutChanged();
            Assert.AreEqual(range.Extent, _monitor.IgnoredMarkers.Single());
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_ExternalMarkersShouldTransitionToExternalEditMode()
        {
            Setup("cat", "tree", "dog");
            _buffer
                .Setup(x => x.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            SetupExternalEditTag(_textBuffer.GetLineRange(0).Extent);
            RaiseLayoutChanged();
            _factory.Verify();
        }

        [Test]
        public void LayoutChanged_IgnoredMarkersShouldBeIgnored()
        {
            Setup("cat", "tree", "dog");
            var range = _textBuffer.GetLineRange(0);
            SetupExternalEditTag(range.Extent);
            _monitor.IgnoredMarkers = new List<SnapshotSpan> { range.Extent };
            RaiseLayoutChanged();
        }

    }
}
