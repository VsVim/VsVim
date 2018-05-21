using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Moq;
using System.Collections.Generic;
using Vim;

namespace Vim.UnitTest
{
    internal sealed class MockFactory
    {
        MockRepository _factory;

        internal MockFactory(MockRepository factory = null)
        {
            _factory = factory ?? new MockRepository(MockBehavior.Strict);
        }

        internal Mock<ITextViewLine> CreateTextViewLine(SnapshotLineRange lineRange)
        {
            var mock = _factory.Create<ITextViewLine>();
            mock.SetupGet(x => x.Start).Returns(lineRange.Start);
            mock.SetupGet(x => x.End).Returns(lineRange.End);
            mock.SetupGet(x => x.VisibilityState).Returns(VisibilityState.FullyVisible);
            return mock;
        }

        internal Mock<ITextViewLineCollection> CreateTextViewLineCollection(SnapshotLineRange lineRange)
        {
            var mock = _factory.Create<ITextViewLineCollection>();
            mock.SetupGet(x => x.IsValid).Returns(true);
            var firstLineRange = new SnapshotLineRange(lineRange.Snapshot, lineRange.StartLineNumber, 1);
            var firstLine = CreateTextViewLine(firstLineRange);
            mock.SetupGet(x => x.FirstVisibleLine).Returns(firstLine.Object);

            var lastLineRange = new SnapshotLineRange(lineRange.Snapshot, lineRange.LastLineNumber, 1);
            var lastLine = CreateTextViewLine(lastLineRange);
            mock.SetupGet(x => x.LastVisibleLine).Returns(lastLine.Object);

            var lineList = new List<ITextViewLine>() { firstLine.Object, lastLine.Object };

            mock.SetupGet(x => x.Count).Returns(lineList.Count);
            mock.Setup(x => x.GetEnumerator()).Returns(lineList.GetEnumerator());
            mock.Setup(x => x.CopyTo(It.IsAny<ITextViewLine[]>(), It.IsAny<int>()))
                .Callback((ITextViewLine[] array, int index) => lineList.CopyTo(array, index));

            return mock;
        }

        internal Mock<ITextView> CreateTextView(ITextBuffer textBuffer)
        {
            var mock = _factory.Create<ITextView>();
            mock.SetupGet(x => x.TextBuffer).Returns(textBuffer);
            mock.SetupGet(x => x.TextSnapshot).Returns(() => textBuffer.CurrentSnapshot);
            mock.SetupGet(x => x.InLayout).Returns(true);
            return mock;
        }

        internal void SetVisibleLineRange(Mock<ITextView> textView, SnapshotLineRange lineRange)
        {
            textView.SetupGet(x => x.InLayout).Returns(false);
            textView.SetupGet(x => x.TextViewLines).Returns(CreateTextViewLineCollection(lineRange).Object);
        }
    }
}
