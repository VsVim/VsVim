using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.ExternalEdit;
using Vim.VisualStudio.UnitTest.Mock;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// Tests for the ExternalEditorMonitor implementation.  Need to really hammer the scenarios here
    /// as this component in past forms is a frequent source of user hangs
    /// </summary>
    public abstract class ExternalEditMonitorTest : VimTestBase
    {
        private MockRepository _factory;
        private IVimBuffer _buffer;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private Mock<IExternalEditAdapter> _adapter;
        private Mock<ITagger<ITag>> _tagger;
        private Mock<IVsTextLines> _vsTextLines;
        private Mock<IVimApplicationSettings> _vimApplicationSettings;
        private ExternalEditMonitor _monitor;

        public void Create(params string[] lines)
        {
            Create(true, true, lines);
        }

        public void Create(bool hasTextLines, bool hasTagger, params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _buffer = Vim.CreateVimBuffer(_textView);
            _vimApplicationSettings = _factory.Create<IVimApplicationSettings>();
            _vimApplicationSettings.SetupGet(x => x.EnableExternalEditMonitoring).Returns(true);

            // Have adatper ignore by default
            _adapter = _factory.Create<IExternalEditAdapter>(MockBehavior.Strict);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(false);
            _adapter.Setup(x => x.IsExternalEditMarker(It.IsAny<IVsTextLineMarker>())).Returns(false);
            var adapterList = new List<IExternalEditAdapter> { _adapter.Object };

            Result<IVsTextLines> textLines = Result.Error;
            if (hasTextLines)
            {
                _vsTextLines = _factory.Create<IVsTextLines>(MockBehavior.Strict);
                _vsTextLines.SetupNoEnumMarkers();
                textLines = Result.CreateSuccess(_vsTextLines.Object);
            }

            var taggerList = new List<ITagger<ITag>>();
            if (hasTagger)
            {
                _tagger = _factory.Create<ITagger<ITag>>(MockBehavior.Loose);
                _tagger.Setup(x => x.GetTags(It.IsAny<NormalizedSnapshotSpanCollection>())).Returns(new List<ITagSpan<ITag>>());
                taggerList.Add(_tagger.Object);
            }

            _monitor = new ExternalEditMonitor(
                _vimApplicationSettings.Object,
                _buffer,
                ProtectedOperations,
                textLines,
                taggerList.ToReadOnlyCollectionShallow(),
                adapterList.ToReadOnlyCollectionShallow());
        }

        public override void Dispose()
        {
            base.Dispose();
            Dispatcher.CurrentDispatcher.DoEvents();
        }

        /// <summary>
        /// Create markers of the specified type at the given SnapshotSpan values
        /// </summary>
        private void CreateMarkers(int tagType, params SnapshotSpan[] spans)
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

        /// <summary>
        /// Create tags at the specified SnapshotSpan values
        /// </summary>
        private void CreateTags(params SnapshotSpan[] spans)
        {
            var list = new List<ITagSpan<ITag>>();
            foreach (var span in spans)
            {
                var tagSpan = new TagSpan<ITag>(
                    span,
                    _factory.Create<ITag>().Object);
                list.Add(tagSpan);
            }

            _tagger.Setup(x => x.GetTags(It.IsAny<NormalizedSnapshotSpanCollection>())).Returns(list);
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

        /// <summary>
        /// Poke into ITextView and make it actually raise a LayoutChanged event.  Useful for
        /// testing
        /// </summary>
        private void RaiseLayoutChanged()
        {
            var methodInfo = _textView.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x => x.Name == "RaiseLayoutChangeEvent")
                .Where(x => x.GetParameters().Length == 0)
                .Single();
            methodInfo.Invoke(_textView, null);
        }

        public sealed class GetExternalEditSpansTest : ExternalEditMonitorTest
        {
            /// <summary>
            /// Ensure that ITag values which aren't interesting to us aren't returned 
            /// as an external edit span
            /// </summary>
            [Fact]
            public void Tags_NoiseValues()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(0, externalEditSpans.Count);
            }

            /// <summary>
            /// Ensure edit tags register as such
            /// </summary>
            [Fact]
            public void Tags_EditTag()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(1, externalEditSpans.Count);
                Assert.Equal(_textBuffer.GetLine(0).Extent, externalEditSpans[0]);
            }

            /// <summary>
            /// When we aren't passed the Tags check flag don't actually check tags
            /// </summary>
            [Fact]
            public void Tags_WrongFlag()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Markers);
                Assert.Equal(0, externalEditSpans.Count);
            }
        }

        public sealed class SwitchModeTest : ExternalEditMonitorTest
        {

            /// <summary>
            /// Verify we don't do anything special like saving external edit tags when switching
            /// out of a mode other than external edit
            /// </summary>
            [Fact]
            public void NoActionOutsideExternalEdit()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                Assert.Equal(1, _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Tags).Count);

                _buffer.SwitchMode(ModeKind.Command, ModeArgument.None);
                Assert.Equal(0, _monitor.IgnoredExternalEditSpans.Count());
            }

            /// <summary>
            /// This is a very important test because we often see the transition to visual mode
            /// before the layout and hence would ignore valid edit tags
            /// </summary>
            [Fact]
            public void OldModeIsExternalThenSaveIgnoreTags()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.Equal(1, _monitor.IgnoredExternalEditSpans.Count());
            }
        }

        public sealed class PeformCheckTest : ExternalEditMonitorTest
        {
            /// <summary>
            /// If we perform the check for external edit starts and there are indeed tags 
            /// then transition into external edit
            /// </summary>
            [Fact]
            public void TagsWithExternalEditTags()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.ExternalEdit, _buffer.ModeKind);
            }

            /// <summary>
            /// If we run the check and there are no more external edit tags we should transition 
            /// out of external edit mode and back into insert
            /// </summary>
            [Fact]
            public void TagsNoMoreExternalEdits()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.Insert, _buffer.ModeKind);
            }

            [Fact]
            public void TagsWithExternalEditTagsAndDisabled()
            {
                Create("cat", "tree", "dog");
                _vimApplicationSettings.SetupGet(x => x.EnableExternalEditMonitoring).Returns(false);
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.Normal, _buffer.ModeKind);
            }

            /// <summary>
            /// If we perform the check for external edit starts and there are indeed tags 
            /// but we're only looking for markers then don't take any action
            /// </summary>
            [Fact]
            public void MarksWithExternalEditTags()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.Markers);
                Assert.Equal(ModeKind.Normal, _buffer.ModeKind);
            }
        }
    }
}
