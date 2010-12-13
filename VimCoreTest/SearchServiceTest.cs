using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SearchServiceTest
    {
        private MockRepository _factory;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<ITextSearchService> _textSearch;
        private SearchService _searchRaw;
        private ISearchService _search;

        [SetUp]
        public void SetUp()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _settings = _factory.Create<IVimGlobalSettings>();
            _settings.SetupGet(x => x.Magic).Returns(true);
            _settings.SetupGet(x => x.IgnoreCase).Returns(true);
            _settings.SetupGet(x => x.SmartCase).Returns(false);
            _textSearch = _factory.Create<ITextSearchService>();
            _searchRaw = new SearchService(_textSearch.Object, _settings.Object);
            _search = _searchRaw;
        }

        private void AssertFindNext(
            SearchData data,
            string searchText,
            FindOptions options)
        {
            var isWrap = SearchKindUtil.IsWrap(data.Kind);
            var snapshot = MockObjectFactory.CreateTextSnapshot(10);
            var nav = _factory.Create<ITextStructureNavigator>();
            var findData = new FindData(searchText, snapshot.Object, options, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(2, isWrap, findData))
                .Returns<SnapshotSpan?>(null)
                .Verifiable();
            _search.FindNext(data, new SnapshotPoint(snapshot.Object, 2), nav.Object);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions1()
        {
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions2()
        {
            var options = _searchRaw.CreateFindOptions(SearchText.NewStraightText(""), SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions3()
        {
            var options = _searchRaw.CreateFindOptions(SearchText.NewWholeWord(""), SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions4()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions5()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions6()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions7()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions8()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions9()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern("fOo"), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions10()
        {
            var options = _searchRaw.CreateFindOptions(SearchText.NewWholeWord(""), SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse, options);
        }

        [Test]
        [Description("Needs to respect the ignorecase option if ConsiderIgnoreCase is specified")]
        public void FindNext1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var data = VimUtil.CreateSearchData(SearchText.NewPattern("foo"), options: SearchOptions.ConsiderIgnoreCase);
            AssertFindNext(data, "foo", FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Needs to respect the noignorecase option if ConsiderIgnoreCase is specified")]
        public void FindNext2()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(false).Verifiable();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            AssertFindNext(data, "foo", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Match case if not considering smart or ignore case")]
        public void FindNext3()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.None);
            AssertFindNext(data, "foo", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Verify it's using the regex and not the text")]
        public void FindNext4()
        {
            var data = VimUtil.CreateSearchData(SearchText.NewPattern(@"\<the"));
            AssertFindNext(data, @"\bthe", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Verify it's using the regex and not the text")]
        public void FindNext5()
        {
            var data = VimUtil.CreateSearchData(SearchText.NewPattern(@"(the"));
            AssertFindNext(data, @"\(the", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        public void FindNextMulitple1()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            var data = new FindData("foo", tss, FindOptions.UseRegularExpressions | FindOptions.MatchCase, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(10, true, data))
                .Returns(new SnapshotSpan(tss, 11, 3))
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 1);
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(new SnapshotSpan(tss, 11, 3), ret.Value);
            _factory.Verify();
        }

        [Test]
        public void FindNextMulitple2()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            var data = new FindData("foo", tss, FindOptions.UseRegularExpressions | FindOptions.MatchCase, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(10, true, data))
                .Returns(new SnapshotSpan(tss, 11, 3))
                .Verifiable();
            _textSearch
                .Setup(x => x.FindNext(14, true, data))
                .Returns((SnapshotSpan?)null)
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsFalse(ret.IsSome());
            _factory.Verify();
        }

        [Test, Description("In a normal back search stop at 0")]
        public void FindNextMulitple3()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            var data = new FindData("foo", tss, FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.SearchReverse, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(10, false, data))
                .Returns(new SnapshotSpan(tss, 0, 3))
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.Backward, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsFalse(ret.IsSome());
            _factory.Verify();
        }

        [Test]
        public void FindNextMulitple4()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            var data = new FindData("foo", tss, FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.SearchReverse, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(10, true, data))
                .Returns(new SnapshotSpan(tss, 0, 3))
                .Verifiable();
            _textSearch
                .Setup(x => x.FindNext(42, true, data))
                .Returns(new SnapshotSpan(tss, 10, 3))
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.BackwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(new SnapshotSpan(tss, 10, 3), ret.Value);
            _factory.Verify();
        }

        [Test]
        public void FindNextMulitple5()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            var data = new FindData("foo", tss, FindOptions.UseRegularExpressions | FindOptions.MatchCase, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(10, true, data))
                .Returns(new SnapshotSpan(tss, 0, 3))
                .Verifiable();
            _textSearch
                .Setup(x => x.FindNext(3, true, data))
                .Returns(new SnapshotSpan(tss, 10, 3))
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(new SnapshotSpan(tss, 10, 3), ret.Value);
            _factory.Verify();
        }

        [Test]
        public void BadRegex1()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            _textSearch
                .Setup(x => x.FindNext(0, true, It.IsAny<FindData>()))
                .Throws(new InvalidOperationException())
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("f("), SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNext(searchData, new SnapshotPoint(tss, 0), nav.Object);
            Assert.IsTrue(ret.IsNone());
            _factory.Verify();
        }


        [Test]
        public void BadRegex2()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            _textSearch
                .Setup(x => x.FindNext(0, true, It.IsAny<FindData>()))
                .Throws(new InvalidOperationException())
                .Verifiable();
            var searchData = new SearchData(SearchText.NewPattern("f("), SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 0), nav.Object, 2);
            Assert.IsTrue(ret.IsNone());
            _factory.Verify();
        }

        [Test]
        [Description("An InvalidOperationException from a non-regex shouldn't be handled")]
        public void BadRegex3()
        {
            var tss = MockObjectFactory.CreateTextSnapshot(42).Object;
            var nav = _factory.Create<ITextStructureNavigator>();
            _textSearch
                .Setup(x => x.FindNext(0, true, It.IsAny<FindData>()))
                .Throws(new InvalidOperationException())
                .Verifiable();
            var searchData = new SearchData(SearchText.NewStraightText("f("), SearchKind.ForwardWithWrap, SearchOptions.None);
            Assert.Throws<InvalidOperationException>(() => _search.FindNext(searchData, new SnapshotPoint(tss, 0), nav.Object));
            _factory.Verify();
        }
    }

}
