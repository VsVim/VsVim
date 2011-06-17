using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class TrackingLineColumnTest
    {
        private TrackingLineColumnService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new TrackingLineColumnService();
        }

        private ITrackingLineColumn Create(ITextBuffer buffer, int line, int column)
        {
            return _service.Create(buffer, line, column, LineColumnTrackingMode.Default);
        }

        private static void AssertPoint(ITrackingLineColumn tlc, int lineNumber, int column)
        {
            var point = tlc.Point;
            Assert.IsTrue(point.IsSome());
            AssertLineColumn(point.Value, lineNumber, column);
        }


        private static void AssertLineColumn(SnapshotPoint point, int lineNumber, int column)
        {
            var line = point.GetContainingLine();
            Assert.AreEqual(lineNumber, line.LineNumber, "Invalid line number");
            Assert.AreEqual(column, point.Position - line.Start.Position, "Invalid column");
        }

        [Test]
        public void SimpleEdit1()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar", "baz");
            var tlc = Create(buffer, 0, 1);
            buffer.Replace(new Span(0, 0), "foo");
            AssertPoint(tlc, 0, 1);
        }

        [Test, Description("Replace the line, shouldn't affect the column tracking")]
        public void SimpleEdit2()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar", "baz");
            var tlc = Create(buffer, 0, 1);
            buffer.Replace(new Span(0, 5), "barbar");
            AssertPoint(tlc, 0, 1);
        }

        [Test, Description("Edit at the end of the line")]
        public void SimpleEdit3()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar", "baz");
            var tlc = Create(buffer, 0, 1);
            buffer.Replace(new Span(5, 0), "barbar");
            AssertPoint(tlc, 0, 1);
        }

        [Test, Description("Edit a different line")]
        public void SimpleEdit4()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar", "baz");
            var tlc = Create(buffer, 0, 1);
            buffer.Replace(buffer.GetLineRange(1, 1).ExtentIncludingLineBreak.Span, "hello world");
            AssertPoint(tlc, 0, 1);
        }

        /// <summary>
        /// Deleting the line containing the tracking item should cause it to be deleted
        /// </summary>
        [Test]
        public void Edit_DeleteLine()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar");
            var tlc = Create(buffer, 0, 0);
            buffer.Delete(buffer.GetLineFromLineNumber(0).ExtentIncludingLineBreak.Span);
            Assert.IsTrue(tlc.Point.IsNone());
            Assert.IsTrue(tlc.VirtualPoint.IsNone());
        }

        /// <summary>
        /// Deleting the line below shouldn't affect it
        /// </summary>
        [Test]
        public void Edit_DeleteLineBelow()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar");
            var tlc = Create(buffer, 0, 2);
            buffer.Delete(buffer.GetLineFromLineNumber(1).ExtentIncludingLineBreak.Span);
            AssertPoint(tlc, 0, 2);
        }

        /// <summary>
        /// Deleting the line above should just adjust it
        /// </summary>
        [Test]
        public void Edit_DeleteLineAbove()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo", "bar", "baz");
            var tlc = Create(buffer, 1, 2);
            buffer.Delete(buffer.GetLineFromLineNumber(0).ExtentIncludingLineBreak.Span);
            AssertPoint(tlc, 0, 2);
        }

        [Test]
        public void TruncatingEdit1()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar baz");
            var tlc = Create(buffer, 0, 5);
            buffer.Replace(buffer.GetLine(0).ExtentIncludingLineBreak, "yes");
            AssertPoint(tlc, 0, 3);
        }

        [Test, Description("Make it 0 width")]
        public void TruncatingEdit2()
        {
            var buffer = EditorUtil.CreateTextBuffer("foo bar baz");
            var tlc = Create(buffer, 0, 5);
            buffer.Replace(buffer.GetLineFromLineNumber(0).ExtentIncludingLineBreak, "");
            AssertPoint(tlc, 0, 0);
        }
    }
}
