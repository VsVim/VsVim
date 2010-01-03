using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Visual;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;
using System.Windows.Threading;

namespace VimCoreTest
{
    [TestFixture]
    public class SelectionTrackerTest
    {
        private ITextView _view;
        private SelectionTracker _tracker;

        private void Create(SelectionMode mode, params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _tracker = new SelectionTracker(_view, mode);
            _tracker.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _view = null;
            _tracker = null;
        }


        [Test]
        public void AnchorPoint1()
        {
            Create(SelectionMode.Character, "foo");
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreEqual(0, _tracker.AnchorPoint.Position);
            Assert.AreSame(_view.TextSnapshot, _tracker.AnchorPoint.Snapshot);
        }

        [Test, Description("Shouldn't track if it's Stopp'd")]
        public void AnchorPoint2()
        {
            Create(SelectionMode.Character, "foo");
            _tracker.Stop();
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreNotSame(_view.TextSnapshot, _tracker.AnchorPoint.Snapshot);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Start1()
        {
            Create(SelectionMode.Character, "foo");
            _tracker.Start();
        }

        [Test]
        public void Start2()
        {
            Create(SelectionMode.Character, "foo");
            Assert.AreEqual(new SnapshotPoint(_view.TextSnapshot, 0), _tracker.AnchorPoint);
        }

        [Test,ExpectedException(typeof(InvalidOperationException))]
        public void Stop1()
        {
            Create(SelectionMode.Character, "foo");
            _tracker.Stop();
            _tracker.Stop();
        }

        [Test]
        public void CaretMove1()
        {
            Create(SelectionMode.Character, "foo");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Caret.MoveTo(span.End);
            Dispatcher.CurrentDispatcher.DoEvents();
            Assert.AreEqual(1, _view.Selection.SelectedSpans.Count);
            Assert.AreEqual(span, _view.Selection.SelectedSpans[0]);
        }

        [Test]
        public void CaretMove2()
        {
            Create(SelectionMode.Character, "foo");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Caret.MoveTo(span.End);
            Dispatcher.CurrentDispatcher.DoEvents();
            _tracker.Stop();
            _tracker.Start();
            _view.Caret.MoveTo(span.Start);
            Dispatcher.CurrentDispatcher.DoEvents();
            Assert.AreEqual(1, _view.Selection.SelectedSpans.Count);
            Assert.AreEqual(span, _view.Selection.SelectedSpans[0]);
        }
    }
}
