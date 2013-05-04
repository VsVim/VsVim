using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class SnapshotSpanUtilTest : VimTestBase
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        public void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        /// <summary>
        /// Make sure all points valid
        /// </summary>
        [Fact]
        public void GetPoints1()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(x => SnapshotSpanUtil.GetPoints(Path.Forward, x));
            foreach (var point in points)
            {
                var notUsed = point.GetChar();
            }
        }

        /// <summary>
        /// Make sure all points valid
        /// </summary>
        [Fact]
        public void GetPoints2()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.ExtentIncludingLineBreak)
                .SelectMany(x => SnapshotSpanUtil.GetPoints(Path.Forward, x));
            foreach (var point in points)
            {
                var notUsed = point.GetChar();
            }
        }

        [Fact]
        public void GetPoints3()
        {
            Create("foo");
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(x => SnapshotSpanUtil.GetPoints(Path.Forward, x))
                .ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal('f', points[0].GetChar());
            Assert.Equal('o', points[1].GetChar());
            Assert.Equal('o', points[2].GetChar());
        }

        [Fact]
        public void GetPoints4()
        {
            Create("foo", "bar");
            var points = SnapshotSpanUtil.GetPoints(Path.Forward, _buffer.GetLine(0).Extent);
            var chars = points.Select(x => x.GetChar()).ToList();
            Assert.Equal(new char[] { 'f', 'o', 'o' }, chars);
        }

        [Fact]
        public void GetPoints5()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPoints(Path.Forward, new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.Equal(0, points.Count());
        }

        [Fact]
        public void GetPoints_Backward1()
        {
            Create("foo");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPoints(Path.Backward, span).ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal('o', points[0].GetChar());
            Assert.Equal('o', points[1].GetChar());
            Assert.Equal('f', points[2].GetChar());
        }

        [Fact]
        public void GetPoints_Backward2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(1).Extent;
            var points = SnapshotSpanUtil.GetPoints(Path.Backward, span).ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal('r', points[0].GetChar());
            Assert.Equal('a', points[1].GetChar());
            Assert.Equal('b', points[2].GetChar());
        }

        [Fact]
        public void GetPoints_Backward3()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1);
            var point = SnapshotSpanUtil.GetPoints(Path.Backward, span).Single();
            Assert.Equal('f', point.GetChar());
        }

        [Fact]
        public void GetPoints_Backward4()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2);
            var points = SnapshotSpanUtil.GetPoints(Path.Backward, span).Select(x => x.GetChar()).ToList();
            Assert.Equal(new char[] { 'o', 'f' }, points);
        }

        [Fact]
        public void GetPoints_Backward5()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPoints(Path.Backward, span).ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal('o', points[0].GetChar());
            Assert.Equal('o', points[1].GetChar());
            Assert.Equal('f', points[2].GetChar());
        }

        [Fact]
        public void GetPoints_Backward6()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPoints(Path.Backward, new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.Equal(0, points.Count());
        }

        [Fact]
        public void GetLastLine1()
        {
            Create("a", "b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetLastLine(span);
            Assert.Equal(0, endLine.LineNumber);
        }

        [Fact]
        public void GetLastLine2()
        {
            Create("a", "b", "c");
            var span = _buffer.GetLine(2).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetLastLine(span);
            Assert.Equal(2, endLine.LineNumber);
        }

        [Fact]
        public void GetLastLine3()
        {
            Create("", "b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetLastLine(span);
            Assert.Equal(0, endLine.LineNumber);
        }

        /// <summary>
        /// When the last line is empty it is indistinguishable from including the line
        /// above.  In the majority of cases you don't want the last line to be the empty
        /// one.  Those few places can special case the movement
        /// </summary>
        [Fact]
        public void GetLastLine4()
        {
            Create("a", "");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length);
            var endLine = SnapshotSpanUtil.GetLastLine(span);
            Assert.Equal(0, endLine.LineNumber);
        }

        [Fact]
        public void ExtendToFullLine1()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = _buffer.GetSpan(0, 1);
            span = SnapshotSpanUtil.ExtendToFullLine(span);
            Assert.Equal(_buffer.GetLineRange(0).Extent, span);
        }

        [Fact]
        public void ExtendToFullLine2()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = new SnapshotSpan(_buffer.GetLine(1).Start, 0);
            span = SnapshotSpanUtil.ExtendToFullLine(span);
            Assert.Equal(_buffer.GetLineRange(1).Extent, span);
        }

        [Fact]
        public void ExtendToFullLineIncludingLineBreak1()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = _buffer.GetSpan(0, 1);
            span = SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak(span);
            Assert.Equal(_buffer.GetLineRange(0).ExtentIncludingLineBreak, span);
        }

        [Fact]
        public void ExtendToFullLineIncludingLineBreak2()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = new SnapshotSpan(_buffer.GetLine(1).Start, 0);
            span = SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak(span);
            Assert.Equal(_buffer.GetLineRange(1).ExtentIncludingLineBreak, span);
        }

        [Fact]
        public void GetLinesAndEdges1()
        {
            Create("dog", "cat", "pig", "fox");
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak);
            Assert.True(tuple.Item1.IsNone());
            Assert.True(tuple.Item2.IsSome(x => x.Count == 2));
            Assert.True(tuple.Item2.IsSome(x => x.StartLineNumber == 0));
            Assert.True(tuple.Item3.IsNone());
        }

        [Fact]
        public void GetLinesAndEdges2()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(1).EndIncludingLineBreak);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.Equal(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.True(tuple.Item2.IsSome(x => x.Count == 1));
            Assert.True(tuple.Item3.IsNone());
        }

        [Fact]
        public void GetLinesAndEdges3()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(1).End);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.Equal(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.Equal(new SnapshotSpan(_buffer.GetLine(1).Start, span.End), tuple.Item3.Value);
            Assert.True(tuple.Item2.IsNone());
        }

        [Fact]
        public void GetLinesAndEdges4()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(2).End);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.Equal(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.Equal(new SnapshotSpan(_buffer.GetLine(2).Start, span.End), tuple.Item3.Value);
            Assert.True(tuple.Item2.IsSome(x => x.Count == 1));
        }

        [Fact]
        public void GetLinesAndEdges5()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetPoint(2));
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.Equal(new SnapshotSpan(span.Start, span.End), tuple.Item1.Value);
            Assert.True(tuple.Item3.IsNone());
            Assert.True(tuple.Item2.IsNone());
        }

        /// <summary>
        /// Last point before end of buffer
        /// </summary>
        [Fact]
        public void GetLinesAndEdges6()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetEndPoint().Subtract(1),
                0);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.Equal(span, tuple.Item1.Value);
            Assert.True(tuple.Item2.IsNone());
            Assert.True(tuple.Item3.IsNone());
        }

        /// <summary>
        /// Make sure we continue the tradition of not returning the Snapshot end point
        /// for requests even if it was the input
        /// </summary>
        [Fact]
        public void GetLinesAndEdges7()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetEndPoint(),
                0);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.True(tuple.Item1.IsNone());
            Assert.True(tuple.Item2.IsNone());
            Assert.True(tuple.Item3.IsNone());
        }

        /// <summary>
        /// Regression test for issue 311
        /// </summary>
        [Fact]
        public void GetLinesAndEdges8()
        {
            Create("long first line", "ab", "c");
            var span = new SnapshotSpan(
                _buffer.GetLine(1).Start.Add(1),
                _buffer.GetEndPoint());
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.True(tuple.Item1.IsSome());
            Assert.Equal(new SnapshotSpan(span.Start, _buffer.GetLine(1).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.True(tuple.Item2.IsNone());
            Assert.Equal(_buffer.GetLine(2).ExtentIncludingLineBreak, tuple.Item3.Value);
        }

        /// <summary>
        /// Simple exhaustive test to make sure the function works for every point in a buffer
        /// </summary>
        [Fact]
        public void GetLinesAndEdges9()
        {
            Create(s_lines);
            var snapshot = _buffer.CurrentSnapshot;
            for (var i = 0; i < snapshot.Length; i++)
            {
                var span = new SnapshotSpan(snapshot, i, snapshot.Length - i);
                SnapshotSpanUtil.GetLinesAndEdges(span);
                span = new SnapshotSpan(snapshot, 0, i);
                SnapshotSpanUtil.GetLinesAndEdges(span);
            }
        }

    }
}
