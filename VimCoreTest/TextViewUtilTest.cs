using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class TextViewUtilTest : VimTestBase
    {
        [Fact]
        public void MoveCaretToVirtualPoint()
        {
            var buffer = CreateTextBuffer("foo","bar");
            var factory = new MockRepository(MockBehavior.Strict);
            var caret = MockObjectFactory.CreateCaret(factory: factory);
            caret.Setup(x => x.EnsureVisible()).Verifiable();

            var selection = MockObjectFactory.CreateSelection(factory: factory);
            selection.Setup(x => x.Clear()).Verifiable();

            var textView = MockObjectFactory.CreateTextView(
                textBuffer: buffer, 
                selection: selection.Object,
                caret: caret.Object,
                factory: factory);
            var point = new VirtualSnapshotPoint(buffer.GetLine(0), 2); 
            caret.Setup(x => x.MoveTo(point)).Returns(new CaretPosition()).Verifiable();

            TextViewUtil.MoveCaretToVirtualPoint(textView.Object, point);
            factory.Verify();
        }

        [Fact]
        public void GetVisibleSnapshotLines1()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            Assert.Equal(new int[] { 0, 1, 2}, lines.Select(x => x.LineNumber));
        }

        [Fact]
        public void GetVisibleSnapshotLines2()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            Assert.Equal(new int[] { 1, 2}, lines.Select(x => x.LineNumber));
        }

        /// <summary>
        /// During a layout just return an empty sequence
        /// </summary>
        [Fact]
        public void GetVisibleSnapshotLines3()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var view = tuple.Item1;
            view.SetupGet(x => x.InLayout).Returns(true);
            var lines = TextViewUtil.GetVisibleSnapshotLines(view.Object).ToList();
            Assert.Equal(0, lines.Count);
        }

    }
}
