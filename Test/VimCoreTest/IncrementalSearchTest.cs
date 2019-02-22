using Vim.EditorHost;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Microsoft.VisualStudio.Text;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace Vim.UnitTest
{
    public abstract class IncrementalSearchTest : VimTestBase
    {
        private static readonly SearchOptions s_options = SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase;
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
            VimSynchronizationContext.IsDispatchEnabled = true;

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

        private async Task ProcessWithEnter(string value)
        {
            var keyInputs = string.IsNullOrEmpty(value)
                ? new[] { KeyInputUtil.EnterKey }
                : VimUtil.ConvertTextToKeyInput(value, enter: true);
            await _search.DoSearchAsync(keyInputs);
        }

        public sealed class RunSearchTest : IncrementalSearchTest
        {
            public RunSearchTest()
            {
                VimSynchronizationContext.IsDispatchEnabled = true;
            }

            /// <summary>
            /// Should continue to need more until Enter or Escape is processed
            /// </summary>
            [WpfFact]
            public async Task NeedMoreUntilEndKey()
            {
                Create("foo bar");
                var data = new SearchData("b", SearchOffsetData.None, SearchKind.ForwardWithWrap, s_options);
                var bindData = await _search.DoSearchAsync(SearchPath.Forward, "b", enter: false);
                Assert.True(bindData.IsNeedMoreInput);
            }

            /// <summary>
            /// Enter should terminate the search
            /// </summary>
            [WpfFact]
            public async Task EnterShouldComplete()
            {
                Create("foo bar");
                var bindData = await _search.DoSearchAsync(SearchPath.Forward, "f");
                Assert.True(bindData.IsComplete);
                _factory.Verify();
                Assert.Equal("f", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Escape should cancel the search
            /// </summary>
            [WpfFact]
            public async Task EscapeShouldCancel()
            {
                Create("foo bar");
                var bindData = await _search.DoSearchAsync(SearchPath.Forward, VimKey.Escape);
                Assert.True(bindData.IsCancelled);
            }

            /// <summary>
            /// Completing a search should update the LastSearch value
            /// </summary>
            [WpfFact]
            public async Task LastSearch1()
            {
                Create(" foo bar");
                await ProcessWithEnter("foo");
                Assert.Equal("foo", _vimData.LastSearchData.Pattern);
                Assert.True(_vimData.LastSearchData.Kind.IsAnyForward);
                _factory.Verify();
            }

            [WpfFact]
            public async Task EmptyShouldUseLast()
            {
                Create("foo bar");
                _vimData.LastSearchData = VimUtil.CreateSearchData("foo");
                await ProcessWithEnter("");
                Assert.Equal("foo", _vimData.LastSearchData.Pattern);
            }
        }

        public sealed class MiscTest : IncrementalSearchTest
        {
            [WpfFact]
            public async Task LastSearch2()
            {
                Create(" foo bar");

                await ProcessWithEnter("foo bar");
                Assert.Equal("foo bar", _vimData.LastSearchData.Pattern);
                _factory.Verify();

                _textView.MoveCaretTo(0);
                await ProcessWithEnter("bar");
                Assert.Equal("bar", _vimData.LastSearchData.Pattern);
                _factory.Verify();
            }

            [WpfFact]
            public void SessionCreated_FireOnBegin()
            {
                Create("foo");
                IIncrementalSearchSession eventSession = null; 
                _search.SessionCreated += (unused, args) =>
                    {
                        eventSession = args.Session;
                    };
                var session = _search.CreateSession(SearchPath.Forward);
                Assert.Same(session, eventSession);
                Assert.Equal(SearchPath.Forward, session.SearchData.Path);
            }

            /// <summary>
            /// Make sure the CurrentSearchUpdated fires even if the character in question is
            /// not found
            /// </summary>
            [WpfFact]
            public async Task CurrentSearchUpdated_FireOnSearhCharNotFound()
            {
                Create("foo bar");
                var session = _search.CreateSession(SearchPath.Forward);
                var runCount = 0;
                session.SearchStart += (_, args) =>
                    {
                        Assert.Equal("z", args.SearchData.Pattern);
                        runCount++;
                    };

                session.SearchEnd += (_, args) =>
                    {
                        Assert.True(args.SearchResult.IsNotFound);
                        runCount++;
                    };
                await session.DoSearchAsync("z");
                Assert.Equal(2, runCount);
            }

            [WpfFact]
            public async Task CurrentSearch1()
            {
                Create("foo bar");
                await _search.DoSearchAsync(SearchPath.Forward, "B", enter: false);
                Assert.Equal("B", _search.CurrentSearchData.Pattern);
            }

            [WpfFact]
            public async Task CurrentSearch3()
            {
                Create("foo bar");
                await _search.DoSearchAsync(SearchPath.Forward, "ab", enter: false);
                Assert.Equal("ab", _search.CurrentSearchData.Pattern);
            }

            [WpfFact]
            public void InSearch1()
            {
                Create("foo bar");
                _search.CreateSession(SearchPath.Forward);
                Assert.True(_search.HasActiveSession);
            }

            [WpfFact]
            public async Task InSearch2()
            {
                Create("foo bar");
                await _search.DoSearchAsync("foo");
                Assert.False(_search.HasActiveSession);
                Assert.Equal("foo", _vimData.LastSearchData.Pattern);
            }

            /// <summary>
            /// Cancelling should remove the CurrentSearch value
            /// </summary>
            [WpfFact]
            public async Task InSearch3()
            {
                Create("foo bar");
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Escape);
                Assert.False(_search.HasActiveSession);
            }

            /// <summary>
            /// Backspace on a blank search should cancel
            /// </summary>
            [WpfFact]
            public async Task Backspace_NoText()
            {
                Create("foo bar");
                var result = await _search.DoSearchAsync(SearchPath.Forward, VimKey.Back);
                Assert.True(result.IsCancelled);
            }

            /// <summary>
            /// Don't crash when backspacing with a textual value
            /// </summary>
            [WpfFact]
            public async Task Backspace_WithText()
            {
                Create("foo bar");
                var result = await _search.DoSearchAsync("b");
                result = result.Run(VimKey.Back);
                Assert.True(result.IsNeedMoreInput);
            }

            /// <summary>
            /// Make sure we don't match the caret position when going forward.  Search starts
            /// after the caret
            /// </summary>
            [WpfFact]
            public async Task Search_ShouldStartAfterCaretWhenForward()
            {
                Create("foo bar");
                _globalSettings.WrapScan = false;
                var bindResult = await _search.DoSearchAsync("f");
                bindResult = bindResult.Run(VimKey.Enter);
                var result = bindResult.AsComplete().Result;
                Assert.True(result.IsNotFound);
                Assert.True(result.AsNotFound().CanFindWithWrap);
            }

            /// <summary>
            /// Make sure we don't match the caret position when going backward.  Search starts
            /// before the caret
            /// </summary>
            [WpfFact]
            public async Task Search_ShouldStartBeforeCaretWhenBackward()
            {
                Create("cat bar");
                _globalSettings.WrapScan = false;
                _textView.MoveCaretTo(2);
                var result = (await _search.DoSearchAsync("t", enter: true)).AsComplete().Result;
                Assert.True(result.IsNotFound);
            }
        }

        public sealed class HistoryTest : IncrementalSearchTest
        {
            /// <summary>
            /// Make sure we update the search history on every search found or not
            /// </summary>
            [WpfFact]
            public async Task UpdateOnComplete()
            {
                Create("cat bear");
                await _search.DoSearchAsync("a");
                await _search.DoSearchAsync("b");
                Assert.Equal(
                    new[] { "b", "a" },
                    _vimData.SearchHistory);
            }

            /// <summary>
            /// Cancelled searches should go into the history list oddly enough
            /// </summary>
            [WpfFact]
            public async Task UpdateOnCancel()
            {
                Create("cat bear");
                await _search.DoSearchAsync("a");
                var result = await _search.DoSearchAsync("b", enter: false);
                result.Run(VimKey.Escape);
                Assert.Equal(
                    new[] { "b", "a" },
                    _vimData.SearchHistory);
            }

            /// <summary>
            /// A completed search should not create a duplicate entry in the history list. 
            /// </summary>
            [WpfFact]
            public async Task UpdateShouldNotDuplicate()
            {
                Create("cat bear");
                await _search.DoSearchAsync("a");
                await _search.DoSearchAsync("b");
                await _search.DoSearchAsync("a");
                Assert.Equal(
                    new[] { "a", "b" },
                    _vimData.SearchHistory);
            }

            /// <summary>
            /// The up key should scroll the history list
            /// </summary>
            [WpfFact]
            public async Task UpShouldScroll()
            {
                Create("cat bear");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Up);
                Assert.Equal("a", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The up key should scroll the history list repeatedly
            /// </summary>
            [WpfFact]
            public async Task UpShouldScrollAgain()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Up, VimKey.Up);
                Assert.Equal("b", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The down key should scroll the history list in the opposite order.  When it 
            /// reaches the end it should go back to a blank
            /// </summary>
            [WpfFact]
            public async Task DownShouldScrollBack()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Up, VimKey.Down);
                Assert.Equal("", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// The down key should scroll the history list in the opposite order
            /// </summary>
            [WpfFact]
            public async Task DownShouldScrollBackAfterUp()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Up, VimKey.Up, VimKey.Down);
                Assert.Equal("a", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// Beep if the down key goes off the end of the list
            /// </summary>
            [WpfFact]
            public async Task DownOffEndOfList()
            {
                Create("dog cat");
                _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
                await _search.DoSearchAsync(SearchPath.Forward, VimKey.Down);
                Assert.Equal(1, _vimHost.BeepCount);
            }

            /// <summary>
            /// Search through the history for a single item
            /// </summary>
            [WpfFact]
            public async Task OneMatch()
            {
                Create("");
                _vimData.SearchHistory = (new[] { "dog", "cat" }).ToHistoryList();
                var result = await _search.DoSearchAsync("d", enter: false);
                result.Run(VimKey.Up);
                Assert.Equal("dog", _search.CurrentSearchData.Pattern);
            }

            /// <summary>
            /// Search through the history for an item which has several matches
            /// </summary>
            [WpfFact]
            public async Task TwoMatches()
            {
                Create("");
                _vimData.SearchHistory = (new[] { "dog", "cat", "dip" }).ToHistoryList();
                var result = await _search.DoSearchAsync("d", enter: false);
                result.Run(VimKey.Up, VimKey.Up);
                Assert.Equal("dip", _search.CurrentSearchData.Pattern);
            }
        }

        public sealed class CancelTest : IncrementalSearchTest
        {
            [WpfFact]
            public async Task InSearchProperty()
            {
                Create("hello world");
                await _search.DoSearchAsync("wo", enter: false);
                Assert.True(_search.HasActiveSession);
                _search.CancelSession();
                Assert.False(_search.HasActiveSession);
            }

            /// <summary>
            /// Make sure we can repeat the cancel many times and get the same result
            /// </summary>
            [WpfFact]
            public async Task ManyTimes()
            {
                Create("hello world");
                for (var i = 0; i < 5; i++)
                {
                    _textView.MoveCaretTo(0);
                    var bindResult = await _search.DoSearchAsync("el", enter: true);
                    var searchResult = bindResult.AsComplete().Result;
                    Assert.True(searchResult.IsFound);
                    var span = searchResult.AsFound().SpanWithOffset;
                    Assert.Equal(new Span(1, 2), span.Span);
                    await _search.DoSearchAsync("wo", enter: false);
                    Assert.True(_search.HasActiveSession);
                    _search.CancelSession();
                    Assert.False(_search.HasActiveSession);
                }
            }
        }
    }
}