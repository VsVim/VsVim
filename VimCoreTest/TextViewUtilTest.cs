using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Moq;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class TextViewUtilTest : VimTestBase
    {
        [Test]
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

        [Test]
        public void GetVisibleSnapshotLines1()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            CollectionAssert.AreEqual(new int[] { 0, 1, 2}, lines.Select(x => x.LineNumber));
        }

        [Test]
        public void GetVisibleSnapshotLines2()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            CollectionAssert.AreEqual(new int[] { 1, 2}, lines.Select(x => x.LineNumber));
        }

        [Test]
        [Description("During a layout just return an empty sequence")]
        public void GetVisibleSnapshotLines3()
        {
            var buffer = CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var view = tuple.Item1;
            view.SetupGet(x => x.InLayout).Returns(true);
            var lines = TextViewUtil.GetVisibleSnapshotLines(view.Object).ToList();
            Assert.AreEqual(0, lines.Count);
        }

    }
}
