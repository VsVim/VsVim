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
        private ITextBuffer _textBuffer;
        private ITextStructureNavigator _navigator;
        private IVimGlobalSettings _globalSettings;
        private MockRepository _factory;
        private Mock<ITextSearchService> _textSearch;
        private IVimData _vimData;
        private SearchService _searchRaw;
        private ISearchService _search;

        [SetUp]
        public void SetUp()
        {
            _textBuffer = EditorUtil.CreateBuffer("");
            _navigator = VimUtil.CreateTextStructureNavigator(_textBuffer);
            _vimData = new VimData();
            _globalSettings = new Vim.GlobalSettings();
            _globalSettings.Magic = true;
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;

            _factory = new MockRepository(MockBehavior.Strict);
            _textSearch = _factory.Create<ITextSearchService>();
            _searchRaw = new SearchService(_textSearch.Object, _globalSettings);
            _search = _searchRaw;
        }

        private void AssertFindNext(
            SearchData data,
            string searchText,
            FindOptions options)
        {
            var snapshot = MockObjectFactory.CreateTextSnapshot(10);
            var nav = _factory.Create<ITextStructureNavigator>();
            var findData = new FindData(searchText, snapshot.Object, options, nav.Object);
            _textSearch
                .Setup(x => x.FindNext(2, true, findData))
                .Returns<SnapshotSpan?>(null)
                .Verifiable();
            _search.FindNext(data, new SnapshotPoint(snapshot.Object, 2), nav.Object);
            _factory.Verify();
        }

        private FindOptions CreateFindOptions(string pattern, SearchKind kind, SearchOptions options)
        {
            var searchData = new SearchData(pattern, kind, options);
            var findData = _searchRaw.ConvertToFindData(searchData, _textBuffer.CurrentSnapshot, _navigator);
            Assert.IsTrue(findData.IsSome());
            return findData.Value.FindOptions;
        }

        [Test]
        public void CreateFindOptions1()
        {
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions2()
        {
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions3()
        {
            var options = CreateFindOptions(@"\<sample\>", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions4()
        {
            _globalSettings.IgnoreCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions5()
        {
            _globalSettings.IgnoreCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions6()
        {
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions7()
        {
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions8()
        {
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("foo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions9()
        {
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("fOo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
            _factory.Verify();
        }

        [Test]
        public void CreateFindOptions10()
        {
            var options = CreateFindOptions(PatternUtil.CreateWholeWord("sample"), SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse, options);
        }

        [Test]
        [Description("Needs to respect the ignorecase option if ConsiderIgnoreCase is specified")]
        public void FindNext1()
        {
            _globalSettings.IgnoreCase = true;
            var data = VimUtil.CreateSearchData("foo", options: SearchOptions.ConsiderIgnoreCase);
            AssertFindNext(data, "foo", FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Needs to respect the noignorecase option if ConsiderIgnoreCase is specified")]
        public void FindNext2()
        {
            _globalSettings.IgnoreCase = false;
            var data = new SearchData("foo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            AssertFindNext(data, "foo", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Match case if not considering smart or ignore case")]
        public void FindNext3()
        {
            _globalSettings.IgnoreCase = true;
            var data = new SearchData("foo", SearchKind.Forward, SearchOptions.None);
            AssertFindNext(data, "foo", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Verify it's using the regex and not the text")]
        public void FindNext4()
        {
            var data = VimUtil.CreateSearchData(@"\<the");
            AssertFindNext(data, @"\bthe", FindOptions.MatchCase | FindOptions.UseRegularExpressions);
        }

        [Test]
        [Description("Verify it's using the regex and not the text")]
        public void FindNext5()
        {
            var data = VimUtil.CreateSearchData(@"(the");
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
            var searchData = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 1);
            Assert.IsTrue(ret.IsFound);
            Assert.AreEqual(new SnapshotSpan(tss, 11, 3), ret.AsFound().Item2);
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
            var searchData = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsFalse(ret.IsFound);
            _factory.Verify();
        }

        [Test]
        public void FindNextMulitple3()
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
            var searchData = new SearchData("foo", SearchKind.BackwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsTrue(ret.IsFound);
            Assert.AreEqual(new SnapshotSpan(tss, 10, 3), ret.AsFound().Item2);
            _factory.Verify();
        }

        [Test]
        public void FindNextMulitple4()
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
            var searchData = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 10), nav.Object, 2);
            Assert.IsTrue(ret.IsFound);
            Assert.AreEqual(new SnapshotSpan(tss, 10, 3), ret.AsFound().Item2);
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
            var searchData = new SearchData("f(", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNext(searchData, new SnapshotPoint(tss, 0), nav.Object);
            Assert.IsTrue(ret.IsNotFound);
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
            var searchData = new SearchData("f(", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNextMultiple(searchData, new SnapshotPoint(tss, 0), nav.Object, 2);
            Assert.IsTrue(ret.IsNotFound);
            _factory.Verify();
        }

        [Test]
        public void BadRegex_NoMagicSpecifierShouldBeHandled()
        {
            var snapshot = EditorUtil.CreateBuffer("hello world");
            var nav = _factory.Create<ITextStructureNavigator>();
            var searchData = new SearchData(@"\V", SearchKind.ForwardWithWrap, SearchOptions.None);
            var ret = _search.FindNext(searchData, snapshot.GetPoint(0), nav.Object);
            Assert.IsTrue(ret.IsNotFound);
        }
    }

}
