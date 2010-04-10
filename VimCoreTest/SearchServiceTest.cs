using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Operations;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;

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

        private void AssertFindNextPattern(string pattern, SearchKind kind, FindOptions options, int position = 2)
        {
            var isWrap = SearchKindUtil.IsWrap(kind);
            var snapshot = MockObjectFactory.CreateTextSnapshot(10);
            var nav = _factory.Create<ITextStructureNavigator>();
            var findData = new FindData(pattern, snapshot.Object, options, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(position, isWrap, findData))
                .Returns<SnapshotSpan?>(null)
                .Verifiable();
            _search.FindNextPattern(pattern, kind ,new SnapshotPoint(snapshot.Object, position), nav.Object);
            _factory.Verify();
        }

        private void AssertFindNextResult(SearchData data, FindOptions options, int position = 2)
        {
            var isWrap = SearchKindUtil.IsWrap(data.Kind);
            var snapshot = MockObjectFactory.CreateTextSnapshot(10);
            var nav = _factory.Create<ITextStructureNavigator>();
            var findData = new FindData(data.Pattern, snapshot.Object, options, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(position, isWrap, findData))
                .Returns<SnapshotSpan?>(null)
                .Verifiable();
            _search.FindNextResult(data, new SnapshotPoint(snapshot.Object, position), nav.Object);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions("", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.None, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions("", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions3()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions("", SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.SearchReverse, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions4()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions("", SearchKind.Backward, SearchOptions.Regex);
            Assert.AreEqual(FindOptions.SearchReverse | FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions5()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions("", SearchKind.Forward, SearchOptions.MatchWord);
            Assert.AreEqual(FindOptions.WholeWord, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions6()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions("foo", SearchKind.Forward, SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.None, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions7()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions("FOO", SearchKind.Forward, SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions8()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions("FOO", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.None, options);
            _factory.Verify();
        }

        [Test]
        public void FindNextPattern1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            AssertFindNextPattern("foo", SearchKind.Forward, FindOptions.UseRegularExpressions);
        }

        [Test]
        public void FindNextPattern2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            AssertFindNextPattern("foo", SearchKind.Forward, FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        public void FindNextPattern3()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            AssertFindNextPattern("foo", SearchKind.Backward, FindOptions.MatchCase | FindOptions.SearchReverse | FindOptions.UseRegularExpressions);
        }

        [Test]
        public void FindNextPattern4()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            AssertFindNextPattern("FOO", SearchKind.Backward, FindOptions.MatchCase | FindOptions.SearchReverse | FindOptions.UseRegularExpressions);
        }

        [Test]
        public void FindNextResult1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var data = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None);
            AssertFindNextResult(data, FindOptions.None);
        }

        [Test]
        public void FindNextResult2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var data = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.MatchWord);
            AssertFindNextResult(data, FindOptions.WholeWord);
        }
    }

}
