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
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var data = _search.CreateSearchData("foo", SearchKind.Backward);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.Backward, data.Kind);
            Assert.AreEqual(FindOptions.SearchReverse, data.Options);
            _factory.Verify();
        }

        [Test]
        public void CreateSearchData2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var data = _search.CreateSearchData("foo", SearchKind.Backward);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.Backward, data.Kind);
            Assert.AreEqual(FindOptions.SearchReverse | FindOptions.MatchCase, data.Options);
            _factory.Verify();
        }

        [Test]
        public void CreateSearchData3()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var data = _search.CreateSearchData("foo", SearchKind.ForwardWithWrap);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.ForwardWithWrap, data.Kind);
            Assert.AreEqual(FindOptions.MatchCase, data.Options);
            _factory.Verify();
        }

        [Test]
        public void CreateSearchDataWithOptions1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var data = _search.CreateSearchDataWithOptions("foo", SearchKind.ForwardWithWrap, FindOptions.WholeWord);
            Assert.AreEqual("foo", data.Pattern);
            Assert.AreEqual(SearchKind.ForwardWithWrap, data.Kind);
            Assert.AreEqual(FindOptions.MatchCase | FindOptions.WholeWord, data.Options);
            _factory.Verify();
        }
    }
}
