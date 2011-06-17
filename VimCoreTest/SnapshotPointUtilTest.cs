using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SnapshotPointUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _textBuffer = null;
        ITextSnapshot _snapshot = null;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _snapshot = _textBuffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _textBuffer = null;
            _snapshot = null;
        }

        [Test]
        public void GetLineRangeSpan1()
        {
            Create(s_lines);
            var span = SnapshotPointUtil.GetLineRangeSpan(new SnapshotPoint(_snapshot, 0), 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Extent, span);
        }

        /// <summary>
        /// Multi-line range
        /// </summary>
        [Test]
        public void GetLineRangeSpan2()
        {
            Create(s_lines);
            var span = SnapshotPointUtil.GetLineRangeSpan(new SnapshotPoint(_snapshot, 0), 2);
            var start = _snapshot.GetLineFromLineNumber(0);
            var second = _snapshot.GetLineFromLineNumber(1);
            var expected = new Span(start.Start, second.End - start.Start);
            Assert.AreEqual(span.Span, expected);
        }

        [Test]
        public void GetLineRangeSpanIncludingLineBreak1()
        {
            Create("foo", "bar");
            var span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak(new SnapshotPoint(_snapshot, 0), 1);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, span);
        }

        [Test]
        public void GetCharacterSpan1()
        {
            Create("foo");
            var span = SnapshotPointUtil.GetCharacterSpan(new SnapshotPoint(_textBuffer.CurrentSnapshot, 0));
            Assert.AreEqual(0, span.Start.Position);
            Assert.AreEqual(1, span.Length);
        }

        [Test, Description("Empty line shtould have a character span of the entire line")]
        public void GetCharacterSpan2()
        {
            Create("foo", String.Empty, "baz");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1);
            var span = SnapshotPointUtil.GetCharacterSpan(line.Start);
            Assert.AreEqual(span, line.ExtentIncludingLineBreak);
        }

        [Test, Description("End of line should have the span of the line break")]
        public void GetCharacterSpan3()
        {
            Create("foo", "bar");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = SnapshotPointUtil.GetCharacterSpan(line.End);
            Assert.AreEqual(span, new SnapshotSpan(line.End, line.EndIncludingLineBreak));
        }

        [Test]
        public void GetNextPointWithWrap1()
        {
            Create("foo", "baz");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = SnapshotPointUtil.GetNextPointWithWrap(line.Start);
            Assert.AreEqual(1, next.Position);
        }

        [Test, Description("End of line should wrap")]
        public void GetNextPointWithWrap2()
        {
            Create("foo", "bar");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = SnapshotPointUtil.GetNextPointWithWrap(line.End);
            line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, next);
        }

        [Test, Description("Wrap around the buffer")]
        public void GetNextPointWithWrap3()
        {
            Create("foo", "bar");
            var next = SnapshotPointUtil.GetNextPointWithWrap(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).End);
            Assert.AreEqual(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, next);
        }

        [Test]
        public void GetPreviousPointWithWrap1()
        {
            Create("foo", "bar");
            var prev = SnapshotPointUtil.GetPreviousPointWithWrap(new SnapshotPoint(_textBuffer.CurrentSnapshot, 1));
            Assert.AreEqual(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, prev);
        }

        [Test]
        public void GetPreviousPointWithWrap2()
        {
            Create("foo", "bar");
            var prev = SnapshotPointUtil.GetPreviousPointWithWrap(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
            Assert.AreEqual(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).End, prev);
        }

        [Test]
        public void GetPreviousPointWithWrap3()
        {
            Create("foo", "bar");
            var prev = SnapshotPointUtil.GetPreviousPointWithWrap(new SnapshotPoint(_textBuffer.CurrentSnapshot, 0));
            Assert.AreEqual(SnapshotUtil.GetEndPoint(_textBuffer.CurrentSnapshot), prev);
        }

        [Test]
        public void GetLines1()
        {
            Create("foo", "bar");
            var point = new SnapshotPoint(_snapshot, 0);
            var agg = SnapshotPointUtil.GetLines(point, Path.Forward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("foobar", agg);
        }

        /// <summary>
        /// Check forward wraping
        /// </summary>
        [Test]
        public void GetLines2()
        {
            Create("foo", "bar", "baz");
            var point = new SnapshotPoint(_snapshot, 6);
            var agg = SnapshotPointUtil.GetLines(point, Path.Forward)
                .Select(x => x.GetText())
                .Aggregate((x, y) => x + y);
            Assert.AreEqual("barbaz", agg);
        }

        [Test]
        public void GetLines3()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = SnapshotPointUtil.GetLines(line.Start.Subtract(1), Path.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test]
        public void GetLines4()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = SnapshotPointUtil.GetLines(line.Start, Path.Backward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cba", msg);
        }

        [Test]
        public void GetLines5()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = SnapshotPointUtil.GetLines(line.Start, Path.Forward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cde", msg);
        }

        /// <summary>
        /// If going forward and starting from the end don't return any spans
        /// </summary>
        [Test]
        public void GetSpans_FromEnd()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = SnapshotPointUtil.GetSpans(Path.Forward, line.End);
            Assert.AreEqual(0, list.Count());
        }

        [Test, Description("Don't wrap backwards if we don't say wrap")]
        public void GetSpans5()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = SnapshotPointUtil.GetSpans(Path.Backward, line.Start + 2);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Multi lack of wrap")]
        public void GetSpans6()
        {
            Create("foo", "bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = SnapshotPointUtil.GetSpans(Path.Forward, line.Start + 1);
            Assert.AreEqual(2, list.Count());
        }

        /// <summary>
        /// Don't include the provided point when getting spans backward
        /// </summary>
        [Test]
        public void GetSpans_DontIncludePointGoingBackward()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = SnapshotPointUtil.GetSpans(Path.Backward, line.Start).Select(x => x.GetText()).ToList();
            CollectionAssert.AreEqual(new[] { "foo bar" }, list);
        }

        [Test]
        public void GetSpans8()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = SnapshotPointUtil.GetSpans(Path.Backward, line.Start.Subtract(1));
            Assert.AreEqual(1, list.Count());
        }

        [Test]
        public void GetCharOrDefault1()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).Start;
            Assert.AreEqual('f', SnapshotPointUtil.GetCharOrDefault(point, 'g'));
        }

        [Test]
        public void GetCharOrDefault2()
        {
            Create("foo", "bar");
            var endPoint = new SnapshotPoint(_textBuffer.CurrentSnapshot, _textBuffer.CurrentSnapshot.Length);
            var didSee = false;
            try
            {
                var notUsed = endPoint.GetChar();
            }
            catch (ArgumentException)
            {
                didSee = true;
            }
            Assert.IsTrue(didSee);
            Assert.AreEqual('f', SnapshotPointUtil.GetCharOrDefault(endPoint, 'f'));
        }

        [Test, Description("All points should be valid")]
        public void GetPoints1()
        {
            Create("foo", "bar");
            var start = SnapshotUtil.GetStartPoint(_textBuffer.CurrentSnapshot);
            foreach (var cur in SnapshotPointUtil.GetPoints(Path.Forward, start))
            {
                var notUsed = cur.GetChar();
            }
        }

        [Test]
        public void GetPoints2()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_textBuffer.CurrentSnapshot).Add(1);
            var first = SnapshotPointUtil.GetPoints(Path.Forward, start).First();
            Assert.AreEqual('o', first.GetChar());
        }

        [Test]
        public void GetPoints3()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_textBuffer.CurrentSnapshot);
            var points = SnapshotPointUtil.GetPoints(Path.Forward, start);
            var str = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.AreEqual("foo bar", str);
        }

        [Test, Description("All points should be valid")]
        public void GetPoints4()
        {
            Create("foo", "bar");
            var start = SnapshotUtil.GetStartPoint(_textBuffer.CurrentSnapshot);
            foreach (var cur in SnapshotPointUtil.GetPoints(Path.Forward, start))
            {
                var notUsed = cur.GetChar();
            }
        }

        [Test]
        public void GetPoints5()
        {
            Create("foo bar");
            var start = SnapshotUtil.GetStartPoint(_textBuffer.CurrentSnapshot).Add(1);
            var first = SnapshotPointUtil.GetPoints(Path.Backward, start).First();
            Assert.AreEqual('o', first.GetChar());
        }

        [Test]
        public void GetPoints6()
        {
            Create("foo bar");
            var start = _textBuffer.GetEndPoint();
            var points = SnapshotPointUtil.GetPoints(Path.Backward, start);
            var str = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.AreEqual("rab oof", str);
        }

        [Test]
        public void GetPoints7()
        {
            Create("foo bar");
            var start = _textBuffer.CurrentSnapshot.GetLineRange(0).End;
            var points = SnapshotPointUtil.GetPoints(Path.Backward, start);
            var str = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.AreEqual("rab oof", str);
        }

        [Test]
        public void TryGetNextPointOnLine1()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).Start;
            var res = SnapshotPointUtil.TryGetNextPointOnLine(point, 1);
            Assert.IsTrue(res.IsSome());
            Assert.AreEqual(point.Add(1), res.Value);
        }

        [Test]
        public void TryGetNextPointOnLine2()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).End;
            var res = SnapshotPointUtil.TryGetNextPointOnLine(point, 1);
            Assert.IsFalse(res.IsSome());
        }

        [Test]
        public void TryGetNextPointOnLine3()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).Start.Add(1);
            var res = SnapshotPointUtil.TryGetNextPointOnLine(point, 1);
            Assert.IsTrue(res.IsSome());
            Assert.AreEqual(point.Add(1), res.Value);
        }

        [Test]
        public void TryGetPreviousPointOnLine1()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).End.Subtract(1);
            var res = SnapshotPointUtil.TryGetPreviousPointOnLine(point, 1);
            Assert.IsTrue(res.IsSome());
            Assert.AreEqual(point.Subtract(1), res.Value);
        }

        [Test]
        public void TryGetPreviousPointOnLine2()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).Start.Add(1);
            var res = SnapshotPointUtil.TryGetPreviousPointOnLine(point, 1);
            Assert.IsTrue(res.IsSome());
            Assert.AreEqual(_textBuffer.GetLine(0).Start, res.Value);
        }

        [Test]
        public void TryGetPreviousPointOnLine3()
        {
            Create("foo", "bar");
            var point = _textBuffer.GetLine(0).Start;
            var res = SnapshotPointUtil.TryGetPreviousPointOnLine(point, 1);
            Assert.IsFalse(res.IsSome());
        }

        [Test]
        public void GetPointsOnContainingLineFrom1()
        {
            Create("foo", "bar", "baz");
            var points = SnapshotPointUtil.GetPointsOnContainingLineFrom(_textBuffer.GetLine(0).Start).Select(x => x.GetChar());
            CollectionAssert.AreEqual("foo", points);
        }

        [Test]
        public void GetPointsOnContainingLineFrom2()
        {
            Create("foo", "bar", "baz");
            var points = SnapshotPointUtil.GetPointsOnContainingLineFrom(_textBuffer.GetLine(0).Start.Add(1)).Select(x => x.GetChar());
            CollectionAssert.AreEqual("oo", points);
        }

        [Test]
        public void GetPointsOnContainingLineBackwardsFrom1()
        {
            Create("foo", "bar", "baz");
            var points = SnapshotPointUtil.GetPointsOnContainingLineBackwardsFrom(_textBuffer.GetLine(0).End).Select(x => x.GetChar());
            CollectionAssert.AreEqual("oof", points);
        }

        [Test]
        public void GetPointsOnContainingLineBackwardsFrom2()
        {
            Create("foo", "bar", "baz");
            var points = SnapshotPointUtil.GetPointsOnContainingLineBackwardsFrom(_textBuffer.GetLine(1).End).Select(x => x.GetChar());
            CollectionAssert.AreEqual("rab", points);
        }

        [Test]
        public void GetPointsOnContainingLineBackwardsFrom3()
        {
            Create("foo", "bar", "baz");
            var points = SnapshotPointUtil.GetPointsOnContainingLineBackwardsFrom(_textBuffer.GetLine(1).End.Subtract(2)).Select(x => x.GetChar());
            CollectionAssert.AreEqual("ab", points);
        }
    }
}
