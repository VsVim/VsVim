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

namespace VimCoreTest
{
    [TestFixture]
    public class IncrementalSearchTest
    {
        private FakeVimHost _host;
        private Mock<ISearchReplace> _searchReplace;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _host = new FakeVimHost();
            _searchReplace = new Mock<ISearchReplace>(MockBehavior.Strict);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _searchRaw = new IncrementalSearch(
                _host,
                _textView,
                _settings.Object,
                _searchReplace.Object);
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
            Assert.IsTrue(_search.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EnterKey)).IsSearchComplete);
        }

        [Test]
        public void Process1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchReplace.Setup(x =>x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            Assert.IsTrue(_search.Process(InputUtil.CharToKeyInput('b')).IsSearchNeedMore);
        }

        [Test]
        public void Process2()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EnterKey)).IsSearchComplete);
        }

        [Test]
        public void Process3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EscapeKey)).IsSearchCanceled);
        }

        [Test]
        public void LastSearch1()
        {
            Create("foo bar");
            Assert.IsTrue(String.IsNullOrEmpty(_search.LastSearch.Pattern));
        }

        [Test]
        public void LastSearch2()
        {
            Create("foo bar");
            _searchReplace.Setup(x =>x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            ProcessWithEnter("foo");
            Assert.AreEqual("foo", _search.LastSearch.Pattern);
        }

        [Test]
        public void LastSearch3()
        {
            Create("foo bar");
            _searchReplace.Setup(x =>x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            ProcessWithEnter("foo bar");
            ProcessWithEnter("bar");
            Assert.AreEqual("bar", _search.LastSearch.Pattern);
        }

        [Test]
        public void Status1()
        {
            Create("foo");
            _searchReplace.Setup(x =>x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.AreEqual("/", _host.Status);
        }

        [Test]
        public void Status2()
        {
            Create("foo bar");
            _searchReplace.Setup(x =>x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.CharToKeyInput('a'));
            Assert.AreEqual("/a", _host.Status);
        }

        [Test]
        public void Status3()
        {
            Create("foo bar");
            _searchReplace.Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.CharToKeyInput('a'));
            _search.Process(InputUtil.CharToKeyInput('b'));
            Assert.AreEqual("/ab", _host.Status);
        }

        [Test]
        public void Status4()
        {
            Create("foo bar");
            _searchReplace.Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>())).Returns(FSharpOption<SnapshotSpan>.None);
            ProcessWithEnter("foo");
            Assert.IsTrue(String.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void Status5()
        {
            Create("foo bar");
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption.Create(new SnapshotSpan(_textView.TextSnapshot, 0, 2)));
            _search.LastSearch = new SearchData("foo", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            _search.FindNextMatch(1);
            Assert.AreEqual("/foo", _host.Status);
        }

        [Test]
        public void Status6()
        {
            Create("foo bar");
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.LastSearch = new SearchData("foo", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            _search.FindNextMatch(1);
            Assert.AreEqual(Resources.NormalMode_PatternNotFound("foo"), _host.Status);
        }

        [Test]
        public void CurrentSearchSpanChanged1()
        {
            Create("foo bar");
            var list = new List<FSharpOption<SnapshotSpan>>();
            _search.CurrentSearchSpanChanged += (_,opt) => { list.Add(opt); };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list[0].IsNone());
        }

        [Test]
        public void CurrentSearchSpanChanged2()
        {
            Create("foo bar");
            var list = new List<FSharpOption<SnapshotSpan>>();
            _search.CurrentSearchSpanChanged += (_,opt) => { list.Add(opt); };
            _search.Begin(SearchKind.ForwardWithWrap);
            list.Clear();
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Process(InputUtil.CharToKeyInput('a'));
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list[0].IsNone());
        }

        [Test]
        public void CurrentSearchSpanChanged3()
        {
            Create("foo bar");
            var list = new List<FSharpOption<SnapshotSpan>>();
            _search.CurrentSearchSpanChanged += (_,opt) => { list.Add(opt); };
            _search.Begin(SearchKind.ForwardWithWrap);
            list.Clear();
            var span = new SnapshotSpan(_textView.TextSnapshot, 0,2);
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption.Create(span));
            _search.Process(InputUtil.CharToKeyInput('a'));
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list[0].IsSome());
            Assert.AreEqual(span, list[0].Value);
        }

        [Test]
        public void CurrentSearchSpanChanged4()
        {
            Create("foo bar");
            var list = new List<FSharpOption<SnapshotSpan>>();
            _search.CurrentSearchSpanChanged += (_,opt) => { list.Add(opt); };
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Process(InputUtil.CharToKeyInput('a'));
            list.Clear();
            _search.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EnterKey));
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list[0].IsNone());
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _searchReplace
                .Setup(x => x.FindNextMatch(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            Assert.AreEqual("B", _search.CurrentSearch.Value.Pattern);
        }

        [Test]
        public void CurrentSearch2()
        {
            Create("foo bar");
            var data = new SearchData("B", SearchKind.Forward, SearchReplaceFlags.IgnoreCase);
            _searchReplace
                .Setup(x => x.FindNextMatch(data, It.IsAny<SnapshotPoint>()))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            _searchReplace.Verify();
            Assert.AreEqual("B", _search.CurrentSearch.Value.Pattern);
        }



    /*
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
            _mode.Process(WellKnownKey.EnterKey);
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
            _mode.Process(WellKnownKey.BackKey);
            Assert.AreEqual(3, _view.Selection.SelectedSpans.Single().Length);
            Assert.AreEqual("foo", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test]
        public void SearchBackspace2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bb");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _mode.Process(WellKnownKey.BackKey);
            Assert.AreEqual("b", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test, Description("Completely exit from the search")]
        public void SearchBackspace3()
        {
            CreateBuffer("foo bar");
            _mode.Process("/b");
            _mode.Process(WellKnownKey.BackKey);
            var res = _mode.Process('i');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }
*/

    }
}
