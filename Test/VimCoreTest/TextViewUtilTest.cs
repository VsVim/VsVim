using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class TextViewUtilTest : VimTestBase
    {
        [WpfFact]
        public void MoveCaretToVirtualPoint()
        {
            var buffer = CreateTextBuffer("foo", "bar");
            var factory = new MockRepository(MockBehavior.Strict);
            var point = new VirtualSnapshotPoint(buffer.GetLine(0), 2);

            var caret = MockObjectFactory.CreateCaret(factory: factory);
            var caretPosition = new CaretPosition(
                point,
                factory.Create<IMappingPoint>().Object,
                PositionAffinity.Predecessor);
            caret.Setup(x => x.EnsureVisible()).Verifiable();
            caret.SetupGet(x => x.Position).Returns(caretPosition).Verifiable();

            // Verify not called: By not creating a setup, it will assert if called.
            //caret.Setup(x => x.MoveTo(point)).Returns(caretPosition).Verifiable();

            var selection = MockObjectFactory.CreateSelection(factory: factory);

            // Verify not called: By not creating a setup, it will assert if called.
            //selection.Setup(x => x.Clear()).Verifiable();

            selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();

            var textView = MockObjectFactory.CreateTextView(
                textBuffer: buffer,
                selection: selection.Object,
                caret: caret.Object,
                factory: factory);

            // Make sure we do all the caution checks necessary to ensure the
            // text view line is valid.
            textView.SetupGet(x => x.IsClosed).Returns(false).Verifiable();
            textView.SetupGet(x => x.InLayout).Returns(false).Verifiable();
            var lines = factory.Create<ITextViewLineCollection>();
            textView.Setup(x => x.TextViewLines).Returns(lines.Object).Verifiable();
            var line = factory.Create<ITextViewLine>();
            lines.SetupGet(x => x.IsValid).Returns(true).Verifiable();
            lines.Setup(x => x.GetTextViewLineContainingBufferPosition(It.IsAny<SnapshotPoint>())).Returns(line.Object).Verifiable();
            line.SetupGet(x => x.IsValid).Returns(true).Verifiable();
            line.SetupGet(x => x.VisibilityState).Returns(VisibilityState.FullyVisible).Verifiable();
            var span = new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length);
            lines.SetupGet(x => x.FormattedSpan).Returns(span).Verifiable();

            TextViewUtil.MoveCaretToVirtualPoint(textView.Object, point);
            factory.Verify();
        }

        [WpfFact]
        public void GetVisibleSnapshotLines1()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            Assert.Equal(new int[] { 0, 1, 2 }, lines.Select(x => x.LineNumber));
        }

        [WpfFact]
        public void GetVisibleSnapshotLines2()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            Assert.Equal(new int[] { 1, 2 }, lines.Select(x => x.LineNumber));
        }

        /// <summary>
        /// During a layout just return an empty sequence
        /// </summary>
        [WpfFact]
        public void GetVisibleSnapshotLines3()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var view = tuple.Item1;
            view.SetupGet(x => x.InLayout).Returns(true);
            var lines = TextViewUtil.GetVisibleSnapshotLines(view.Object).ToList();
            Assert.Empty(lines);
        }
    }
}
