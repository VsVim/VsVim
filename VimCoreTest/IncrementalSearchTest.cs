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
        private ITextStructureNavigator _nav;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private ICommonOperations _operations;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IVimHost> _vimHost;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _globalSettings = new Vim.GlobalSettings();
            _globalSettings.IncrementalSearch = true;
            _globalSettings.WrapScan = true;
            _localSettings = new LocalSettings(_globalSettings, EditorUtil.GetEditorOptions(_textView), _textView);
            _nav = VimUtil.CreateTextStructureNavigator(_textView, WordKind.NormalWord);
            _factory = new MockRepository(MockBehavior.Strict);
            _vimHost = _factory.Create<IVimHost>();
            _vimHost.Setup(x => x.EnsureVisible(_textView, It.IsAny<SnapshotPoint>()));
            _statusUtil = _factory.Create<IStatusUtil>();
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchBackwardWrapped));
            _statusUtil.Setup(x => x.OnWarning(Resources.Common_SearchForwardWrapped));
            _vimData = new VimData();
            _operations = VimUtil.CreateCommonOperations(
                textView: _textView,
                localSettings: _localSettings,
                vimHost: _vimHost.Object,
                statusUtil: _statusUtil.Object);
            _searchRaw = new IncrementalSearch(
                _operations,
                _localSettings,
                _nav,
                _statusUtil.Object,
                _vimData);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            var result = _search.Begin(Path.Forward).Run(value).Run(VimKey.Enter);
            Assert.IsTrue(result.IsComplete);
        }

        /// <summary>
        /// Should continue to need more until Enter or Escape is processed
        /// </summary>
        [Test]
        public void RunSearch_NeedMoreUntilEndKey()
        {
            Create("foo bar");
            var data = new SearchData("b", SearchKind.ForwardWithWrap, s_options);
            Assert.IsTrue(_search.Begin(Path.Forward).Run("b").IsNeedMoreInput);
        }

        /// <summary>
        /// Enter should terminate the search
        /// </summary>
        [Test]
        public void RunSearch_EnterShouldComplete()
        {
            Create("foo bar");
            Assert.IsTrue(_search.Begin(Path.Forward).Run("f").Run(VimKey.Enter).IsComplete);
            _factory.Verify();
            Assert.AreEqual("f", _vimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Escape should cancel the search
        /// </summary>
        [Test]
        public void RunSearch_EscapeShouldCancel()
        {
            Create("foo bar");
            Assert.IsTrue(_search.Begin(Path.Forward).Run(VimKey.Escape).IsCancelled);
        }

        /// <summary>
        /// Completing a search should update the LastSearch value
        /// </summary>
        [Test]
        public void LastSearch1()
        {
            Create(" foo bar");
            var data = VimUtil.CreatePatternData("foo", Path.Forward);
            ProcessWithEnter("foo");
            Assert.AreEqual(data, _vimData.LastPatternData);
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create(" foo bar");

            ProcessWithEnter("foo bar");
            Assert.AreEqual(VimUtil.CreatePatternData("foo bar", Path.Forward), _vimData.LastPatternData);
            _factory.Verify();

            _textView.MoveCaretTo(0);
            ProcessWithEnter("bar");
            Assert.AreEqual(VimUtil.CreatePatternData("bar", Path.Forward), _vimData.LastPatternData);
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
            _search.Begin(Path.Forward);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Make sure the CurrentSearchUpdated fires even if the character in question is
        /// not found
        /// </summary>
        [Test]
        public void CurrentSearchUpdated_FireOnSearhCharNotFound()
        {
            Create("foo bar");
            var didRun = false;
            var bind = _search.Begin(Path.Forward);
            _search.CurrentSearchUpdated +=
                (unused, result) =>
                {
                    Assert.AreEqual("z", result.SearchData.Pattern);
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
                    Assert.AreEqual("foo", result.SearchData.Pattern);
                    Assert.IsTrue(result.IsFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
            Assert.AreEqual(VimUtil.CreatePatternData("foo", Path.Forward), _vimData.LastPatternData);
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run("B");
            Assert.AreEqual("B", _search.CurrentSearch.Value.Pattern);
        }

        [Test]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run("ab");
            Assert.AreEqual("ab", _search.CurrentSearch.Value.Pattern);
        }

        [Test]
        public void InSearch1()
        {
            Create("foo bar");
            _search.Begin(Path.Forward);
            Assert.IsTrue(_search.InSearch);
        }

        [Test]
        public void InSearch2()
        {
            Create("foo bar");
            _search.DoSearch("foo");
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.IsSome());
            Assert.AreEqual("foo", _vimData.LastPatternData.Pattern);
        }

        /// <summary>
        /// Cancelling should remove the CurrentSearch value
        /// </summary>
        [Test]
        public void InSearch3()
        {
            Create("foo bar");
            _search.Begin(Path.Forward).Run(VimKey.Escape);
            Assert.IsFalse(_search.InSearch);
        }

        /// <summary>
        /// Backspace on a blank search should cancel
        /// </summary>
        [Test]
        public void Backspace_NoText()
        {
            Create("foo bar");
            var result = _search.Begin(Path.Forward).Run(VimKey.Back);
            Assert.IsTrue(result.IsCancelled);
        }

        /// <summary>
        /// Don't crash when backspacing with a textual value
        /// </summary>
        [Test]
        public void Backspace_WithText()
        {
            Create("foo bar");
            var result = _search.Begin(Path.Forward).Run("b").Run(VimKey.Back);
            Assert.IsTrue(result.IsNeedMoreInput);
        }

        /// <summary>
        /// Make sure we don't match the caret position when going forward.  Search starts
        /// after the caret
        /// </summary>
        [Test]
        public void Search_ShouldStartAfterCaretWhenForward()
        {
            Create("foo bar");
            _globalSettings.WrapScan = false;
            var result = _search.Begin(Path.Forward).Run("f").Run(VimKey.Enter).AsComplete().Item;
            Assert.IsTrue(result.IsNotFound);
            Assert.IsTrue(result.AsNotFound().Item2);
        }

        /// <summary>
        /// Make sure we don't match the caret position when going backward.  Search starts
        /// before the caret
        /// </summary>
        [Test]
        public void Search_ShouldStartBeforeCaretWhenBackward()
        {
            Create("cat bar");
            _globalSettings.WrapScan = false;
            _textView.MoveCaretTo(2);
            var result = _search.Begin(Path.Backward).Run("t").Run(VimKey.Enter).AsComplete().Item;
            Assert.IsTrue(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we update the search history on every search found or not
        /// </summary>
        [Test]
        public void History_UpdateOnComplete()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b");
            CollectionAssert.AreEquivalent(
                new[] { "b", "a" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// Cancelled searches should go into the history list oddly enough
        /// </summary>
        [Test]
        public void History_UpdateOnCancel()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b", enter: false).Run(VimKey.Escape);
            CollectionAssert.AreEquivalent(
                new[] { "b", "a" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// A completed search should not create a duplicate entry in the history list. 
        /// </summary>
        [Test]
        public void History_UpdateShouldNotDuplicate()
        {
            Create("cat bear");
            _search.DoSearch("a");
            _search.DoSearch("b");
            _search.DoSearch("a");
            CollectionAssert.AreEquivalent(
                new[] { "a", "b" },
                _vimData.SearchHistory);
        }

        /// <summary>
        /// The up key should scroll the history list
        /// </summary>
        [Test]
        public void History_UpShouldScroll()
        {
            Create("cat bear");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up);
            Assert.AreEqual("a", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// The up key should scroll the history list repeatedly
        /// </summary>
        [Test]
        public void History_UpShouldScrollAgain()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up).Run(VimKey.Up);
            Assert.AreEqual("b", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order.  When it 
        /// reaches the end it should go back to a blank
        /// </summary>
        [Test]
        public void History_DownShouldScrollBack()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up).Run(VimKey.Down);
            Assert.AreEqual("", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// The down key should scroll the history list in the opposite order
        /// </summary>
        [Test]
        public void History_DownShouldScrollBackAfterUp()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _search.Begin(Path.Forward).Run(VimKey.Up, VimKey.Up, VimKey.Down);
            Assert.AreEqual("a", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// Beep if the down key goes off the end of the list
        /// </summary>
        [Test]
        public void History_DownOffEndOfList()
        {
            Create("dog cat");
            _vimData.SearchHistory = (new[] { "a", "b" }).ToHistoryList();
            _vimHost.Setup(x => x.Beep()).Verifiable();
            _search.Begin(Path.Forward).Run(VimKey.Down);
            _vimHost.Verify();
        }

        /// <summary>
        /// Search through the history for a single item
        /// </summary>
        [Test]
        public void HistorySearch_OneMatch()
        {
            Create("");
            _vimData.SearchHistory = (new[] { "dog", "cat" }).ToHistoryList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up);
            Assert.AreEqual("dog", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// Search through the history for an item which has several matches
        /// </summary>
        [Test]
        public void HistorySearch_TwoMatches()
        {
            Create("");
            _vimData.SearchHistory = (new[] { "dog", "cat", "dip" }).ToHistoryList();
            _search.DoSearch("d", enter: false).Run(VimKey.Up).Run(VimKey.Up);
            Assert.AreEqual("dip", _search.CurrentSearch.Value.Pattern);
        }

        /// <summary>
        /// When properly implemented this will allow command line editing.  Today though 
        /// we just beep to indicate we don't support it
        /// </summary>
        [Test]
        public void HistorySearch_LeftArrow()
        {
            Create("");
            _vimHost.Setup(x => x.Beep()).Verifiable();
            _search.DoSearch("d", enter: false).Run(VimKey.Left);
            _vimHost.Verify();
        }

        /// <summary>
        /// When properly implemented this will allow command line editing.  Today though 
        /// we just beep to indicate we don't support it
        /// </summary>
        [Test]
        public void HistorySearch_RightArrow()
        {
            Create("");
            _vimHost.Setup(x => x.Beep()).Verifiable();
            _search.DoSearch("d", enter: false).Run(VimKey.Right);
            _vimHost.Verify();
        }
    }
}