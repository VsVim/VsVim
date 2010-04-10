using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Vim.Modes.Normal;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class IncrementalSearchTest
    {
        private MockFactory _factory;
        private Mock<ISearchService> _searchService;
        private Mock<ITextStructureNavigator> _nav;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _searchService = _factory.Create<ISearchService>();
            _searchService
                .Setup(x => x.CreateSearchData(It.IsAny<string>(), It.IsAny<SearchKind>()))
                .Returns<string,SearchKind>((pattern,kind) => new SearchData(pattern,kind, SearchOptions.None));
            _nav = _factory.Create<ITextStructureNavigator>();
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _searchRaw = new IncrementalSearch(
                _textView,
                _settings.Object,
                _nav.Object,
                _searchService.Object);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            _search.Begin(SearchKind.ForwardWithWrap);
            foreach (var cur in value)
            {
                var ki = InputUtil.CharToKeyInput(cur);
                Assert.IsTrue(_search.Process(ki).IsSearchNeedMore);
            }
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey)).IsSearchComplete);
        }

        [Test]
        public void Process1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNextPattern("b", SearchKind.ForwardWithWrap, _textView.GetCaretPoint(), _nav.Object))
                .Returns(FSharpOption.Create(_textView.GetLineSpan(0)));
            Assert.IsTrue(_search.Process(InputUtil.CharToKeyInput('b')).IsSearchNeedMore);
        }

        [Test]
        public void Process2()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData("", SearchKind.ForwardWithWrap, SearchOptions.None)).Verifiable();
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey)).IsSearchComplete);
            _searchService.Verify();
        }

        [Test]
        public void Process3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey)).IsSearchCancelled);
        }

        [Test]
        public void LastSearch1()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None)).Verifiable();
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            ProcessWithEnter("foo");
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();

            _searchService.SetupSet(x => x.LastSearch = new SearchData("foo bar", SearchKind.ForwardWithWrap, SearchOptions.None)).Verifiable();
            ProcessWithEnter("foo bar");
            _factory.Verify();

            _searchService.SetupSet(x => x.LastSearch = new SearchData("bar", SearchKind.ForwardWithWrap, SearchOptions.None)).Verifiable();
            ProcessWithEnter("bar");
            _factory.Verify();
        }

        [Test]
        public void Status1()
        {
            Create("foo");
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    didRun = true;
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status2()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    Assert.AreEqual("a", tuple.Item1.Pattern);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };
            _search.Process(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status3()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData("foo", SearchKind.ForwardWithWrap, SearchOptions.None)).Verifiable();
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchCompleted += (unused, tuple) =>
                {
                    Assert.AreEqual("foo", tuple.Item1.Pattern);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.Forward, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            Assert.AreEqual("B", _search.CurrentSearch.Value.Pattern);
        }

        [Test]
        public void CurrentSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.Forward, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            _factory.Verify();
            Assert.AreEqual("B", _search.CurrentSearch.Value.Pattern);
        }

        [Test]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.CharToKeyInput('a'));
            _search.Process(InputUtil.CharToKeyInput('b'));
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
            _searchService.SetupSet(x => x.LastSearch = new SearchData("", SearchKind.Forward, SearchOptions.None));
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.HasValue());
        }

        [Test, Description("Cancelling needs to remove the CurrentSearch")]
        public void InSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.ForwardWithWrap, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.IsFalse(_search.InSearch);
        }

        [Test, Description("Backspace with blank search query cancels search")]
        public void Backspace1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            var result = _search.Process(InputUtil.VimKeyToKeyInput(VimKey.BackKey));
            Assert.IsTrue(result.IsSearchCancelled);
        }

        [Test, Description("Backspace with text doesn't crash")]
        public void Backspace2()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData("", SearchKind.Forward, SearchOptions.None));
            _searchService
                .Setup(x => x.FindNextPattern(It.IsAny<string>(), SearchKind.Forward, It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('b'));
            var result = _search.Process(InputUtil.VimKeyToKeyInput(VimKey.BackKey));
            Assert.IsTrue(result.IsSearchNeedMore);
            _searchService.Verify();
        }

    /*
        [Test]
        public void FindNextMatch1()
        {
            Create("bar for");
            var data = new SearchData("for", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsTrue(found.IsSome());
            Assert.AreEqual(4, found.Value.Start.Position);
        }

        [Test]
        public void FindNextMatch2()
        {
            Create("foo bar");
            var data = new SearchData("won't be there", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsTrue(found.IsNone());
        }

        [Test]
        public void FindNextMatch3()
        {
            Create("foo bar");
            var data = new SearchData("oo", SearchKind.Backward, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End);
            Assert.IsTrue(found.HasValue());
            Assert.AreEqual(1, found.Value.Start);
            Assert.AreEqual("oo", found.Value.GetText());
        }

        [Test, Description("Search with a bad regex should just produce a bad result")]
        public void FindNextMatch4()
        {
            Create("foo bar(");
            var data = new SearchData("(", SearchKind.Forward, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsFalse(found.HasValue());
        }

        [Test, Description("Make sure it matches the first occurance")]
        public void Search3()
        {
            CreateBuffer("foo bar bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual("bar", sel.GetText());
        }

        [Test, Description("No match should select nothing")]
        public void Search4()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/q");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(0, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("A partial match followed by a bad match should go back to start")]
        public void Search5()
        {
            CreateBuffer("foo bar baz");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _mode.Process("/bq");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(1, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Search accross lines")]
        public void Search6()
        {
            CreateBuffer("foo", "bar");
            _mode.Process("/bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual("bar", sel.GetText());
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void SearchNext1()
        {
            CreateBuffer("foo bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Don't start at current position")]
        public void SearchNext2()
        {
            CreateBuffer("bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual(0, sel.Length);
        }

        [Test, Description("Don't skip the current word just the current letter")]
        public void SearchNext3()
        {
            CreateBuffer("bbar, baz");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("n");
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Counted next")]
        public void SearchNext4()
        {
            CreateBuffer(" bar bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar"));
            _mode.Process("3n");
            Assert.AreEqual(9, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Make sure enter sets the search")]
        public void SearchNext5()
        {
            CreateBuffer("foo bar baz");
            _mode.Process("/ba");
            _mode.Process(VimKey.EnterKey);
            _mode.Process("n");
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void SearchReverse1()
        {
            CreateBuffer("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("?bar");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(4, sel.Start.Position);
            Assert.AreEqual("bar", sel.GetText());
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Change nothing on invalid searh")]
        public void SearchReverse2()
        {
            CreateBuffer("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("?invalid");
            var sel = _view.Selection.SelectedSpans.Single();
            Assert.AreEqual(line.End, sel.Start);
            Assert.AreEqual(0, sel.Length);
            Assert.AreEqual(line.End, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void SearchNextReverse1()
        {
            CreateBuffer("bar bar");
            _modeRaw.ChangeLastSearch(new IncrementalSearch("bar", SearchKind.Backward));
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _mode.Process("n");
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
        }

        [Test]
        public void SearchStatus1()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _mode.Process("/");
            Assert.AreEqual("/", host.Status);
        }

        [Test]
        public void SearchStatus2()
        {
            var host = new FakeVimHost();
            CreateBuffer(host, s_lines);
            _mode.Process("/zzz");
            Assert.AreEqual("/zzz", host.Status);
        }

        [Test]
        public void SearchBackspace1()
        {
            CreateBuffer("foo bar");
            _mode.Process("/fooh");
            Assert.AreEqual(0, _view.Selection.SelectedSpans.Single().Length);
            _mode.Process(VimKey.BackKey);
            Assert.AreEqual(3, _view.Selection.SelectedSpans.Single().Length);
            Assert.AreEqual("foo", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test]
        public void SearchBackspace2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bb");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _mode.Process(VimKey.BackKey);
            Assert.AreEqual("b", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test, Description("Completely exit from the search")]
        public void SearchBackspace3()
        {
            CreateBuffer("foo bar");
            _mode.Process("/b");
            _mode.Process(VimKey.BackKey);
            var res = _mode.Process('i');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }
*/

    }
}