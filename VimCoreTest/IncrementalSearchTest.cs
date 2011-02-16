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
        private Mock<IVimData> _vimData;
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
            _localSettings = new LocalSettings(_globalSettings, _textView);
            _searchService = VimUtil.CreateSearchService(_globalSettings);
            _nav = VimUtil.CreateTextStructureNavigator(_textView.TextBuffer);
            _factory = new MockRepository(MockBehavior.Strict);
            _vimData = _factory.Create<IVimData>();
            _statusUtil = _factory.Create<IStatusUtil>();
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
                _vimData.Object);
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
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            Assert.IsTrue(_search.Begin(SearchKind.ForwardWithWrap).Run(VimKey.Enter).IsComplete);
            _factory.Verify();
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
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options);
            _vimData.SetupSet(x => x.LastSearchData = data).Verifiable();
            ProcessWithEnter("foo");
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create("foo bar");

            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("foo bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("foo bar");
            _factory.Verify();

            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("bar");
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
                    Assert.IsTrue(result.IsSearchNotFound);
                };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrenSearchUpdated_FireOnSearhCharNotFound()
        {
            Create("foo bar");
            var didRun = false;
            _search.CurrentSearchUpdated +=
                (unused, result) =>
                {
                    Assert.AreEqual("z", result.SearchData.Text.RawText);
                    Assert.IsTrue(result.IsSearchNotFound);
                    didRun = true;
                };
            _search.Begin(SearchKind.ForwardWithWrap).Run("z");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrentSearchComplete_FireWhenDone()
        {
            Create("foo bar");
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            var didRun = false;
            _search.CurrentSearchCompleted +=
                (unused, result) =>
                {
                    Assert.AreEqual("foo", result.SearchData.Text.RawText);
                    Assert.IsTrue(result.IsSearchFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
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
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderSmartCase | SearchOptions.ConsiderIgnoreCase));
            _search.Begin(SearchKind.Forward).Run(VimKey.Enter);
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.IsSome());
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
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.None));
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
            Assert.IsTrue(result.IsSearchNotFound);
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
            Assert.IsTrue(result.IsSearchNotFound);
        }

        [Test]
        public void SearchForwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _vimData.SetupSet(x => x.LastSearchData = It.IsAny<SearchData>());
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchForwardWrapped)).Verifiable();
            _search.DoSearch("d");
            _statusUtil.Verify();
        }

        [Test]
        public void SearchBackwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _vimData.SetupSet(x => x.LastSearchData = It.IsAny<SearchData>());
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchBackwardWrapped)).Verifiable();
            _search.DoSearch("b", SearchKind.BackwardWithWrap);
            _statusUtil.Verify();
        }
    }
}