using System;
using System.Threading;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Modes.Visual;
using Xunit;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class SelectionTrackerTest : VimTestBase
    {
        private ITextView _textView;
        private IVimGlobalSettings _globalSettings;
        private IVimBufferData _vimBufferData;
        private ICommonOperations _commonOperations;
        private SelectionTracker _tracker;
        private Mock<IIncrementalSearch> _incrementalSearch;

        private void Create(VisualKind kind, params string[] lines)
        {
            Create(kind, 0, lines);
        }

        private void Create(VisualKind kind, int caretPosition, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textView.MoveCaretTo(caretPosition);
            _globalSettings = new GlobalSettings();
            var localSettings = new LocalSettings(_globalSettings);
            var vimTextBuffer = MockObjectFactory.CreateVimTextBuffer(_textView.TextBuffer, localSettings);
            vimTextBuffer.SetupGet(x => x.UseVirtualSpace).Returns(false);
            _vimBufferData = MockObjectFactory.CreateVimBufferData(vimTextBuffer.Object, _textView);
            _incrementalSearch = new Mock<IIncrementalSearch>(MockBehavior.Loose);
            _commonOperations = CommonOperationsFactory.GetCommonOperations(_vimBufferData);
            _tracker = new SelectionTracker(_vimBufferData, _commonOperations, _incrementalSearch.Object, kind);
            _tracker.Start();
        }

        [WpfFact]
        public void AnchorPoint1()
        {
            Create(VisualKind.Character, "foo");
            _textView.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.Equal(0, _tracker.AnchorPoint.Position);
            Assert.Same(_textView.TextSnapshot, _tracker.AnchorPoint.Position.Snapshot);
        }

        /// <summary>
        /// Tracking shouldn't happen if we're stopped
        /// </summary>
        [WpfFact]
        public void AnchorPoint2()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Stop();
            _textView.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.True(_tracker._anchorPoint.IsNone());
        }

        [WpfFact]
        public void Start1()
        {
            Create(VisualKind.Character, "foo");
            Assert.Throws<InvalidOperationException>(() => _tracker.Start());
        }

        [WpfFact]
        public void Start2()
        {
            Create(VisualKind.Character, "foo");
            Assert.Equal(new SnapshotPoint(_textView.TextSnapshot, 0), _tracker.AnchorPoint.Position);
        }

        /// <summary>
        /// Don't reset the selection if there already is one.  Breaks actions like CTRL+A")]
        /// </summary>
        [WpfFact]
        public void Start_DontResetSelection()
        {
            Create(VisualKind.Character, "");
            var realView = CreateTextView("foo bar baz");
            var selection = new Mock<ITextSelection>(MockBehavior.Strict);
            var snapshot = new Mock<ITextSnapshot>(MockBehavior.Strict);
            snapshot.SetupGet(x => x.Length).Returns(1);
            selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            var nonEmptySpan = new VirtualSnapshotSpan(new SnapshotSpan(snapshot.Object, new Span(0, 1)));
            selection.SetupGet(x => x.StreamSelectionSpan).Returns(nonEmptySpan).Verifiable();
            selection.SetupGet(x => x.IsReversed).Returns(false).Verifiable();
            selection.SetupGet(x => x.AnchorPoint).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 0));
            selection.SetupGet(x => x.ActivePoint).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 1));
            selection.SetupGet(x => x.End).Returns(new VirtualSnapshotPoint(realView.TextSnapshot, 1));
            selection.SetupProperty(x => x.Mode);
            var view = new Mock<ITextView>(MockBehavior.Strict);
            view.SetupGet(x => x.TextBuffer).Returns(realView.TextBuffer);
            view.SetupGet(x => x.Caret).Returns(realView.Caret);
            view.SetupGet(x => x.TextSnapshot).Returns(realView.TextSnapshot);
            view.SetupGet(x => x.Selection).Returns(selection.Object);
            var vimTextBuffer = new Mock<IVimTextBuffer>(MockBehavior.Strict);
            vimTextBuffer.SetupGet(x => x.LocalSettings).Returns(new LocalSettings(_globalSettings));
            vimTextBuffer.SetupGet(x => x.UseVirtualSpace).Returns(false);
            vimTextBuffer.SetupSet(x => x.LastVisualSelection = It.IsAny<Microsoft.FSharp.Core.FSharpOption<VisualSelection>>());
            var vimBufferData = MockObjectFactory.CreateVimBufferData(vimTextBuffer.Object, view.Object);
            var tracker = new SelectionTracker(vimBufferData, _commonOperations, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            selection.Verify();
        }

        /// <summary>
        /// In a selection it should take the anchor point of the selection
        /// </summary>
        [WpfFact]
        public void Start4()
        {
            Create(VisualKind.Character);
            var view = CreateTextView("foo bar baz");
            view.Selection.Select(new SnapshotSpan(view.TextSnapshot, 1, 3), false);
            var vimTextBuffer = new Mock<IVimTextBuffer>(MockBehavior.Strict);
            vimTextBuffer.SetupGet(x => x.Vim).Returns(Vim);
            vimTextBuffer.SetupGet(x => x.UndoRedoOperations).Returns(_vimBufferData.VimTextBuffer.UndoRedoOperations);
            vimTextBuffer.SetupGet(x => x.WordNavigator).Returns(_vimBufferData.VimTextBuffer.WordNavigator);
            vimTextBuffer.SetupGet(x => x.WordUtil).Returns(_vimBufferData.VimTextBuffer.WordUtil);
            vimTextBuffer.SetupGet(x => x.LocalSettings).Returns(_vimBufferData.VimTextBuffer.LocalSettings);
            vimTextBuffer.SetupGet(x => x.UseVirtualSpace).Returns(false);
            vimTextBuffer.SetupSet(x => x.LastVisualSelection = It.IsAny<Microsoft.FSharp.Core.FSharpOption<VisualSelection>>());
            var vimBufferData = MockObjectFactory.CreateVimBufferData(vimTextBuffer.Object, view);
            var commonOperations = CommonOperationsFactory.GetCommonOperations(vimBufferData);
            var tracker = new SelectionTracker(vimBufferData, commonOperations, _incrementalSearch.Object, VisualKind.Character);
            tracker.Start();
            Assert.Equal(view.Selection.AnchorPoint.Position.Position, tracker.AnchorPoint.Position);
        }

        /// <summary>
        /// Start in line mode should select the entire line
        /// </summary>
        [WpfFact]
        public void Start_LineShouldSelectWholeLine()
        {
            Create(VisualKind.Line, "foo", "bar");
            DoEvents();
            Assert.Equal(_textView.TextBuffer.GetLineFromLineNumber(0).Start, _textView.Selection.Start.Position);
            Assert.Equal(_textView.TextBuffer.GetLineFromLineNumber(0).EndIncludingLineBreak, _textView.Selection.End.Position);
        }

        [WpfFact]
        public void Stop1()
        {
            Create(VisualKind.Character, "foo");
            _tracker.Stop();
            Assert.Throws<InvalidOperationException>(() => _tracker.Stop());
        }

        [WpfFact]
        public void HasAggregateFocus1()
        {
            var caret = new Mock<ITextCaret>();
            var view = new Mock<ITextView>();
        }

        /// <summary>
        /// Test an inclusive forward selecion
        /// </summary>
        [WpfFact]
        public void UpdateSelection1()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _textView.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.Equal(_textView.TextBuffer.GetSpan(0, 2), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        /// <summary>
        /// Test an exclusive forward selecion
        /// </summary>
        [WpfFact]
        public void UpdateSelection2()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _globalSettings.Selection = "exclusive";
            _textView.MoveCaretTo(1);
            _tracker.UpdateSelection();
            Assert.Equal(_textView.TextBuffer.GetSpan(0, 1), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        /// <summary>
        /// Test an inclusive forward selecion
        /// </summary>
        [WpfFact]
        public void UpdateSelection3()
        {
            Create(VisualKind.Character, 0, "dog", "chicken");
            _textView.MoveCaretTo(2);
            _tracker.UpdateSelection();
            Assert.Equal(_textView.TextBuffer.GetSpan(0, 3), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        /// <summary>
        /// Test an inclusive backwards selecion
        /// </summary>
        [WpfFact]
        public void UpdateSelection4()
        {
            Create(VisualKind.Character, 3, "dogs", "chicken");
            _textView.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.Equal(_textView.TextBuffer.GetSpan(0, 4), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }

        /// <summary>
        /// Past the end of the line
        /// </summary>
        [WpfFact]
        public void UpdateSelection5()
        {
            Create(VisualKind.Character, 5, "dogs", "chicken");
            _textView.MoveCaretTo(0);
            _tracker.UpdateSelection();
            Assert.Equal(_textView.TextBuffer.GetSpan(0, 4), _textView.Selection.StreamSelectionSpan.SnapshotSpan);
        }
    }
}
