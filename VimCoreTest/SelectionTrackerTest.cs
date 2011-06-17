using System;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes.Visual;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SelectionTrackerTest
    {
        private ITextView _view;
        private IVimGlobalSettings _settings;
        private SelectionTracker _tracker;
        private Mock<IIncrementalSearch> _incrementalSearch;
        private TestableSynchronizationContext _context;
        private SynchronizationContext _before;

        private void Create(VisualKind kind, params string[] lines)
        {
            Create(kind, 0, lines);
        }

        private void Create(VisualKind kind, int caretPosition, params string[] lines)
        {
            _view = EditorUtil.CreateTextView(lines);
            _view.MoveCaretTo(caretPosition);
            _settings = new Vim.GlobalSettings();
            _incrementalSearch = new Mock<IIncrementalSearch>(MockBehavior.Loose);
            _tracker = new SelectionTracker(_view, _settings, _incrementalSearch.Object, kind);
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
            Create(VisualKind.Character, "foo");
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreEqual(0, _tracker.AnchorPoint.Position);
            Assert.AreSame(_view.TextSnapshot, _tracker.AnchorPoint.Position.Snapshot);
        }

        [Test, Description("Shouldn't track if it's Stopp'd")]
        public void AnchorPoint2()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Stop();
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreNotSame(_view.TextSnapshot, _tracker.AnchorPoint.Position.Snapshot);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Start1()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Start();
        }

        [Test]
        public void Start2()
        {
            Create(VisualKind.Character, "foo");
            Assert.AreEqual(new SnapshotPoint(_view.TextSnapshot, 0), _tracker.AnchorPoint.Position);
        }

        [Test, Description("Don't reset the selection if there already is one.  Breaks actions like CTRL+A")]
        public void Start3()
        {
            var realView = EditorUtil.CreateTextView("foo bar baz");
            var selection = new Mock<ITextSelection>(MockBehavior.Strict);
            selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream);
            selection.SetupGet(x => x.AnchorPoint).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 0));
            var view = new Mock<ITextView>(MockBehavior.Strict);
            view.SetupGet(x => x.TextBuffer).Returns(realView.TextBuffer);
            view.SetupGet(x => x.Caret).Returns(realView.Caret);
            view.SetupGet(x => x.TextSnapshot).Returns(realView.TextSnapshot);
            view.SetupGet(x => x.Selection).Returns(selection.Object);
            var tracker = new SelectionTracker(view.Object, _settings, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            selection.Verify();
        }

        [Test, Description("In a selection it should take the anchor point of the selection")]
        public void Start4()
        {
            var view = EditorUtil.CreateTextView("foo bar baz");
            view.Selection.Select(new SnapshotSpan(view.TextSnapshot, 1, 3), false);
            var tracker = new SelectionTracker(view, _settings, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            Assert.AreEqual(view.Selection.AnchorPoint.Position.Position, tracker.AnchorPoint.Position.Position);
        }

        [Test, Description("Line mode should include the entire line with linebreak")]
        public void Start5()
        {
            Create(VisualKind.Line, "foo", "bar");
            _context.RunAll();
            Assert.AreEqual(_view.TextBuffer.GetLineFromLineNumber(0).Start, _view.Selection.Start.Position);
            Assert.AreEqual(_view.TextBuffer.GetLineFromLineNumber(0).EndIncludingLineBreak, _view.Selection.End.Position);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void Stop1()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Stop();
            _tracker.Stop();
        }

        [Test]
        public void HasAggregateFocus1()
        {
            var caret = new Mock<ITextCaret>();
            var view = new Mock<ITextView>();
        }

        [Test]
        [Description("Test an inclusive forward selecion")]
        public void UpdateSelection1()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _view.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.AreEqual(_view.TextBuffer.GetSpan(0, 2), _view.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an exclusive forward selecion")]
        public void UpdateSelection2()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _settings.Selection = "exclusive";
            _view.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.AreEqual(_view.TextBuffer.GetSpan(0, 1), _view.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an inclusive forward selecion")]
        public void UpdateSelection3()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _view.MoveCaretTo(2);
            _tracker.UpdateSelection();
            Assert.AreEqual(_view.TextBuffer.GetSpan(0, 3), _view.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an inclusive backwards selecion")]
        public void UpdateSelection4()
        {
            Create(VisualKind.Character, 3, "dogs", "chicken");
            _view.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.AreEqual(_view.TextBuffer.GetSpan(0, 4), _view.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Past the end of the line")]
        public void UpdateSelection5()
        {
            Create(VisualKind.Character, 5, "dogs", "chicken");
            _view.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.AreEqual(_view.TextBuffer.GetSpan(0, 4), _view.Selection.StreamSelectionSpan.SnapshotSpan);
        }
    }
}
