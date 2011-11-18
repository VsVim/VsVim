using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class HighlightSearchTaggerSourceTest
    {
        private HighlightSearchTaggerSource _asyncTaggerSourceRaw;
        private IAsyncTaggerSource<SearchData, TextMarkerTag> _asyncTaggerSource;
        private ITextBuffer _textBuffer;
        private ISearchService _searchService;
        private IVimGlobalSettings _globalSettings;
        private IVimData _vimData;

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _globalSettings = new GlobalSettings();
            _globalSettings.IgnoreCase = true;
            _globalSettings.HighlightSearch = true;
            _vimData = new VimData();
            _searchService = VimUtil.CreateSearchService();
            _asyncTaggerSourceRaw = new HighlightSearchTaggerSource(
                _textBuffer,
                _globalSettings,
                VimUtil.CreateTextStructureNavigator(_textBuffer, WordKind.NormalWord),
                _searchService,
                _vimData);
            _asyncTaggerSource = _asyncTaggerSourceRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _asyncTaggerSourceRaw = null;
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
        /// Make sure nothing is returned when we are in one time disabled mode
        /// </summary>
        [Test]
        public void GetTags_OneTimeDisabled()
        {
            Create("foo is the bar");
            _asyncTaggerSourceRaw._oneTimeDisabled = true;
            var ret = GetTags(_textBuffer.GetExtent());
            Assert.AreEqual(0, ret.Count());
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
        public void GetTagsPrompt_HighlightDisabled()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _globalSettings.HighlightSearch = false;
            var ret = _asyncTaggerSource.GetTagsPrompt(_textBuffer.GetExtent());
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(0, ret.Value.Length);
        }

        /// <summary>
        /// We can promptly say nothing when in One Time disabled
        /// </summary>
        [Test]
        public void GetTagsPrompt_OneTimeDisabled()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _asyncTaggerSourceRaw._oneTimeDisabled = true;
            var ret = _asyncTaggerSource.GetTagsPrompt(_textBuffer.GetExtent());
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(0, ret.Value.Length);
        }

        /// <summary>
        /// The one time disabled event should cause a TagsChaged event and the one time disabled
        /// flag to be set
        /// </summary>
        [Test]
        public void Handle_OneTimeDisabledEvent()
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
