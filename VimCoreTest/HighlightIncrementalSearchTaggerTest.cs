using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Normal;
using Moq;
using Microsoft.VisualStudio.Text;
using Vim;
using Microsoft.VisualStudio.Text.Operations;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    [TestFixture]
    public class HighlightIncrementalSearchTaggerTest
    {
        private HighlightIncrementalSearchTagger _taggerRaw;
        private ITagger<TextMarkerTag> _tagger;
        private ITextBuffer _textBuffer;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<ISearchService> _search;
        private Mock<ITextStructureNavigator> _nav;

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
            _settings = new Mock<IVimGlobalSettings>();
            _search = new Mock<ISearchService>();
            _nav = new Mock<ITextStructureNavigator>(MockBehavior.Strict);
            _taggerRaw = new HighlightIncrementalSearchTagger(
                _textBuffer,
                _settings.Object,
                _nav.Object,
                _search.Object);
            _tagger = _taggerRaw;

            if (forSearch)
            {
                _settings.SetupGet(x => x.IgnoreCase).Returns(true);
                _settings.SetupGet(x => x.HighlightSearch).Returns(true);
            }

            if (lastSearch != null)
            {
                _search.SetupGet(x => x.LastSearch).Returns(new SearchData(lastSearch, SearchKind.Forward, FindOptions.None));
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
            _search
                .SetupGet(x => x.LastSearch)
                .Returns(new SearchData(String.Empty, SearchKind.Forward, FindOptions.None))
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
            var data = new SearchData("foo", SearchKind.Forward, FindOptions.None);
            _search
                .Setup(x => x.FindNextResult(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            _search
                .Setup(x => x.FindNextResult(data, _textBuffer.GetPoint(3), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3), ret.Single().Span);
        }

        [Test, Description("Don't return a tag outside the requested span")]
        public void GetTags4()
        {
            Init(lines: "foo is the bar", lastSearch: "foo");
            var data = new SearchData("foo", SearchKind.Forward, FindOptions.None);
            _search
                .Setup(x => x.FindNextResult(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(_textBuffer.GetSpan(4,3)));
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            Assert.AreEqual(0, ret.Count());
        }

        [Test, Description("Spans which start in the request but end outside it should be returned")]
        public void GetTags5()
        {
            Init(lines: "foo is the bar", lastSearch: "foo");
            var data = new SearchData("foo", SearchKind.Forward, FindOptions.None);
            _search
                .Setup(x => x.FindNextResult(data, _textBuffer.GetPoint(0), _nav.Object))
                .Returns(FSharpOption.Create(_textBuffer.GetSpan(2,3)));
            var ret = _taggerRaw.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3)));
            Assert.AreEqual(1, ret.Count());
            Assert.AreEqual(new SnapshotSpan(_textBuffer.CurrentSnapshot, 2, 3), ret.Single().Span);
        }

    }
}
