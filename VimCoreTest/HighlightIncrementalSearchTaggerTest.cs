using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.Normal;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class HighlightIncrementalSearchTaggerTest
    {
        private HighlightIncrementalSearchTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;
        private ITextBuffer _textBuffer;
        private MockRepository _factory;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<ISearchService> _search;
        private Mock<ITextStructureNavigator> _nav;
        private Mock<IVimData> _vimData;

        private static string[] DefaultText = new string[] { 
            "this is the default text value for searching",
            "within this unit test.  I'm really losing my creativity",
            "at this point and just typing whatever" };

        public void Init(
            bool forSearch = true,
            string lastSearch = null,
            params string[] lines)
        {
            lines = lines.Length > 0 ? lines : DefaultText;
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _settings = _factory.Create<IVimGlobalSettings>(MockBehavior.Loose);
            _search = _factory.Create<ISearchService>(MockBehavior.Loose);
            _nav = _factory.Create<ITextStructureNavigator>(MockBehavior.Strict);
            _vimData = _factory.Create<IVimData>(MockBehavior.Loose);
            _taggerRaw = new HighlightIncrementalSearchTagger(
                _textBuffer,
                _settings.Object,
                _nav.Object,
                _search.Object,
                _vimData.Object);
            _tagger = _taggerRaw;

            if (forSearch)
            {
                _settings.SetupGet(x => x.IgnoreCase).Returns(true);
                _settings.SetupGet(x => x.HighlightSearch).Returns(true);
            }

            if (lastSearch != null)
            {
                _vimData
                    .SetupGet(x => x.LastSearchData)
                    .Returns(new SearchData(SearchText.NewPattern(lastSearch), SearchKind.Forward, SearchOptions.None));
            }
        }

        [TearDown]
        public void TearDown()
        {
            _taggerRaw = null;
        }

        [Test, Description("Do nothing if search disabled")]
        public void GetTags1()
        {
            Init(forSearch: false);
            _settings.Setup(x => x.HighlightSearch).Returns(false).Verifiable();
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection());
            Assert.AreEqual(0, ret.Count());
            _settings.Verify();
        }

        [Test, Description("Do nothing if the search pattern is empty")]
        public void GetTags2()
        {
            Init(forSearch: false);
            _settings.Setup(x => x.HighlightSearch).Returns(true).Verifiable();
            _vimData
                .SetupGet(x => x.LastSearchData)
                .Returns(new SearchData(SearchText.NewPattern(String.Empty), SearchKind.Forward, SearchOptions.None))
                .Verifiable();
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2)));
            Assert.AreEqual(0, ret.Count());
            _settings.Verify();
            _search.Verify();
        }

        [Test]
        public void GetTags3()
        {
            Init(lines: "foo is the bar", lastSearch: "foo");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.None);
            _search
                .Setup(x => x.FindNext(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            _search
                .Setup(x => x.FindNext(data, _textBuffer.GetPoint(3), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
        }

        [Test, Description("Don't return a tag outside the requested span")]
        public void GetTags4()
        {
            Init(lines: "foo is the bar", lastSearch: "foo");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.None);
            _search
                .Setup(x => x.FindNext(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(_textBuffer.GetSpan(4, 3)));
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            Assert.AreEqual(0, ret.Count());
        }

        [Test, Description("Spans which start in the request but end outside it should be returned")]
        public void GetTags5()
        {
            Init(lines: "foo is the bar", lastSearch: "foo");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.None);
            _search
                .Setup(x => x.FindNext(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(_textBuffer.GetSpan(2, 3)));
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 2, 3), ret.Single().Span);
        }

    }
}
