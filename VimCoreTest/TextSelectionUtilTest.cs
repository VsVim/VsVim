using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class TextSelectionUtilTest
    {
        private Mock<ITextSelection> _selection;

        [SetUp]
        public void SetUp()
        {
            _selection = new Mock<ITextSelection>(MockBehavior.Strict);
            _selection.SetupGet(x => x.IsEmpty).Returns(false);
        }
        
        [Test]
        [Description("No selection should equal no span")]
        public void GetOverarchingSpan1()
        {
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            Assert.IsTrue(TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).IsNone());
            _selection.Verify();
        }

        [Test]
        [Description("No spans should have no overarching")]
        public void GetOverarchingSpan2()
        {
            var spans = new NormalizedSnapshotSpanCollection();
            _selection.SetupGet(x => x.SelectedSpans).Returns(spans).Verifiable();
            Assert.IsTrue(TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).IsNone());
            _selection.Verify();
        }

        [Test]
        public void GetOverarchingSpan3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var span = buffer.GetLineSpan(0);
            var col = new NormalizedSnapshotSpanCollection(span);
            _selection.SetupGet(x => x.SelectedSpans).Returns(col).Verifiable();
            Assert.AreEqual(span, TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).Value);
            _selection.Verify();
        }

        [Test]
        public void GetOverarchingSpan4()
        {
            var buffer = EditorUtil.CreateBuffer("foo","baz","bar");
            var span1 = buffer.GetLineSpan(0);
            var span2 = buffer.GetLineSpan(0, 1);
            var col = new NormalizedSnapshotSpanCollection(new SnapshotSpan[]{span1, span2});
            _selection.SetupGet(x => x.SelectedSpans).Returns(col).Verifiable();
            Assert.AreEqual(buffer.GetLineSpan(0,1), TextSelectionUtil.GetOverarchingSelectedSpan(_selection.Object).Value);
            _selection.Verify();
        }

    }
}
