using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class SearchServiceTest
    {
        private MockFactory _factory;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<ITextSearchService> _textSearch;
        private SearchService _searchRaw;
        private ISearchService _search;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockFactory(MockBehavior.Loose);
            _settings = _factory.Create<IVimGlobalSettings>();
            _textSearch = _factory.Create<ITextSearchService>();
            _searchRaw = new SearchService(_textSearch.Object, _settings.Object);
            _search = _searchRaw;
        }

        [Test]
        public void CreateSearchData1()
        {
            var data = _search.CreateSearchData("foo", SearchKind.Backward);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.Backward, data.Kind);
            Assert.AreEqual(SearchOptions.None, data.Options);
            _factory.Verify();
        }

        [Test]
        public void CreateSearchData2()
        {
            var data = _search.CreateSearchData("foo", SearchKind.Backward);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.Backward, data.Kind);
            Assert.AreEqual(SearchOptions.None, data.Options);
            _factory.Verify();
        }

        [Test]
        public void CreateSearchDataWithOptions1()
        {
            var data = _search.CreateSearchDataWithOptions("foo", SearchKind.ForwardWithWrap, SearchOptions.MatchWord);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.ForwardWithWrap, data.Kind);
            Assert.AreEqual(SearchOptions.MatchWord, data.Options);
        }

        [Test]
        public void CreateFindOptions1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.None, options);
        }

        [Test]
        public void CreateFindOptions2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions3()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.SearchReverse, options);
        }

        [Test]
        public void CreateFindOptions4()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchKind.Backward, SearchOptions.Regex);
            Assert.AreEqual(FindOptions.SearchReverse | FindOptions.UseRegularExpressions, options);
        }

        [Test]
        public void CreateFindOptions5()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchKind.Forward, SearchOptions.MatchWord);
            Assert.AreEqual(FindOptions.WholeWord, options);
        }

    }

}
