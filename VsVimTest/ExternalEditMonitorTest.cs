using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
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
    /// <summary>
    /// Tests for the ExternalEditorMonitor implementation.  Need to really hammer the scenarios here
    /// as this component in past forms is a frequent source of user hangs
    /// </summary>
    [TestFixture]
    public sealed class ExternalEditMonitorTest : VimTestBase
    {
        private MockRepository _factory;
        private IVimBuffer _buffer;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private Mock<IExternalEditAdapter> _adapter;
        private Mock<ITagger<ITag>> _tagger;
        private Mock<IVsTextLines> _vsTextLines;
        private ExternalEditMonitor _monitor;

        public void Create(params string[] lines)
        {
            Create(true, true, lines);
        }

        public void Create(bool hasTextLines, bool hasTagger, params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);

            // Have adatper ignore by default
            _adapter = _factory.Create<IExternalEditAdapter>(MockBehavior.Strict);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(false);
            _adapter.Setup(x => x.IsExternalEditMarker(It.IsAny<IVsTextLineMarker>())).Returns(false);

            Result<IVsTextLines> textLines = Result.Error;
            if (hasTextLines)
            {
                _vsTextLines = _factory.Create<IVsTextLines>(MockBehavior.Strict);
                _vsTextLines.SetupNoEnumMarkers();
                textLines = Result.CreateSuccess(_vsTextLines.Object);
            }

            Result<ITagger<ITag>> tagger = Result.Error;
            if (hasTagger)
            {
                _tagger = _factory.Create<ITagger<ITag>>(MockBehavior.Loose);
                _tagger.Setup(x => x.GetTags(It.IsAny<NormalizedSnapshotSpanCollection>())).Returns(new List<ITagSpan<ITag>>());
                tagger = Result.CreateSuccess(_tagger.Object);
            }

            var list = new List<IExternalEditAdapter> { _adapter.Object };
            var adapters = new ReadOnlyCollection<IExternalEditAdapter>(list);
            _monitor = new ExternalEditMonitor(
                _buffer,
                textLines,
                tagger,
                adapters);
        }

        [TearDown]
        public void TearDown()
        {
            Dispatcher.CurrentDispatcher.DoEvents();
            _buffer.Close();
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

        /// <summary>
        /// Ensure that ITag values which aren't interesting to us aren't returned 
        /// as an external edit span
        /// </summary>
        [Test]
        public void GetExternalEditSpans_Tags_NoiseValues()
        {
            Create("cat", "tree", "dog");
            CreateTags(_textBuffer.GetLine(0).Extent);
            var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
            Assert.AreEqual(0, externalEditSpans.Count);
        }

        /// <summary>
        /// Ensure edit tags register as such
        /// </summary>
        [Test]
        public void GetExternalEditSpans_Tags_EditTag()
        {
            Create("cat", "tree", "dog");
            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.All);
            Assert.AreEqual(1, externalEditSpans.Count);
            Assert.AreEqual(_textBuffer.GetLine(0).Extent, externalEditSpans[0]);
        }

        /// <summary>
        /// When we aren't passed the Tags check flag don't actually check tags
        /// </summary>
        [Test]
        public void GetExternalEditSpans_Tags_WrongFlag()
        {
            Create("cat", "tree", "dog");
            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            var externalEditSpans = _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Markers);
            Assert.AreEqual(0, externalEditSpans.Count);
        }

        /// <summary>
        /// Verify we don't do anything special like saving external edit tags when switching
        /// out of a mode other than external edit
        /// </summary>
        [Test]
        public void SwitchMode_NoActionOutsideExternalEdit()
        {
            Create("cat", "tree", "dog");
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);

            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            Assert.AreEqual(1, _monitor.GetExternalEditSpans(ExternalEditMonitor.CheckKind.Tags).Count);

            _buffer.SwitchMode(ModeKind.Command, ModeArgument.None);
            Assert.AreEqual(0, _monitor.IgnoredExternalEditSpans.Count());
        }

        /// <summary>
        /// This is a very important test because we often see the transition to visual mode
        /// before the layout and hence would ignore valid edit tags
        /// </summary>
        [Test]
        public void SwitchMode_OldModeIsExternalThenSaveIgnoreTags()
        {
            Create("cat", "tree", "dog");
            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.AreEqual(1, _monitor.IgnoredExternalEditSpans.Count());
        }

        /// <summary>
        /// If we perform the check for external edit starts and there are indeed tags 
        /// then transition into external edit
        /// </summary>
        [Test]
        public void PerformCheck_Tags_WithExternalEditTags()
        {
            Create("cat", "tree", "dog");
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
            Assert.AreEqual(ModeKind.ExternalEdit, _buffer.ModeKind);
        }

        /// <summary>
        /// If we perform the check for external edit starts and there are indeed tags 
        /// but we're only looking for markers then don't take any action
        /// </summary>
        [Test]
        public void PerformCheck_Marks_WithExternalEditTags()
        {
            Create("cat", "tree", "dog");
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            CreateTags(_textBuffer.GetLine(0).Extent);
            _adapter.Setup(x => x.IsExternalEditTag(It.IsAny<ITag>())).Returns(true);
            _monitor.PerformCheck(ExternalEditMonitor.CheckKind.Markers);
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        /// <summary>
        /// If we run the check and there are no more external edit tags we should transition 
        /// out of external edit mode and back into insert
        /// </summary>
        [Test]
        public void PerformCheck_Tags_NoMoreExternalEdits()
        {
            Create("cat", "tree", "dog");
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            _monitor.PerformCheck(ExternalEditMonitor.CheckKind.All);
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }
    }
}
