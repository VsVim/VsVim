using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for TssUtilTest
    /// </summary>
    [TestFixture]
    public class TssUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        [SetUp]
        public void Init()
        {
            Initialize(s_lines);
        }

        public void Initialize(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Test]
        public void GetLines1()
        {
            Initialize("foo", "bar");
            var point = new SnapshotPoint(_snapshot, 0);
            var agg = TssUtil.GetLines(point, SearchKind.Forward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("foobar", agg);
        }

        /// <summary>
        /// Check forward wraping
        /// </summary>
        [Test]
        public void GetLines2()
        {
            Initialize("foo", "bar", "baz");
            var point = new SnapshotPoint(_snapshot, 6);
            var agg = TssUtil.GetLines(point, SearchKind.Forward)
                .Select(x => x.GetText())
                .Aggregate((x, y) => x + y);
            Assert.AreEqual("barbaz", agg);
            var point2 = new SnapshotPoint(_snapshot, 6);
            agg = TssUtil.GetLines(point2, SearchKind.ForwardWithWrap)
                .Select(x => x.GetText())
                .Aggregate((x, y) => x + y);
            Assert.AreEqual("barbazfoo", agg);
        }

        [Test]
        public void GetLines3()
        {
            Initialize("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetLines(line.Start.Subtract(1), SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test]
        public void GetLines4()
        {
            Initialize("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.Backward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cba", msg);
        }

        [Test]
        public void GetLines5()
        {
            Initialize("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.Forward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cde", msg);
        }

        [Test]
        public void GetLines6()
        {
            Initialize("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.BackwardWithWrap).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cbaed", msg);
        }

        [Test]
        public void GetLines7()
        {
            Initialize("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.ForwardWithWrap).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cdeab", msg);
        }

     

        [Test]
        public void GetSpans1()
        {
            Initialize("foo", "bar");
            var point = new SnapshotPoint(_snapshot, 1);
            var list = TssUtil.GetSpans(point, SearchKind.ForwardWithWrap).Select(x => x.GetText()).ToList();
            Assert.AreEqual("oo", list[0]);
            Assert.AreEqual("bar", list[1]);
            Assert.AreEqual("f", list[2]);
        }

        [Test]
        public void GetSpans2()
        {
            Initialize("foo", "bar");
            var point = new SnapshotPoint(_snapshot, 1);
            var list = TssUtil.GetSpans(point, SearchKind.BackwardWithWrap).Select(x => x.GetText()).ToList();
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("f", list[0]);
            Assert.AreEqual("bar", list[1]);
            Assert.AreEqual("oo", list[2]);
        }

        [Test, Description("Full lines starting at line not 0")]
        public void GetSpans3()
        {
            Initialize("foo", "bar baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start, SearchKind.ForwardWithWrap).Select(x => x.GetText()).ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("bar baz", list[0]);
            Assert.AreEqual("foo", list[1]);
        }

        [Test, Description("Don't wrap if we say dont't wrap")]
        public void GetSpans4()
        {
            Initialize("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = TssUtil.GetSpans(line.End, SearchKind.Forward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Don't wrap backwards if we don't say wrap")]
        public void GetSpans5()
        {
            Initialize("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = TssUtil.GetSpans(line.Start + 2, SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Multi lack of wrap")]
        public void GetSpans6()
        {
            Initialize("foo", "bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start + 1, SearchKind.Forward);
            Assert.AreEqual(2, list.Count());
        }

        [Test, Description("multi lack of wrap reverse")]
        public void GetSpans7()
        {
            Initialize("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start, SearchKind.Backward);
            Assert.AreEqual(2, list.Count());
        }

        [Test]
        public void GetSpans8()
        {
            Initialize("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start.Subtract(1), SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Handle being given a point in the middle of a line break")]
        public void GetSpans9()
        {
            Initialize("foo", "bar");
            var point = _snapshot.GetLineFromLineNumber(0).End.Add(1);
            var list = TssUtil.GetSpans(point, SearchKind.ForwardWithWrap).Select(x => x.GetText());
            Assert.AreEqual(3, list.Count());
            Assert.AreEqual(String.Empty, list.ElementAt(0));
            Assert.AreEqual("bar", list.ElementAt(1));
            Assert.AreEqual("foo", list.ElementAt(2));
        }

        [Test]
        public void GetLineRangeSpan1()
        {
            var span = TssUtil.GetLineRangeSpan(new SnapshotPoint(_snapshot,0), 1);
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Extent, span);
        }

        /// <summary>
        /// Multi-line range
        /// </summary>
        [Test]
        public void GetLineRangeSpan2()
        {
            var span = TssUtil.GetLineRangeSpan(new SnapshotPoint(_snapshot, 0), 2);
            var start = _snapshot.GetLineFromLineNumber(0);
            var second = _snapshot.GetLineFromLineNumber(1);
            var expected = new Span(start.Start, second.End - start.Start);
            Assert.AreEqual(span.Span, expected);
        }

        [Test]
        public void GetLineRangeSpanIncludingLineBreak1()
        {
            Initialize("foo", "bar");
            var span = TssUtil.GetLineRangeSpanIncludingLineBreak(new SnapshotPoint(_snapshot, 0), 1);
            Assert.AreEqual(_snapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, span);
        }

        [Test]
        public void FindNextWordPosition1()
        {
            Initialize("foo bar");
            var p = TssUtil.FindNextWordPosition(new SnapshotPoint(_snapshot, 1), WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start of word should give bakc the current word")]
        public void FindNextWordPosition2()
        {
            Initialize("foo bar");
            var p = TssUtil.FindNextWordPosition(new SnapshotPoint(_snapshot, 0), WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start on non-first line")]
        public void FindNextWordPosition3()
        {
            Initialize("foo", "bar baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var p = TssUtil.FindNextWordPosition(line.Start, WordKind.NormalWord);
            Assert.AreNotEqual(line.Start, p);
        }

        [Test, Description("Start on non-first line with non-first word")]
        public void FindNextWordPosition4()
        {
            Initialize("foo", "bar caz dang");
            var line = _snapshot.GetLineFromLineNumber(1);
            var p = line.Start+4;
            Assert.AreEqual('c', p.GetChar());
            var p2 = TssUtil.FindNextWordPosition(line.Start + 4, WordKind.NormalWord);
            Assert.AreNotEqual(p2, p);
            Assert.AreEqual(p+4, p2);
        }

        [Test, Description("Find word accross line boundary")]
        public void FindNextWordPosition5()
        {
            Initialize("foo", "bar daz");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = TssUtil.FindNextWordPosition(line.End, WordKind.NormalWord);
            var other = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(other.Start, point);
        }

        [Test, Description("At end of buffer it should give back the last point")]
        public void FindNextWordPosition6()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = line.Start.Add(5);
            var other = TssUtil.FindNextWordPosition(point, WordKind.NormalWord);
            Assert.AreEqual(line.End, other);
        }   

        [Test, Description("Make sure we don't throw if we are in the Line break")]
        public void FindNextWordSpan1()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindNextWordSpan(line.End, WordKind.NormalWord);
            Assert.AreEqual(String.Empty, span.GetText());
            Assert.AreEqual(line.End, span.Start);
        }

        [Test]
        public void FindPreviousWordSpan1()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 1, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("in whitespace so go backwards")]
        public void FindPreviousWordSpan2()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 3, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("Don't go back a word if we're in the middle of one")]
        public void FindPreviousWordSpan3()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 5, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Make sure to go back if we're at the start of a word")]
        public void FindPreviousWordSpan4()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = line.Start + 4;
            Assert.AreEqual('b', point.GetChar());
            var span = TssUtil.FindPreviousWordSpan(point, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("Make sure to go backwards across lines")]
        public void FindPreviousWordSpan5()
        {
            Initialize("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var span = TssUtil.FindPreviousWordSpan(line.Start, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Don't crash when at the end of a line")]
        public void FindPreviousWordSpan6()
        {
            Initialize("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.End, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Back to front should return the start of the buffer")]
        public void FindPreviousWordSpan7()
        {
            Initialize("    foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start.Add(2), WordKind.NormalWord);
            Assert.AreEqual(0, span.Length);
            Assert.AreEqual(line.Start, span.Start);
        }

        [Test]
        public void FindIndentPosition()
        {
            Initialize("  foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(2, TssUtil.FindIndentPosition(line));
        }

        [Test]
        public void FindIndentPosition2()
        {
            Initialize("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(0, TssUtil.FindIndentPosition(line));
        }

        [Test]
        public void GetCharacterSpan1()
        {
            Initialize("foo");
            var span = TssUtil.GetCharacterSpan(new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.AreEqual(0, span.Start.Position);
            Assert.AreEqual(1, span.Length);
        }

        [Test, Description("Empty line shtould have a character span of the entire line")]
        public void GetCharacterSpan2()
        {
            Initialize("foo", String.Empty, "baz");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(1);
            var span = TssUtil.GetCharacterSpan(line.Start);
            Assert.AreEqual(span, line.ExtentIncludingLineBreak);
        }

        [Test, Description("End of line should have the span of the line break")]
        public void GetCharacterSpan3()
        {
            Initialize("foo", "bar");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetCharacterSpan(line.End);
            Assert.AreEqual(span, new SnapshotSpan(line.End, line.EndIncludingLineBreak));
        }

        [Test]
        public void GetReverseCharacterSpan1()
        {
            Initialize("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(1), 1);
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan2()
        {
            Initialize("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 2);
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan3()
        {
            Initialize("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 200);
            Assert.AreEqual("fo", span.GetText());
        }        



        [Test, Description("End of line should not have a current word")]
        public void FindCurrentWordSpan1()
        {
            Initialize("foo bar");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var opt = TssUtil.FindCurrentWordSpan(line.End, WordKind.NormalWord);
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void GetStartPoint()
        {
            Initialize("foo bar");
            var start = TssUtil.GetStartPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.Start, start);
        }

        [Test]
        public void GetEndPoint()
        {
            Initialize("foo bar");
            var end = TssUtil.GetEndPoint(_buffer.CurrentSnapshot);
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.End, end);
        }

        [Test]
        public void GetNextPointWithWrap1()
        {
            Initialize("foo", "baz");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = TssUtil.GetNextPointWithWrap(line.Start);
            Assert.AreEqual(1, next.Position);
        }

        [Test, Description("End of line should wrap")]
        public void GetNextPointWithWrap2()
        {
            Initialize("foo", "bar");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = TssUtil.GetNextPointWithWrap(line.End);
            line = _buffer.CurrentSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, next);
        }

        [Test, Description("Wrap around the buffer")]
        public void GetNextPointWithWrap3()
        {
            Initialize("foo", "bar");
            var next = TssUtil.GetNextPointWithWrap(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).End);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, next);
        }
        [Test]
        public void GetNextPoint1()
        {
            Initialize("foo", "baz");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = TssUtil.GetNextPoint(line.Start);
            Assert.AreEqual(1, next.Position);
        }

        [Test, Description("End of line should wrap")]
        public void GetNextPoint2()
        {
            Initialize("foo", "bar");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var next = TssUtil.GetNextPoint(line.End);
            line = _buffer.CurrentSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, next);
        }

        [Test, Description("Don't around the buffer")]
        public void GetNextPoint3()
        {
            Initialize("foo", "bar");
            var point = _buffer.CurrentSnapshot.GetLineFromLineNumber(1).End;
            var next = TssUtil.GetNextPoint(point);
            Assert.AreEqual(next, point);
        }

        [Test]
        public void GetPreviousPointWithWrap1()
        {
            Initialize("foo", "bar");
            var prev = TssUtil.GetPreviousPointWithWrap(new SnapshotPoint(_buffer.CurrentSnapshot, 1));
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, prev);
        }

        [Test]
        public void GetPreviousPointWithWrap2()
        {
            Initialize("foo", "bar");
            var prev = TssUtil.GetPreviousPointWithWrap(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, prev);
        }

        [Test]
        public void GetPreviousPointWithWrap3()
        {
            Initialize("foo", "bar");
            var prev = TssUtil.GetPreviousPointWithWrap(new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.AreEqual(TssUtil.GetEndPoint(_buffer.CurrentSnapshot), prev);
        }

        [Test]
        public void GetPoints1()
        {
            Initialize("foo");
            var points = TssUtil.GetPoints(_buffer.CurrentSnapshot.GetLineFromLineNumber(0));
            var text = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.AreEqual("foo",text);
        }
      
    }
}
