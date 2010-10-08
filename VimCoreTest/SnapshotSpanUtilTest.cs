using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class SnapshotSpanUtilTest
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
            _buffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
            _snapshot = null;
        }

        [Test, Description("Make sure all points valid")]
        public void GetPoints1()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(SnapshotSpanUtil.GetPoints);
            foreach (var point in points)
            {
                var notUsed = point.GetChar();
            }
        }

        [Test, Description("Make sure all points valid")]
        public void GetPoints2()
        {
            Create(s_lines);
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.ExtentIncludingLineBreak)
                .SelectMany(SnapshotSpanUtil.GetPoints);
            foreach (var point in points)
            {
                var notUsed = point.GetChar();
            }
        }

        [Test]
        public void GetPoints3()
        {
            Create("foo");
            var points =
                _buffer.CurrentSnapshot.Lines
                .Select(x => x.Extent)
                .SelectMany(SnapshotSpanUtil.GetPoints)
                .ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('f', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('o', points[2].GetChar());
        }

        [Test]
        public void GetPoints4()
        {
            Create("foo", "bar");
            var points = SnapshotSpanUtil.GetPoints(_buffer.GetLine(0).Extent);
            var chars = points.Select(x => x.GetChar()).ToList();
            CollectionAssert.AreEqual(new char[] { 'f', 'o', 'o' }, chars);
        }

        [Test]
        public void GetPoints5()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPoints(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.AreEqual(0, points.Count());
        }

        [Test]
        public void GetPointsBackward1()
        {
            Create("foo");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('o', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('f', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(1).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('r', points[0].GetChar());
            Assert.AreEqual('a', points[1].GetChar());
            Assert.AreEqual('b', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward3()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1);
            var point = SnapshotSpanUtil.GetPointsBackward(span).Single();
            Assert.AreEqual('f', point.GetChar());
        }

        [Test]
        public void GetPointsBackward4()
        {
            Create("foo", "bar");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2);
            var points = SnapshotSpanUtil.GetPointsBackward(span).Select(x => x.GetChar()).ToList();
            CollectionAssert.AreEqual(new char[] { 'o', 'f' }, points);
        }

        [Test]
        public void GetPointsBackward5()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Extent;
            var points = SnapshotSpanUtil.GetPointsBackward(span).ToList();
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual('o', points[0].GetChar());
            Assert.AreEqual('o', points[1].GetChar());
            Assert.AreEqual('f', points[2].GetChar());
        }

        [Test]
        public void GetPointsBackward6()
        {
            Create("foo bar");
            var points = SnapshotSpanUtil.GetPointsBackward(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 0));
            Assert.AreEqual(0, points.Count());
        }

        [Test]
        public void GetEndLine1()
        {
            Create("a", "b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetEndLine(span);
            Assert.AreEqual(0, endLine.LineNumber);
        }

        [Test]
        public void GetEndLine2()
        {
            Create("a", "b", "c");
            var span = _buffer.GetLine(2).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetEndLine(span);
            Assert.AreEqual(2, endLine.LineNumber);
        }

        [Test]
        public void GetEndLine3()
        {
            Create("", "b", "c");
            var span = _buffer.GetLine(0).ExtentIncludingLineBreak;
            var endLine = SnapshotSpanUtil.GetEndLine(span);
            Assert.AreEqual(0, endLine.LineNumber);
        }

        [Test]
        [Description("0 length end of buffer line")]
        public void GetEndLine4()
        {
            Create("a", "");
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length);
            var endLine = SnapshotSpanUtil.GetEndLine(span);
            Assert.AreEqual(1, endLine.LineNumber);
        }

        [Test]
        public void ExtendToFullLine1()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = _buffer.GetSpan(0, 1);
            span = SnapshotSpanUtil.ExtendToFullLine(span);
            Assert.AreEqual(_buffer.GetLineSpan(0), span);
        }

        [Test]
        public void ExtendToFullLine2()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = new SnapshotSpan(_buffer.GetLine(1).Start, 0);
            span = SnapshotSpanUtil.ExtendToFullLine(span);
            Assert.AreEqual(_buffer.GetLineSpan(1), span);
        }

        [Test]
        public void ExtendToFullLineIncludingLineBreak1()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = _buffer.GetSpan(0, 1);
            span = SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak(span);
            Assert.AreEqual(_buffer.GetLineSpanIncludingLineBreak(0), span);
        }

        [Test]
        public void ExtendToFullLineIncludingLineBreak2()
        {
            Create("dog", "cat", "chicken", "pig");
            var span = new SnapshotSpan(_buffer.GetLine(1).Start, 0);
            span = SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak(span);
            Assert.AreEqual(_buffer.GetLineSpanIncludingLineBreak(1), span);
        }

        [Test]
        public void GetLinesAndEdges1()
        {
            Create("dog", "cat", "pig", "fox");
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(_buffer.GetLineSpanIncludingLineBreak(0, 1));
            Assert.IsTrue(tuple.Item1.IsNone());
            Assert.IsTrue(tuple.Item3.IsNone());
            CollectionAssert.AreEquivalent(
                new int[] { 0, 1 },
                tuple.Item2.Select(x => x.LineNumber).ToList());
        }

        [Test]
        public void GetLinesAndEdges2()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(1).EndIncludingLineBreak);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.AreEqual(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.IsTrue(tuple.Item3.IsNone());
            CollectionAssert.AreEquivalent(
                new int[] { 1 },
                tuple.Item2.Select(x => x.LineNumber).ToList());
        }

        [Test]
        public void GetLinesAndEdges3()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(1).End);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.AreEqual(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetLine(1).Start, span.End), tuple.Item3.Value);
            Assert.IsTrue(tuple.Item2.Count() == 0);
        }

        [Test]
        public void GetLinesAndEdges4()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetLine(2).End);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.AreEqual(new SnapshotSpan(span.Start, _buffer.GetLine(0).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetLine(2).Start, span.End), tuple.Item3.Value);
            CollectionAssert.AreEquivalent(
               new int[] { 1 },
               tuple.Item2.Select(x => x.LineNumber).ToList());
        }

        [Test]
        public void GetLinesAndEdges5()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.GetPoint(1),
                _buffer.GetPoint(2));
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.AreEqual(new SnapshotSpan(span.Start, span.End), tuple.Item1.Value);
            Assert.IsTrue(tuple.Item3.IsNone());
            Assert.AreEqual(0, tuple.Item2.Count());
        }

        [Test]
        [Description("Last point before end of buffer")]
        public void GetLinesAndEdges6()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetEndPoint().Subtract(1),
                0);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.AreEqual(span, tuple.Item1.Value);
            Assert.AreEqual(0, tuple.Item2.ToList().Count);
            Assert.IsTrue(tuple.Item3.IsNone());
        }

        /// <summary>
        /// Make sure we continue the tradition of not returning the Snapshot end point
        /// for requests even if it was the input
        /// </summary>
        [Test]
        [Description("Span staring at the end point")]
        public void GetLinesAndEdges7()
        {
            Create("dog", "cat", "pig", "fox");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetEndPoint(),
                0);
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.IsTrue(tuple.Item1.IsNone());
            Assert.AreEqual(0, tuple.Item2.ToList().Count);
            Assert.IsTrue(tuple.Item3.IsNone());
        }

        [Test]
        [Description("Regression test for issue 311")]
        public void GetLinesAndEdges8()
        {
            Create("long first line", "ab", "c");
            var span = new SnapshotSpan(
                _buffer.GetLine(1).Start.Add(1),
                _buffer.GetEndPoint());
            var tuple = SnapshotSpanUtil.GetLinesAndEdges(span);
            Assert.IsTrue(tuple.Item1.IsSome());
            Assert.AreEqual(new SnapshotSpan(span.Start, _buffer.GetLine(1).EndIncludingLineBreak), tuple.Item1.Value);
            Assert.AreEqual(0, tuple.Item2.Count());
            Assert.AreEqual(_buffer.GetLine(2).ExtentIncludingLineBreak, tuple.Item3.Value);
        }

        [Test]
        [Description("Simple exhaustive test to make sure the function works for every point in a buffer")]
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
