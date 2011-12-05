using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class HighlightSearchTaggerSourceTest : VimTestBase
    {
        private HighlightSearchTaggerSource _asyncTaggerSourceRaw;
        private IAsyncTaggerSource<SearchData, TextMarkerTag> _asyncTaggerSource;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private ISearchService _searchService;
        private IVimGlobalSettings _globalSettings;
        private IVimData _vimData;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _globalSettings = new GlobalSettings();
            _globalSettings.IgnoreCase = true;
            _globalSettings.HighlightSearch = true;
            _searchService = VimUtil.CreateSearchService();
            _vimData = Vim.VimData;
            _asyncTaggerSourceRaw = new HighlightSearchTaggerSource(
                _textView,
                _globalSettings,
                VimUtil.CreateTextStructureNavigator(_textBuffer, WordKind.NormalWord),
                _searchService,
                _vimData,
                Vim.VimHost);
            _asyncTaggerSource = _asyncTaggerSourceRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _asyncTaggerSourceRaw = null;
        }

        private List<ITagSpan<TextMarkerTag>> TryGetTagsPrompt(SnapshotSpan span)
        {
            IEnumerable<ITagSpan<TextMarkerTag>> tagList;
            Assert.IsTrue(_asyncTaggerSource.TryGetTagsPrompt(span, out tagList));
            return tagList.ToList();
        }

        private List<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
        {
            return _asyncTaggerSource.GetTagsInBackground(
                _asyncTaggerSourceRaw.GetDataForSpan(),
                span,
                CancellationToken.None).ToList();
        }

        /// <summary>
        /// Do nothing if the search pattern is empty
        /// </summary>
        [Test]
        public void GetTags_PatternEmpty()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("");
            var ret = GetTags(_textBuffer.GetExtent());
            Assert.AreEqual(0, ret.Count());
        }

        /// <summary>
        /// Make sure the matches are returned
        /// </summary>
        [Test]
        public void GetTags_WithMatch()
        {
            Create("foo is the bar");
            _vimData.LastPatternData = VimUtil.CreatePatternData("foo");
            var ret = GetTags(_textBuffer.GetExtent());
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
        }

        /// <summary>
        /// Don't return tags outside the requested span
        /// </summary>
        [Test]
        public void GetTags_OutSideSpan()
        {
            Create("foo is the bar");
            _vimData.LastPatternData = VimUtil.CreatePatternData("foo");
            var ret = GetTags(new SnapshotSpan(_textBuffer.CurrentSnapshot, 4, 3));
            Assert.AreEqual(0, ret.Count());
        }

        /// <summary>
        /// Spans which start in the request and end outside it should be returned
        /// </summary>
        [Test]
        public void GetTags_SpansOnStartOfMatch()
        {
            Create("foo is the bar");
            _vimData.LastPatternData = VimUtil.CreatePatternData("foo");
            var ret = GetTags(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
        }

        /// <summary>
        /// It's possible for the search service to return a match of 0 length.  This is perfectly legal 
        /// and should be treated as a match of length 1.  This is how gVim does it
        /// </summary>
        [Test]
        public void GetTags_ZeroLengthResults()
        {
            Create("cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData(@"\|i\>");
            var ret = GetTags(_textBuffer.GetExtent());
            CollectionAssert.AreEquivalent(
                new [] {"c", "a", "t"},
                ret.Select(x => x.Span.GetText()).ToList());
        }

        /// <summary>
        /// We can promptly say nothing when highlight is disabled
        /// </summary>
        [Test]
        public void TryGetTagsPrompt_HighlightDisabled()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _globalSettings.HighlightSearch = false;
            var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
            Assert.AreEqual(0, ret.Count);
        }

        /// <summary>
        /// We can promptly say nothing when in One Time disabled
        /// </summary>
        [Test]
        public void TryGetTagsPrompt_OneTimeDisabled()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _asyncTaggerSourceRaw._oneTimeDisabled = true;
            var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
            Assert.AreEqual(0, ret.Count);
        }

        /// <summary>
        /// If the ITextView is not considered visible then we shouldn't be returning any
        /// tags
        /// </summary>
        [Test]
        public void GetTagsPrompt_NotVisible()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _asyncTaggerSourceRaw._isVisible = false;
            var ret = TryGetTagsPrompt(_textBuffer.GetExtent());
            Assert.AreEqual(0, ret.Count);
        }

        /// <summary>
        /// The one time disabled event should cause a Changed event and the one time disabled
        /// flag to be set
        /// </summary>
        [Test]
        public void Changed_OneTimeDisabledEvent()
        {
            Create("");
            Assert.IsFalse(_asyncTaggerSourceRaw._oneTimeDisabled);
            var raised = false;
            _asyncTaggerSource.Changed += delegate { raised = true; };
            _vimData.RaiseHighlightSearchOneTimeDisable();
            Assert.IsTrue(raised);
            Assert.IsTrue(_asyncTaggerSourceRaw._oneTimeDisabled);
        }

        /// <summary>
        /// If the visibility of the ITextView changes it should cause a Changed event to be raised
        /// </summary>
        [Test]
        public void Changed_IsVisibleChanged()
        {
            Create("");
            Assert.IsTrue(_asyncTaggerSourceRaw._isVisible);
            var raised = false;
            _asyncTaggerSource.Changed += delegate { raised = true; };
            VimHost.IsTextViewVisible = false;
            VimHost.RaiseIsVisibleChanged(_textView);
            Assert.IsFalse(_asyncTaggerSourceRaw._isVisible);
            Assert.IsTrue(raised);
        }

        /// <summary>
        /// The setting of the 'hlsearch' option should reset the one time disabled flag
        /// </summary>
        [Test]
        public void SettingSet()
        {
            Create("");
            _asyncTaggerSourceRaw._oneTimeDisabled = true;
            _globalSettings.HighlightSearch = true;
            Assert.IsFalse(_asyncTaggerSourceRaw._oneTimeDisabled);
        }

        /// <summary>
        /// The setting of the LastSearchData property should reset the _oneTimeDisabled flag
        /// </summary>
        [Test]
        public void LastSearchDataSet()
        {
            Create("");
            _asyncTaggerSourceRaw._oneTimeDisabled = true;
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            Assert.IsFalse(_asyncTaggerSourceRaw._oneTimeDisabled);
        }
    }
}
