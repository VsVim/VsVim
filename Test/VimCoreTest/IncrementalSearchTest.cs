﻿using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    public abstract class IncrementalSearchTest : VimTestBase
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
            var beginData = _search.Begin(SearchPath.Forward);
            var result = string.IsNullOrEmpty(value)
                ? beginData.Run(VimKey.Enter)
                : beginData.Run(value).Run(VimKey.Enter);
            Assert.True(result.IsComplete);
        }

        public sealed class RunSearchTest : IncrementalSearchTest
        {
            /// <summary>
            /// Should continue to need more until Enter or Escape is processed
            /// </summary>
            [Fact]
            public void NeedMoreUntilEndKey()
            {
                Create("foo bar");
                var data = new SearchData("b", SearchOffsetData.None, SearchKind.ForwardWithWrap, s_options);
                Assert.True(_search.Begin(SearchPath.Forward).Run("b").IsNeedMoreInput);
            }

            /// <summary>
            /// Enter should terminate the search
            /// </summary>
            [Fact]
            public void EnterShouldComplete()
            {
                Create("foo bar");
                Assert.True(_search.Begin(SearchPath.Forward).Run("f").Run(VimKey.Enter).IsComplete);
                _factory.Verify();
                Assert.Equal("f", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Escape should cancel the search
            /// </summary>
            [Fact]
            public void EscapeShouldCancel()
            {
                Create("foo bar");
                Assert.True(_search.Begin(SearchPath.Forward).Run(VimKey.Escape).IsCancelled);
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
            public void EmptyShouldUseLast()
            {
                Create("foo bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                ProcessWithEnter("");
                Assert.Equal("foo", _vimData.LastSearchData.Pattern);
            }
        }

        public sealed class MiscTest : IncrementalSearchTest
        {
            [Fact]
            public void LastSearch2()
            {
                Create(" foo bar");

                ProcessWithEnter("foo bar");
                Assert.Equal("foo bar", _vimData.LastSearchData.Pattern);
                _factory.Verify();

                _textView.MoveCaretTo(0);
                ProcessWithEnter("bar");
                Assert.Equal("bar", _vimData.LastSearchData.Pattern);
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
                _search.Begin(SearchPath.Forward);
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
                var bind = _search.Begin(SearchPath.Forward);
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
                Assert.Equal(new SearchData("foo", SearchPath.Forward), _vimData.LastSearchData);
            }

            [Fact]
            public void CurrentSearch1()
            {
                Create("foo bar");
                _search.Begin(SearchPath.Forward).Run("B");
                Assert.Equal("B", _search.CurrentSearchData.Pattern);
            }

            [Fact]
            public void CurrentSearch3()
            {
                Create("foo bar");
                _search.Begin(SearchPath.Forward).Run("ab");
                Assert.Equal("ab", _search.CurrentSearchData.Pattern);
            }

            [Fact]
            public void InSearch1()
            {
                Create("foo bar");
                _search.Begin(SearchPath.Forward);
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
                _search.Begin(SearchPath.Forward).Run(VimKey.Escape);
                Assert.False(_search.InSearch);
            }

            /// <summary>
            /// Backspace on a blank search should cancel
            /// </summary>
            [Fact]
            public void Backspace_NoText()
            {
                Create("foo bar");
                var result = _search.Begin(SearchPath.Forward).Run(VimKey.Back);
                Assert.True(result.IsCancelled);
            }

            /// <summary>
            /// Don't crash when backspacing with a textual value
            /// </summary>
            [Fact]
            public void Backspace_WithText()
            {
                Create("foo bar");
                var result = _search.Begin(SearchPath.Forward).Run("b").Run(VimKey.Back);
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
                var result = _search.Begin(SearchPath.Forward).Run("f").Run(VimKey.Enter).AsComplete().Item;
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
                var result = _search.Begin(SearchPath.Backward).Run("t").Run(VimKey.Enter).AsComplete().Item;
                Assert.True(result.IsNotFound);
            }
        }

        public sealed class HistoryTest : IncrementalSearchTest
        {
            /// <summary>
            /// Make sure we update the search history on every search found or not
            /// </summary>
            [Fact]
            public void UpdateOnComplete()
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
            public void UpdateOnCancel()
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
            public void UpdateShouldNotDuplicate()
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
            public void UpShouldScroll()
            {
                Create("cat bear");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                _search.Begin(SearchPath.Forward).Run(VimKey.Up);
                Assert.Equal("a", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The up key should scroll the history list repeatedly
            /// </summary>
            [Fact]
            public void UpShouldScrollAgain()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                _search.Begin(SearchPath.Forward).Run(VimKey.Up).Run(VimKey.Up);
                Assert.Equal("b", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The down key should scroll the history list in the opposite order.  When it 
            /// reaches the end it should go back to a blank
            /// </summary>
            [Fact]
            public void DownShouldScrollBack()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                _search.Begin(SearchPath.Forward).Run(VimKey.Up).Run(VimKey.Down);
                Assert.Equal("", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The down key should scroll the history list in the opposite order
            /// </summary>
            [Fact]
            public void DownShouldScrollBackAfterUp()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                _search.Begin(SearchPath.Forward).Run(VimKey.Up, VimKey.Up, VimKey.Down);
                Assert.Equal("a", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// Beep if the down key goes off the end of the list
            /// </summary>
            [Fact]
            public void DownOffEndOfList()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                _search.Begin(SearchPath.Forward).Run(VimKey.Down);
                Assert.Equal(1, _vimHost.BeepCount);
            }

            /// <summary>
            /// Search through the history for a single item
            /// </summary>
            [Fact]
            public void OneMatch()
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
            public void TwoMatches()
            {
                Create("");
                _vimData.SearchHistory = (new[] { "dog", "cat", "dip" }).ToHistoryList();
                _search.DoSearch("d", enter: false).Run(VimKey.Up).Run(VimKey.Up);
                Assert.Equal("dip", _search.CurrentSearchData.Pattern);
            }
        }

        public sealed class CancelTest : IncrementalSearchTest
        {
            [Fact]
            public void InSearchProperty()
            {
                Create("hello world");
                _search.DoSearch("wo", enter: false);
                Assert.True(_search.InSearch);
                _search.Cancel();
                Assert.False(_search.InSearch);
            }

            /// <summary>
            /// Make sure we can repeat the cancel many times and get the same result
            /// </summary>
            [Fact]
            public void ManyTimes()
            {
                Create("hello world");
                for (int i = 0; i < 5; i++)
                {
                    _textView.MoveCaretTo(0);
                    var searchResult = _search.DoSearch("el", enter: true).AsComplete().Item;
                    Assert.True(searchResult.IsFound);
                    var span = searchResult.AsFound().Item2;
                    Assert.Equal(new Span(1, 2), span.Span);
                    _search.DoSearch("wo", enter: false);
                    Assert.True(_search.InSearch);
                    _search.Cancel();
                    Assert.False(_search.InSearch);
                }
            }
        }
    }
}