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

        private static string[] DefaultText = new string[] { 
            "this is the default text value for searching",
            "within this unit test.  I'm really losing my creativity",
            "at this point and just typing whatever" };

        private void Create(params string[] lines)
        {
            lines = lines.Length > 0 ? lines : DefaultText;
            _textBuffer = EditorUtil.CreateBuffer(lines);
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
            _vimData.LastSearchData = VimUtil.CreateSearchData("dog");
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
            _vimData.LastSearchData = VimUtil.CreateSearchData("");
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
            _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
            var ret = _taggerRaw.GetTags(_textBuffer.CurrentSnapshot.GetTaggerExtent());
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
            _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 4, 3)));
            Assert.AreEqual(0, ret.Count());
        }

        /// <summary>
        /// Spans which start in the request and end outside it should be returned
        /// </summary>
        [Test, Description("Spans which start in the request but end outside it should be returned")]
        public void GetTags5()
        {
            Create("foo is the bar");
            _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2)));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
        }
    }
}
