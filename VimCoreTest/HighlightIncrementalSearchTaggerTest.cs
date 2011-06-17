using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class HighlightIncrementalSearchTaggerTest
    {
        private HighlightIncrementalSearchTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;
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
            _taggerRaw = new HighlightIncrementalSearchTagger(
                _textBuffer,
                _globalSettings,
                VimUtil.CreateTextStructureNavigator(_textBuffer, WordKind.NormalWord),
                _searchService,
                _vimData);
            _tagger = _taggerRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _taggerRaw = null;
        }

        /// <summary>
        /// Do nothing if highlight is disabled
        /// </summary>
        [Test]
        public void GetTags_HighlightDisabled()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            _globalSettings.HighlightSearch = false;
            var ret = _taggerRaw.GetTags(_textBuffer.CurrentSnapshot.GetTaggerExtent());
            Assert.AreEqual(0, ret.Count());
        }

        /// <summary>
        /// Do nothing if the search pattern is empty
        /// </summary>
        [Test]
        public void GetTags_PatternEmpty()
        {
            Create("dog cat");
            _vimData.LastPatternData = VimUtil.CreatePatternData("");
            var ret = _taggerRaw.GetTags(_textBuffer.CurrentSnapshot.GetTaggerExtent());
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
            var ret = _taggerRaw.GetTags(_textBuffer.CurrentSnapshot.GetTaggerExtent());
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
            _taggerRaw._oneTimeDisabled = true;
            var ret = _taggerRaw.GetTags(_textBuffer.CurrentSnapshot.GetTaggerExtent());
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
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 4, 3)));
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
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2)));
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
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(_textBuffer.GetExtent()));
            CollectionAssert.AreEquivalent(
                new [] {"c", "a", "t"},
                ret.Select(x => x.Span.GetText()).ToList());
        }

        /// <summary>
        /// The one time disabled event should cause a TagsChaged event and the one time disabled
        /// flag to be set
        /// </summary>
        [Test]
        public void Handle_OneTimeDisabledEvent()
        {
            Create("");
            Assert.IsFalse(_taggerRaw._oneTimeDisabled);
            var raised = false;
            _tagger.TagsChanged += delegate { raised = true; };
            _vimData.RaiseHighlightSearchOneTimeDisable();
            Assert.IsTrue(raised);
            Assert.IsTrue(_taggerRaw._oneTimeDisabled);
        }

        /// <summary>
        /// The setting of the 'hlsearch' option should reset the one time disabled flag
        /// </summary>
        [Test]
        public void SettingSet()
        {
            Create("");
            _taggerRaw._oneTimeDisabled = true;
            _globalSettings.HighlightSearch = true;
            Assert.IsFalse(_taggerRaw._oneTimeDisabled);
        }

        /// <summary>
        /// The setting of the LastSearchData property should reset the _oneTimeDisabled flag
        /// </summary>
        [Test]
        public void LastSearchDataSet()
        {
            Create("");
            _taggerRaw._oneTimeDisabled = true;
            _vimData.LastPatternData = VimUtil.CreatePatternData("dog");
            Assert.IsFalse(_taggerRaw._oneTimeDisabled);
        }
    }
}
