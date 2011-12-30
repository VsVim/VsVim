using System;
using System.Threading;
using EditorUtils.UnitTest;
using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.Modes.Visual;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class SelectionTrackerTest : VimTestBase
    {
        private ITextView _textView;
        private IVimGlobalSettings _globalSettings;
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
            _textView = CreateTextView(lines);
            _textView.MoveCaretTo(caretPosition);
            _globalSettings = new GlobalSettings();
            _incrementalSearch = new Mock<IIncrementalSearch>(MockBehavior.Loose);
            _tracker = new SelectionTracker(_textView, _globalSettings, _incrementalSearch.Object, kind);
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
            _textView = null;
            _tracker = null;
            SynchronizationContext.SetSynchronizationContext(_before);
        }


        [Test]
        public void AnchorPoint1()
        {
            Create(VisualKind.Character, "foo");
            _textView.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreEqual(0, _tracker.AnchorPoint.Position);
            Assert.AreSame(_textView.TextSnapshot, _tracker.AnchorPoint.Snapshot);
        }

        /// <summary>
        /// Tracking shouldn't happen if we're stopped
        /// </summary>
        [Test]
        public void AnchorPoint2()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Stop();
            _textView.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.IsTrue(_tracker._anchorPoint.IsNone());
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
            Assert.AreEqual(new SnapshotPoint(_textView.TextSnapshot, 0), _tracker.AnchorPoint.Position);
        }

        /// <summary>
        /// Don't reset the selection if there already is one.  Breaks actions like CTRL+A")]
        /// </summary>
        [Test]
        public void Start_DontResetSelection()
        {
            var realView = CreateTextView("foo bar baz");
            var selection = new Mock<ITextSelection>(MockBehavior.Strict);
            selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            selection.SetupGet(x => x.AnchorPoint).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 0));
            selection.SetupProperty(x => x.Mode);
            var view = new Mock<ITextView>(MockBehavior.Strict);
            view.SetupGet(x => x.TextBuffer).Returns(realView.TextBuffer);
            view.SetupGet(x => x.Caret).Returns(realView.Caret);
            view.SetupGet(x => x.TextSnapshot).Returns(realView.TextSnapshot);
            view.SetupGet(x => x.Selection).Returns(selection.Object);
            var tracker = new SelectionTracker(view.Object, _globalSettings, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            selection.Verify();
        }

        [Test, Description("In a selection it should take the anchor point of the selection")]
        public void Start4()
        {
            var view = CreateTextView("foo bar baz");
            view.Selection.Select(new SnapshotSpan(view.TextSnapshot, 1, 3), false);
            var tracker = new SelectionTracker(view, _globalSettings, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            Assert.AreEqual(view.Selection.AnchorPoint.Position.Position, tracker.AnchorPoint.Position);
        }

        /// <summary>
        /// Start in line mode should select the entire line
        /// </summary>
        [Test]
        public void Start_LineShouldSelectWholeLine()
        {
            Create(VisualKind.Line, "foo", "bar");
            _context.RunAll();
            Assert.AreEqual(_textView.TextBuffer.GetLineFromLineNumber(0).Start, _textView.Selection.Start.Position);
            Assert.AreEqual(_textView.TextBuffer.GetLineFromLineNumber(0).EndIncludingLineBreak, _textView.Selection.End.Position);
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
            _textView.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.AreEqual(_textView.TextBuffer.GetSpan(0, 2), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an exclusive forward selecion")]
        public void UpdateSelection2()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.AreEqual(_textView.TextBuffer.GetSpan(0, 1), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an inclusive forward selecion")]
        public void UpdateSelection3()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _textView.MoveCaretTo(2);
            _tracker.UpdateSelection();
            Assert.AreEqual(_textView.TextBuffer.GetSpan(0, 3), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Test an inclusive backwards selecion")]
        public void UpdateSelection4()
        {
            Create(VisualKind.Character, 3, "dogs", "chicken");
            _textView.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.AreEqual(_textView.TextBuffer.GetSpan(0, 4), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        [Test]
        [Description("Past the end of the line")]
        public void UpdateSelection5()
        {
            Create(VisualKind.Character, 5, "dogs", "chicken");
            _textView.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.AreEqual(_textView.TextBuffer.GetSpan(0, 4), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }
    }
}
