using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Moq;

namespace EditorUtils.UnitTest
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
            mock.SetupGet(x => x.End).Returns(lineRange.EndIncludingLineBreak);
            return mock;
        }

        internal Mock<ITextViewLineCollection> CreateTextViewLineCollection(SnapshotLineRange lineRange)
        {
            var mock = _factory.Create<ITextViewLineCollection>();
            var firstLineRange = new SnapshotLineRange(lineRange.Snapshot, lineRange.StartLineNumber, 1);
            var firstLine = CreateTextViewLine(firstLineRange);
            mock.SetupGet(x => x.FirstVisibleLine).Returns(firstLine.Object);

            var lastLineRange = new SnapshotLineRange(lineRange.Snapshot, lineRange.LastLineNumber, 1);
            var lastLine = CreateTextViewLine(lastLineRange);
            mock.SetupGet(x => x.LastVisibleLine).Returns(lastLine.Object);

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
