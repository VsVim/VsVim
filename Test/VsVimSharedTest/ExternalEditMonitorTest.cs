using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.Extensions;
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
        private Mock<IExternalEditAdapter> _adapter2;
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
            _adapter = CreateAdapter();
            _adapter2 = CreateAdapter();
            var adapterList = new List<IExternalEditAdapter> { _adapter.Object, _adapter2.Object };

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
            DoEvents();
        }

        private Mock<IExternalEditAdapter> CreateAdapter()
        {
            var adapter = _factory.Create<IExternalEditAdapter>(MockBehavior.Strict);
            adapter.Setup(x => x.IsExternalEditActive(It.IsAny<ITextView>())).Returns((bool?)null);
            adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(false);
            adapter.Setup(x => x.IsExternalEditMarker(It.IsAny<IVsTextLineMarker>())).Returns(false);
            return adapter;
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
            [WpfFact]
            public void Tags_NoiseValues()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
                Assert.Empty(externalEditSpans);
            }

            /// <summary>
            /// Ensure edit tags register as such
            /// </summary>
            [WpfFact]
            public void Tags_EditTag()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
                Assert.Single(externalEditSpans);
                Assert.Equal(_textBuffer.GetLine(0).Extent, externalEditSpans[0]);
            }

            /// <summary>
            /// When we aren't passed the Tags check flag don't actually check tags
            /// </summary>
            [WpfFact]
            public void Tags_WrongFlag()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Markers);
                Assert.Empty(externalEditSpans);
            }
        }

        public sealed class SwitchModeTest : ExternalEditMonitorTest
        {
            /// <summary>
            /// Verify we don't do anything special like saving external edit tags when switching
            /// out of a mode other than external edit
            /// </summary>
            [WpfFact]
            public void NoActionOutsideExternalEdit()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                Assert.Single(_monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Tags));

                _buffer.SwitchMode(ModeKind.Command, ModeArgument.None);
                Assert.Empty(_monitor.IgnoredExternalEditSpans);
            }

            /// <summary>
            /// This is a very important test because we often see the transition to visual mode
            /// before the layout and hence would ignore valid edit tags
            /// </summary>
            [WpfFact]
            public void OldModeIsExternalThenSaveIgnoreTags()
            {
                Create("cat", "tree", "dog");
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.Single(_monitor.IgnoredExternalEditSpans);
            }
        }

        public sealed class PeformCheckTest : ExternalEditMonitorTest
        {
            /// <summary>
            /// If we perform the check for external edit starts and there are indeed tags 
            /// then transition into external edit
            /// </summary>
            [WpfFact]
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
            /// If we run the check and there are no more external edit tags
            /// we should transition  out of external edit mode and back to
            /// the previous mode
            /// </summary>
            [WpfFact]
            public void TagsNoMoreExternalEdits()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                CreateTags();
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.Normal, _buffer.ModeKind);
            }

            [WpfFact]
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
            [WpfFact]
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

        public sealed class ControlExternalEditTest : ExternalEditMonitorTest
        {
            [WpfFact]
            public void ControlledByOther()
            {
                Create("cat");
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                Assert.False(_monitor.ControlExternalEdit);
            }

            [WpfFact]
            public void ControlledByMonitor()
            {
                Create("cat", "tree", "dog");
                _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                CreateTags(_textBuffer.GetLine(0).Extent);
                _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.ExternalEdit, _buffer.ModeKind);
                Assert.True(_monitor.ControlExternalEdit);
            }

            [WpfFact]
            public void PerformCheckWhenControlledByOthers()
            {
                Create("cat");
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.ExternalEdit, _buffer.ModeKind);
                Assert.False(_monitor.ControlExternalEdit);
            }
        }

        public sealed class IsExternalEditActiveTest : ExternalEditMonitorTest
        {
            [WpfFact]
            public void OnOffTest()
            {
                Create("cat");
                _adapter.Setup(x => x.IsExternalEditActive(_textView)).Returns(true);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.ExternalEdit, _buffer.ModeKind);
                Assert.True(_monitor.ControlExternalEdit);

                _adapter.Setup(x => x.IsExternalEditActive(_textView)).Returns(false);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.NotEqual(ModeKind.ExternalEdit, _buffer.ModeKind);
            }

            /// <summary>
            /// If at least one thinks an external edit is active then it's active
            /// </summary>
            [WpfFact]
            public void AdapterDisagree()
            {
                Create("cat");
                _adapter.Setup(x => x.IsExternalEditActive(_textView)).Returns(true);
                _adapter2.Setup(x => x.IsExternalEditActive(_textView)).Returns(false);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.Equal(ModeKind.ExternalEdit, _buffer.ModeKind);
                Assert.True(_monitor.ControlExternalEdit);

                _adapter.Setup(x => x.IsExternalEditActive(_textView)).Returns(false);
                _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
                Assert.NotEqual(ModeKind.ExternalEdit, _buffer.ModeKind);
            }
        }
    }
}
