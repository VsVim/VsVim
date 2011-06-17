using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class TextViewUtilTest
    {
        [Test]
        public void MoveCaretToVirtualPoint1()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo","bar");
            var caret = MockObjectFactory.CreateCaret();
            var textView = MockObjectFactory.CreateTextView(textBuffer:buffer, caret:caret.Object);
            var point = new VirtualSnapshotPoint(buffer.GetLine(0), 2); 

            caret.Setup(x => x.MoveTo(point)).Returns(new CaretPosition()).Verifiable();
            caret.Setup(x => x.EnsureVisible()).Verifiable();
            TextViewUtil.MoveCaretToVirtualPoint(textView.Object, point);
            caret.Verify();
        }

        [Test]
        public void GetVisibleSnapshotLines1()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 0, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            CollectionAssert.AreEqual(new int[] { 0, 1, 2}, lines.Select(x => x.LineNumber));
        }

        [Test]
        public void GetVisibleSnapshotLines2()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var lines = TextViewUtil.GetVisibleSnapshotLines(tuple.Item1.Object).ToList();
            CollectionAssert.AreEqual(new int[] { 1, 2}, lines.Select(x => x.LineNumber));
        }

        [Test]
        [Description("During a layout just return an empty sequence")]
        public void GetVisibleSnapshotLines3()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar", "dog", "jazz");
            var tuple = MockObjectFactory.CreateTextViewWithVisibleLines(buffer, 1, 2);
            var view = tuple.Item1;
            view.SetupGet(x => x.InLayout).Returns(true);
            var lines = TextViewUtil.GetVisibleSnapshotLines(view.Object).ToList();
            Assert.AreEqual(0, lines.Count);
        }

    }
}
