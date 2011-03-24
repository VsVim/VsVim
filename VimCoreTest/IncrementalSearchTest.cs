using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class IncrementalSearchTest
    {
        private static SearchOptions s_options = SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase;
        private MockRepository _factory;
        private IVimData _vimData;
        private ISearchService _searchService;
        private ITextStructureNavigator _nav;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private Mock<ICommonOperations> _operations;
        private Mock<IStatusUtil> _statusUtil;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _globalSettings = new Vim.GlobalSettings();
            _globalSettings.IncrementalSearch = true;
            _localSettings = new LocalSettings(_globalSettings, _textView);
            _searchService = VimUtil.CreateSearchService(_globalSettings);
            _nav = VimUtil.CreateTextStructureNavigator(_textView.TextBuffer);
            _factory = new MockRepository(MockBehavior.Strict);
            _vimData = new VimData();
            _statusUtil = _factory.Create<IStatusUtil>();
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchBackwardWrapped));
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchForwardWrapped));
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.TextView).Returns(_textView);
            _operations.Setup(x => x.EnsureCaretOnScreenAndTextExpanded());
            _operations.Setup(x => x.EnsurePointOnScreenAndTextExpanded(It.IsAny<SnapshotPoint>()));
            _searchRaw = new IncrementalSearch(
                _operations.Object,
                _localSettings,
                _nav,
                _searchService,
                _statusUtil.Object,
                _vimData);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            var result = _search.Begin(SearchKind.ForwardWithWrap).Run(value).Run(VimKey.Enter);
            Assert.IsTrue(result.IsComplete);
        }

        /// <summary>
        /// Should continue to need more until Enter or Escape is processed
        /// </summary>
        [Test]
        public void RunSearch_NeedMoreUntilEndKey()
        {
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("b"), SearchKind.ForwardWithWrap, s_options);
            Assert.IsTrue(_search.Begin(SearchKind.ForwardWithWrap).Run("b").IsNeedMoreInput);
        }

        /// <summary>
        /// Enter should terminate the search
        /// </summary>
        [Test]
        public void RunSearch_EnterShouldComplete()
        {
            Create("foo bar");
            Assert.IsTrue(_search.Begin(SearchKind.ForwardWithWrap).Run(VimKey.Enter).IsComplete);
            _factory.Verify();
            Assert.AreEqual(_vimData.LastSearchData, new SearchData(SearchText.NewPattern(""), SearchKind.ForwardWithWrap, s_options));
        }

        /// <summary>
        /// Escape should cancel the search
        /// </summary>
        [Test]
        public void RunSearch_EscapeShouldCancel()
        {
            Create("foo bar");
            Assert.IsTrue(_search.Begin(SearchKind.ForwardWithWrap).Run(VimKey.Escape).IsCancelled);
        }

        /// <summary>
        /// Completing a search should update the LastSearch value
        /// </summary>
        [Test]
        public void LastSearch1()
        {
            Create(" foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options);
            ProcessWithEnter("foo");
            Assert.AreEqual(data, _vimData.LastSearchData);
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create(" foo bar");

            ProcessWithEnter("foo bar");
            Assert.AreEqual(new SearchData(SearchText.NewPattern("foo bar"), SearchKind.ForwardWithWrap, s_options), _vimData.LastSearchData);
            _factory.Verify();

            _textView.MoveCaretTo(0);
            ProcessWithEnter("bar");
            Assert.AreEqual(new SearchData(SearchText.NewPattern("bar"), SearchKind.ForwardWithWrap, s_options), _vimData.LastSearchData);
            _factory.Verify();
        }

        [Test]
        public void CurrentSearchUpdated_FireOnBegin()
        {
            Create("foo");
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, result) =>
                {
                    didRun = true;
                    Assert.IsTrue(result.IsNotFound);
                };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Make sure the CurrentSearchUpdated fires even if the character in question is
        /// not found
        /// </summary>
        [Test]
        public void CurrenSearchUpdated_FireOnSearhCharNotFound()
        {
            Create("foo bar");
            var didRun = false;
            var bind = _search.Begin(SearchKind.ForwardWithWrap);
            _search.CurrentSearchUpdated +=
                (unused, result) =>
                {
                    Assert.AreEqual("z", result.SearchData.Text.RawText);
                    Assert.IsTrue(result.IsNotFound);
                    didRun = true;
                };
            bind.Run("z");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrentSearchComplete_FireWhenDone()
        {
            Create("cat foo bar");
            var didRun = false;
            _search.CurrentSearchCompleted +=
                (unused, result) =>
                {
                    Assert.AreEqual("foo", result.SearchData.Text.RawText);
                    Assert.IsTrue(result.IsFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
            Assert.AreEqual(new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options), _vimData.LastSearchData);
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward).Run("B");
            Assert.AreEqual("B", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap).Run("ab");
            Assert.AreEqual("ab", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void InSearch1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            Assert.IsTrue(_search.InSearch);
        }

        [Test]
        public void InSearch2()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward).Run(VimKey.Enter);
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.IsSome());
            Assert.AreEqual(new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderSmartCase | SearchOptions.ConsiderIgnoreCase), _vimData.LastSearchData);
        }

        /// <summary>
        /// Cancelling should remove the CurrentSearch value
        /// </summary>
        [Test]
        public void InSearch3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap).Run(VimKey.Escape);
            Assert.IsFalse(_search.InSearch);
        }

        /// <summary>
        /// Backspace on a blank search should cancel
        /// </summary>
        [Test]
        public void Backspace1()
        {
            Create("foo bar");
            var result = _search.Begin(SearchKind.Forward).Run(VimKey.Back);
            Assert.IsTrue(result.IsCancelled);
        }

        /// <summary>
        /// Don't crash when backspacing with a textual value
        /// </summary>
        [Test]
        public void Backspace2()
        {
            Create("foo bar");
            var result = _search.Begin(SearchKind.Forward).Run("b").Run(VimKey.Back);
            Assert.IsTrue(result.IsNeedMoreInput);
        }

        /// <summary>
        /// Make sure we don't match the caret position when going forward.  Search starts
        /// after the caret
        /// </summary>
        [Test]
        public void SearchShouldStartAfterCaretWhenForward()
        {
            Create("foo bar");
            var result = _search.Begin(SearchKind.Forward).Run("f").Run(VimKey.Enter).AsComplete().Item;
            Assert.IsTrue(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we don't match the caret position when goiong backward.  Search starts
        /// before the caret
        /// </summary>
        [Test]
        public void SearchShouldStartBeforeCaretWhenBackward()
        {
            Create("cat bar");
            _textView.MoveCaretTo(2);
            var result = _search.Begin(SearchKind.Backward).Run("t").Run(VimKey.Enter).AsComplete().Item;
            Assert.IsTrue(result.IsNotFound);
        }

        [Test]
        public void SearchForwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchForwardWrapped)).Verifiable();
            _search.DoSearch("d");
            _statusUtil.Verify();
        }

        [Test]
        public void SearchBackwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped)).Verifiable();
            _search.DoSearch("b", SearchKind.BackwardWithWrap);
            _statusUtil.Verify();
        }

        /// <summary>
        /// Make sure we update the search history on every search found or not
        /// </summary>
        [Test]
        public void History_UpdateOnComplete()
        {
            Create("");
            _search.DoSearch("a");
            _search.DoSearch("b");
            CollectionAssert.AreEquivalent(
                new[] { "b", "a" },
                _vimData.IncrementalSearchHistory);
        }

        /// <summary>
        /// Cancelled searches should go into the history list oddly enough
        /// </summary>
        [Test]
        public void History_UpdateOnCancel()
        {
            Create("");
            _search.DoSearch("a");
            _search.DoSearch("b", enter: false).Run(VimKey.Escape);
            CollectionAssert.AreEquivalent(
                new[] { "b", "a" },
                _vimData.IncrementalSearchHistory);
        }

        /// <summary>
        /// A completed search should not create a duplicate entry in the history list. 
        /// </summary>
        [Test]
        public void History_UpdateShouldNotDuplicate()
        {
            Create("");
            _search.DoSearch("a");
            _search.DoSearch("b");
            _search.DoSearch("a");
            CollectionAssert.AreEquivalent(
                new[] { "a", "b" },
                _vimData.IncrementalSearchHistory);
        }

        /// <summary>
        /// The up key should scroll the history list
        /// </summary>
        [Test]
        public void History_UpShouldScroll()
        {
            Create("dog cat");
            _vimData.IncrementalSearchHistory = (new[] { "a", "b" }).ToFSharpList();
            _search.Begin(SearchKind.Forward).Run(VimKey.Up);
            Assert.AreEqual("a", _search.CurrentSearch.Value.Text.RawText);
        }

        /// <summary>
        /// The up key should scroll the history list repeatedly
        /// </summary>
        [Test]
        public void History_UpShouldScrollAgain()
        {
            Create("dog cat");
            _vimData.IncrementalSearchHistory = (new[] { "a", "b" }).ToFSharpList();
            _search.Begin(SearchKind.Forward).Run(VimKey.Up).Run(VimKey.Up);
            Assert.AreEqual("b", _search.CurrentSearch.Value.Text.RawText);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order
        /// </summary>
        [Test]
        public void History_DownShouldScrollBack()
        {
            Create("dog cat");
            _vimData.IncrementalSearchHistory = (new[] { "a", "b" }).ToFSharpList();
            _search.Begin(SearchKind.Forward).Run(VimKey.Up).Run(VimKey.Down);
            Assert.AreEqual("", _search.CurrentSearch.Value.Text.RawText);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order
        /// </summary>
        [Test]
        public void History_DownShouldScrollBackAfterUp()
        {
            Create("dog cat");
            _vimData.IncrementalSearchHistory = (new[] { "a", "b" }).ToFSharpList();
            _search.Begin(SearchKind.Forward).Run(VimKey.Up).Run(VimKey.Up).Run(VimKey.Down);
            Assert.AreEqual("a", _search.CurrentSearch.Value.Text.RawText);
        }

        /// <summary>
        /// Beep if the down key goes off the end of the list
        /// </summary>
        [Test]
        public void History_DownOffEndOfList()
        {
            Create("dog cat");
            _vimData.IncrementalSearchHistory = (new[] { "a", "b" }).ToFSharpList();
            _operations.Setup(x => x.Beep()).Verifiable();
            _search.Begin(SearchKind.Forward).Run(VimKey.Down);
            _operations.Verify();
        }

        /// <summary>
        /// Search through the history for a single item
        /// </summary>
        [Test]
        public void HistorySearch_OneMatch()
        {
            Create("");
            _vimData.IncrementalSearchHistory = (new[] { "dog", "cat" }).ToFSharpList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up);
            Assert.AreEqual("dog", _search.CurrentSearch.Value.Text.RawText);
        }

        /// <summary>
        /// Search through the history for an item which has several matches
        /// </summary>
        [Test]
        public void HistorySearch_TwoMatches()
        {
            Create("");
            _vimData.IncrementalSearchHistory = (new[] { "dog", "cat", "dip" }).ToFSharpList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up).Run(VimKey.Up);
            Assert.AreEqual("dip", _search.CurrentSearch.Value.Text.RawText);
        }
    }
}