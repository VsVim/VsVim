using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VimCoreTest
{
    class IncrementalSearchTest
    {
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
            _mode.Process(Key.Enter);
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
            _mode.Process(Key.Back);
            Assert.AreEqual(3, _view.Selection.SelectedSpans.Single().Length);
            Assert.AreEqual("foo", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test]
        public void SearchBackspace2()
        {
            CreateBuffer("foo bar");
            _mode.Process("/bb");
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _mode.Process(Key.Back);
            Assert.AreEqual("b", _view.Selection.SelectedSpans.Single().GetText());
        }

        [Test, Description("Completely exit from the search")]
        public void SearchBackspace3()
        {
            CreateBuffer("foo bar");
            _mode.Process("/b");
            _mode.Process(Key.Back);
            var res = _mode.Process('i');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }
*/

    }
}
