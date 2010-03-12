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
using System.Threading;

namespace VimCoreTest
{
    [TestFixture]
    public class SelectionTrackerTest
    {
        private ITextView _view;
        private SelectionTracker _tracker;
        private TestableSynchronizationContext _context;
        private SynchronizationContext _before;

        private void Create(SelectionMode mode, params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _tracker = new SelectionTracker(_view, mode);
            _tracker.Start();
        }

        [SetUp]
        public void SetUp()
        {
            _before = SynchronizationContext.Current;
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _view = null;
            _tracker = null;
            SynchronizationContext.SetSynchronizationContext(_before);
        }


        [Test]
        public void AnchorPoint1()
        {
            Create(SelectionMode.Character, "foo");
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreEqual(0, _tracker.AnchorPoint.Position);
            Assert.AreSame(_view.TextSnapshot, _tracker.AnchorPoint.Position.Snapshot);
        }

        [Test, Description("Shouldn't track if it's Stopp'd")]
        public void AnchorPoint2()
        {
            Create(SelectionMode.Character, "foo");
            _tracker.Stop();
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreNotSame(_view.TextSnapshot, _tracker.AnchorPoint.Position.Snapshot);
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
            Assert.AreEqual(new SnapshotPoint(_view.TextSnapshot, 0), _tracker.AnchorPoint.Position);
        }

        [Test, Description("Don't reset the selection if there already is one.  Breaks actions like CTRL+A")]
        public void Start3()
        {
            var realView = EditorUtil.CreateView("foo bar baz");
            var selection = new Mock<ITextSelection>(MockBehavior.Strict);
            selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream);
            selection.SetupGet(x => x.AnchorPoint).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 0));
            var view = new Mock<ITextView>(MockBehavior.Strict);
            view.SetupGet(x => x.TextBuffer).Returns(realView.TextBuffer);
            view.SetupGet(x => x.Caret).Returns(realView.Caret);
            view.SetupGet(x => x.TextSnapshot).Returns(realView.TextSnapshot);
            view.SetupGet(x => x.Selection).Returns(selection.Object);
            var tracker = new SelectionTracker(view.Object, SelectionMode.Character);
            tracker.Start();
            selection.Verify();
        }

        [Test, Description("In a selection it should take the anchor point of the selection")]
        public void Start4()
        {
            var view = EditorUtil.CreateView("foo bar baz");
            view.Selection.Select(new SnapshotSpan(view.TextSnapshot, 1, 3), false);
            var tracker = new SelectionTracker(view, SelectionMode.Character);
            tracker.Start();
            Assert.AreEqual(view.Selection.AnchorPoint.Position.Position, tracker.AnchorPoint.Position.Position);
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
            _context.RunAll();
            Assert.AreEqual(1, _view.Selection.SelectedSpans.Count);
            Assert.AreEqual(span, _view.Selection.SelectedSpans[0]);
        }

        [Test]
        public void CaretMove2()
        {
            Create(SelectionMode.Character, "foo");
            var span = new SnapshotSpan(_view.TextSnapshot, 0, 3);
            _view.Caret.MoveTo(span.End);
            _context.RunAll();
            _tracker.Stop();
            _tracker.Start();
            _view.Caret.MoveTo(span.Start);
            _context.RunAll();
            Assert.AreEqual(1, _view.Selection.SelectedSpans.Count);
            Assert.AreEqual(span, _view.Selection.SelectedSpans[0]);
        }

        [Test]
        public void SelectedLines1()
        {
            Create(SelectionMode.Character, "foo");
            _view.Selection.Select(new SnapshotSpan(_view.TextSnapshot, 0, 2), false);
            Assert.AreEqual(_view.GetLineSpan(0, 0), _tracker.SelectedLines);
        }

        [Test]
        public void SelectedLines2()
        {
            Create(SelectionMode.Character, "foo", "bar", "baz");
            _view.Selection.Select(new SnapshotSpan(_view.TextSnapshot, 0, 7), false);
            Assert.AreEqual(_view.GetLineSpan(0, 1), _tracker.SelectedLines);
        }

    }
}
