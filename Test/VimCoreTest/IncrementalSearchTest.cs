using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class IncrementalSearchTest : VimTestBase
    {
        private static SearchOptions s_options = SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase;
        private MockRepository _factory;
        private IVimData _vimData;
        private IVimGlobalSettings _globalSettings;
        private MockVimHost _vimHost;
        private Mock<IStatusUtil> _statusUtil;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _vimHost = (MockVimHost)Vim.VimHost;
            _textView = CreateTextView(lines);
            _globalSettings = Vim.GlobalSettings;
            _globalSettings.IncrementalSearch = true;
            _globalSettings.WrapScan = true;

            var vimTextBuffer = Vim.CreateVimTextBuffer(_textView.TextBuffer);

            _factory = new MockRepository(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>();
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped));
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchForwardWrapped));

            _vimData = Vim.VimData;
            var vimBufferData = CreateVimBufferData(vimTextBuffer, _textView);
            var operations = CommonOperationsFactory.GetCommonOperations(vimBufferData);
            _searchRaw = new IncrementalSearch(vimBufferData, operations);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            var result = _search.Begin(Path.Forward).Run(value).Run(VimKey.Enter);
            Assert.True(result.IsComplete);
        }

        /// <summary>
        /// Should continue to need more until Enter or Escape is processed
        /// </summary>
        [Fact]
        public void RunSearch_NeedMoreUntilEndKey()
        {
            Create("foo bar");
            var data = new SearchData("b", SearchOffsetData.None, SearchKind.ForwardWithWrap, s_options);
            Assert.True(_search.Begin(Path.Forward).Run("b").IsNeedMoreInput);
        }

        /// <summary>
        /// Enter should terminate the search
        /// </summary>
        [Fact]
        public void RunSearch_EnterShouldComplete()
        {
            Create("foo bar");
            Assert.True(_search.Begin(Path.Forward).Run("f").Run(VimKey.Enter).IsComplete);
            _factory.Verify();
            Assert.Equal("f", _vimData.LastSearchData.Pattern);
        }

        /// <summary>
        /// Escape should cancel the search
        /// </summary>
        [Fact]
        public void RunSearch_EscapeShouldCancel()
        {
            Create("foo bar");
            Assert.True(_search.Begin(Path.Forward).Run(VimKey.Escape).IsCancelled);
        }

        /// <summary>
        /// Completing a search should update the LastSearch value
        /// </summary>
        [Fact]
        public void LastSearch1()
        {
            Create(" foo bar");
            ProcessWithEnter("foo");
            Assert.Equal("foo", _vimData.LastSearchData.Pattern);
            Assert.True(_vimData.LastSearchData.Kind.IsAnyForward);
            _factory.Verify();
        }

        [Fact]
        public void LastSearch2()
        {
            Create(" foo bar");

            ProcessWithEnter("foo bar");
            Assert.Equal("foo bar", _vimData.LastSearchData.Pattern);
            _factory.Verify();

            _textView.MoveCaretTo(0);
            ProcessWithEnter("bar");
            Assert.Equal("bar",  _vimData.LastSearchData.Pattern);
            _factory.Verify();
        }

        [Fact]
        public void CurrentSearchUpdated_FireOnBegin()
        {
            Create("foo");
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, args) =>
                {
                    didRun = true;
                    Assert.True(args.SearchResult.IsNotFound);
                };
            _search.Begin(Path.Forward);
            Assert.True(didRun);
        }

        /// <summary>
        /// Make sure the CurrentSearchUpdated fires even if the character in question is
        /// not found
        /// </summary>
        [Fact]
        public void CurrentSearchUpdated_FireOnSearhCharNotFound()
        {
            Create("foo bar");
            var didRun = false;
            var bind = _search.Begin(Path.Forward);
            _search.CurrentSearchUpdated +=
                (unused, args) =>
                {
                    Assert.Equal("z", args.SearchResult.SearchData.Pattern);
                    Assert.True(args.SearchResult.IsNotFound);
                    didRun = true;
                };
            bind.Run("z");
            Assert.True(didRun);
        }

        [Fact]
        public void CurrentSearchComplete_FireWhenDone()
        {
            Create("cat foo bar");
            var didRun = false;
            _search.CurrentSearchCompleted +=
                (unused, args) =>
                {
                    Assert.Equal("foo", args.SearchResult.SearchData.Pattern);
                    Assert.True(args.SearchResult.IsFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.True(didRun);
            Assert.Equal(new SearchData("foo", Path.Forward), _vimData.LastSearchData);
        }

        [Fact]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run("B");
            Assert.Equal("B", _search.CurrentSearchData.Pattern);
        }

        [Fact]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run("ab");
            Assert.Equal("ab", _search.CurrentSearchData.Pattern);
        }

        [Fact]
        public void InSearch1()
        {
            Create("foo bar");
            _search.Begin(Path.Forward);
            Assert.True(_search.InSearch);
        }

        [Fact]
        public void InSearch2()
        {
            Create("foo bar");
            _search.DoSearch("foo");
            Assert.False(_search.InSearch);
            Assert.Equal("foo", _vimData.LastSearchData.Pattern);
        }

        /// <summary>
        /// Cancelling should remove the CurrentSearch value
        /// </summary>
        [Fact]
        public void InSearch3()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run(VimKey.Escape);
            Assert.False(_search.InSearch);
        }

        /// <summary>
        /// Backspace on a blank search should cancel
        /// </summary>
        [Fact]
        public void Backspace_NoText()
        {
            Create("foo bar");
            var result = _search.Begin(Path.Forward).Run(VimKey.Back);
            Assert.True(result.IsCancelled);
        }

        /// <summary>
        /// Don't crash when backspacing with a textual value
        /// </summary>
        [Fact]
        public void Backspace_WithText()
        {
            Create("foo bar");
            var result = _search.Begin(Path.Forward).Run("b").Run(VimKey.Back);
            Assert.True(result.IsNeedMoreInput);
        }

        /// <summary>
        /// Make sure we don't match the caret position when going forward.  Search starts
        /// after the caret
        /// </summary>
        [Fact]
        public void Search_ShouldStartAfterCaretWhenForward()
        {
            Create("foo bar");
            _globalSettings.WrapScan = false;
            var result = _search.Begin(Path.Forward).Run("f").Run(VimKey.Enter).AsComplete().Item;
            Assert.True(result.IsNotFound);
            Assert.True(result.AsNotFound().Item2);
        }

        /// <summary>
        /// Make sure we don't match the caret position when going backward.  Search starts
        /// before the caret
        /// </summary>
        [Fact]
        public void Search_ShouldStartBeforeCaretWhenBackward()
        {
            Create("cat bar");
            _globalSettings.WrapScan = false;
            _textView.MoveCaretTo(2);
            var result = _search.Begin(Path.Backward).Run("t").Run(VimKey.Enter).AsComplete().Item;
            Assert.True(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we update the search history on every search found or not
        /// </summary>
        [Fact]
        public void History_UpdateOnComplete()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b");
            Assert.Equal(
                new[] { "b", "a" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// Cancelled searches should go into the history list oddly enough
        /// </summary>
        [Fact]
        public void History_UpdateOnCancel()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b", enter: false).Run(VimKey.Escape);
            Assert.Equal(
                new[] { "b", "a" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// A completed search should not create a duplicate entry in the history list. 
        /// </summary>
        [Fact]
        public void History_UpdateShouldNotDuplicate()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b");
            _search.DoSearch("a");
            Assert.Equal(
                new[] { "a", "b" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// The up key should scroll the history list
        /// </summary>
        [Fact]
        public void History_UpShouldScroll()
        {
            Create("cat bear");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up);
            Assert.Equal("a", _search.CurrentSearchData.Pattern);
        }

        /// <summary>
        /// The up key should scroll the history list repeatedly
        /// </summary>
        [Fact]
        public void History_UpShouldScrollAgain()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up).Run(VimKey.Up);
            Assert.Equal("b", _search.CurrentSearchData.Pattern);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order.  When it 
        /// reaches the end it should go back to a blank
        /// </summary>
        [Fact]
        public void History_DownShouldScrollBack()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up).Run(VimKey.Down);
            Assert.Equal("", _search.CurrentSearchData.Pattern);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order
        /// </summary>
        [Fact]
        public void History_DownShouldScrollBackAfterUp()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up, VimKey.Up, VimKey.Down);
            Assert.Equal("a", _search.CurrentSearchData.Pattern);
        }

        /// <summary>
        /// Beep if the down key goes off the end of the list
        /// </summary>
        [Fact]
        public void History_DownOffEndOfList()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Down);
            Assert.Equal(1, _vimHost.BeepCount);
        }

        /// <summary>
        /// Search through the history for a single item
        /// </summary>
        [Fact]
        public void HistorySearch_OneMatch()
        {
            Create("");
            _vimData.SearchHistory = (new[] { "dog", "cat" }).ToHistoryList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up);
            Assert.Equal("dog", _search.CurrentSearchData.Pattern);
        }

        /// <summary>
        /// Search through the history for an item which has several matches
        /// </summary>
        [Fact]
        public void HistorySearch_TwoMatches()
        {
            Create("");
            _vimData.SearchHistory = (new[] { "dog", "cat", "dip" }).ToHistoryList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up).Run(VimKey.Up);
            Assert.Equal("dip", _search.CurrentSearchData.Pattern);
        }
    }
}