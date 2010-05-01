using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Vim.Extensions;
using Microsoft.VisualStudio.Text.Operations;
using VimCore.Test.Utils;
using Microsoft.VisualStudio.Text;
using VimCore.Test.Mock;

namespace VimCore.Test
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
            _factory = new MockFactory(MockBehavior.Strict);
            _settings = _factory.Create<IVimGlobalSettings>();
            _textSearch = _factory.Create<ITextSearchService>();
            _searchRaw = new SearchService(_textSearch.Object, _settings.Object);
            _search = _searchRaw;
        }

        private void AssertFindNext(SearchData data, FindOptions options, int position = 2)
        {
            var isWrap = SearchKindUtil.IsWrap(data.Kind);
            var snapshot = MockObjectFactory.CreateTextSnapshot(10);
            var nav = _factory.Create<ITextStructureNavigator>();
            var findData = new FindData(data.Text.RawText, snapshot.Object, options, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(position, isWrap, findData))
                .Returns<SnapshotSpan?>(null)
                .Verifiable();
            _search.FindNext(data, new SnapshotPoint(snapshot.Object, position), nav.Object);
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
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.AllowIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions| FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions5()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.AllowIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions6()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(false).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.AllowIgnoreCase | SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions7()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.AllowIgnoreCase | SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions8()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.AllowIgnoreCase | SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions9()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            _settings.SetupGet(x => x.SmartCase).Returns(true).Verifiable();
            var options = _searchRaw.CreateFindOptions(SearchText.NewPattern("fOo"), SearchKind.Forward, SearchOptions.AllowIgnoreCase | SearchOptions.AllowSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions10()
        {
            var options = _searchRaw.CreateFindOptions(SearchText.NewWholeWord(""), SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse , options);
        }

        [Test]
        public void FindNext1()
        {
            _settings.SetupGet(x => x.IgnoreCase).Returns(true).Verifiable();
            AssertFindNext(new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.AllowIgnoreCase), FindOptions.UseRegularExpressions); 
        }

        [Test]
        public void FindNext2()
        {
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.Forward, SearchOptions.None);
            AssertFindNext(data, FindOptions.MatchCase | FindOptions.UseRegularExpressions);
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
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object,1);
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


    }

}
