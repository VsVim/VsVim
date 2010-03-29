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
            Create(s_lines);
        }

        public void Create(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Test]
        public void GetLines1()
        {
            Create("foo", "bar");
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
            Create("foo", "bar", "baz");
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
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetLines(line.Start.Subtract(1), SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test]
        public void GetLines4()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.Backward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cba", msg);
        }

        [Test]
        public void GetLines5()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.Forward).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cde", msg);
        }

        [Test]
        public void GetLines6()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.BackwardWithWrap).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cbaed", msg);
        }

        [Test]
        public void GetLines7()
        {
            Create("abcde".Select(x => x.ToString()).ToArray());
            var line = _snapshot.GetLineFromLineNumber(2);
            var msg = TssUtil.GetLines(line.Start, SearchKind.ForwardWithWrap).Select(x => x.GetText()).Aggregate((x, y) => x + y);
            Assert.AreEqual("cdeab", msg);
        }

     

        [Test]
        public void GetSpans1()
        {
            Create("foo", "bar");
            var point = new SnapshotPoint(_snapshot, 1);
            var list = TssUtil.GetSpans(point, SearchKind.ForwardWithWrap).Select(x => x.GetText()).ToList();
            Assert.AreEqual("oo", list[0]);
            Assert.AreEqual("bar", list[1]);
            Assert.AreEqual("f", list[2]);
        }

        [Test]
        public void GetSpans2()
        {
            Create("foo", "bar");
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
            Create("foo", "bar baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start, SearchKind.ForwardWithWrap).Select(x => x.GetText()).ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("bar baz", list[0]);
            Assert.AreEqual("foo", list[1]);
        }

        [Test, Description("Don't wrap if we say dont't wrap")]
        public void GetSpans4()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = TssUtil.GetSpans(line.End, SearchKind.Forward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Don't wrap backwards if we don't say wrap")]
        public void GetSpans5()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            var list = TssUtil.GetSpans(line.Start + 2, SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Multi lack of wrap")]
        public void GetSpans6()
        {
            Create("foo", "bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start + 1, SearchKind.Forward);
            Assert.AreEqual(2, list.Count());
        }

        [Test, Description("multi lack of wrap reverse")]
        public void GetSpans7()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start, SearchKind.Backward);
            Assert.AreEqual(2, list.Count());
        }

        [Test]
        public void GetSpans8()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var list = TssUtil.GetSpans(line.Start.Subtract(1), SearchKind.Backward);
            Assert.AreEqual(1, list.Count());
        }

        [Test, Description("Handle being given a point in the middle of a line break")]
        public void GetSpans9()
        {
            Create("foo", "bar");
            var point = _snapshot.GetLineFromLineNumber(0).End.Add(1);
            var list = TssUtil.GetSpans(point, SearchKind.ForwardWithWrap).Select(x => x.GetText());
            Assert.AreEqual(3, list.Count());
            Assert.AreEqual(String.Empty, list.ElementAt(0));
            Assert.AreEqual("bar", list.ElementAt(1));
            Assert.AreEqual("foo", list.ElementAt(2));
        }


        [Test]
        public void FindNextWordPosition1()
        {
            Create("foo bar");
            var p = TssUtil.FindNextWordPosition(new SnapshotPoint(_snapshot, 1), WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start of word should give bakc the current word")]
        public void FindNextWordPosition2()
        {
            Create("foo bar");
            var p = TssUtil.FindNextWordPosition(new SnapshotPoint(_snapshot, 0), WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start on non-first line")]
        public void FindNextWordPosition3()
        {
            Create("foo", "bar baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var p = TssUtil.FindNextWordPosition(line.Start, WordKind.NormalWord);
            Assert.AreNotEqual(line.Start, p);
        }

        [Test, Description("Start on non-first line with non-first word")]
        public void FindNextWordPosition4()
        {
            Create("foo", "bar caz dang");
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
            Create("foo", "bar daz");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = TssUtil.FindNextWordPosition(line.End, WordKind.NormalWord);
            var other = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(other.Start, point);
        }

        [Test, Description("At end of buffer it should give back the last point")]
        public void FindNextWordPosition6()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = line.Start.Add(5);
            var other = TssUtil.FindNextWordPosition(point, WordKind.NormalWord);
            Assert.AreEqual(line.End, other);
        }   

        [Test, Description("Make sure we don't throw if we are in the Line break")]
        public void FindNextWordSpan1()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindNextWordSpan(line.End, WordKind.NormalWord);
            Assert.AreEqual(String.Empty, span.GetText());
            Assert.AreEqual(line.End, span.Start);
        }

        [Test]
        public void FindPreviousWordSpan1()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 1, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("in whitespace so go backwards")]
        public void FindPreviousWordSpan2()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 3, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("Don't go back a word if we're in the middle of one")]
        public void FindPreviousWordSpan3()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start + 5, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Make sure to go back if we're at the start of a word")]
        public void FindPreviousWordSpan4()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = line.Start + 4;
            Assert.AreEqual('b', point.GetChar());
            var span = TssUtil.FindPreviousWordSpan(point, WordKind.NormalWord);
            Assert.AreEqual("foo", span.GetText());
        }

        [Test, Description("Make sure to go backwards across lines")]
        public void FindPreviousWordSpan5()
        {
            Create("foo bar", "baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var span = TssUtil.FindPreviousWordSpan(line.Start, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Don't crash when at the end of a line")]
        public void FindPreviousWordSpan6()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.End, WordKind.NormalWord);
            Assert.AreEqual("bar", span.GetText());
        }

        [Test, Description("Back to front should return the start of the buffer")]
        public void FindPreviousWordSpan7()
        {
            Create("    foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var span = TssUtil.FindPreviousWordSpan(line.Start.Add(2), WordKind.NormalWord);
            Assert.AreEqual(0, span.Length);
            Assert.AreEqual(line.Start, span.Start);
        }

        [Test]
        public void FindIndentPosition()
        {
            Create("  foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(2, TssUtil.FindIndentPosition(line));
        }

        [Test]
        public void FindIndentPosition2()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(0, TssUtil.FindIndentPosition(line));
        }


        [Test]
        public void GetReverseCharacterSpan1()
        {
            Create("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(1), 1);
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan2()
        {
            Create("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 2);
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan3()
        {
            Create("foo");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 200);
            Assert.AreEqual("fo", span.GetText());
        }        



        [Test, Description("End of line should not have a current word")]
        public void FindCurrentWordSpan1()
        {
            Create("foo bar");
            var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var opt = TssUtil.FindCurrentWordSpan(line.End, WordKind.NormalWord);
            Assert.IsTrue(opt.IsNone());
        }



        [Test]
        public void GetPoints1()
        {
            Create("foo");
            var points = TssUtil.GetLinePoints(_buffer.CurrentSnapshot.GetLineFromLineNumber(0));
            var text = points.Select(x => x.GetChar().ToString()).Aggregate((x, y) => x + y);
            Assert.AreEqual("foo",text);
        }

        [Test]
        public void GetLineExtent1()
        {
            Create("foo");
            var span = TssUtil.GetLineExtent(_buffer.GetLine(0));
            Assert.AreEqual("foo", span.GetText());
        }

        [Test]
        public void GetLineExtentIncludingLineBreak1()
        {
            Create("foo", "baz");
            var span = TssUtil.GetLineExtentIncludingLineBreak(_buffer.GetLine(0));
            Assert.AreEqual("foo" + Environment.NewLine, span.GetText());
        }

        [Test]
        public void GetWordSpans1()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(new SnapshotPoint(_buffer.CurrentSnapshot, 0), WordKind.NormalWord, SearchKind.Forward)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("foo", words[0]);
            Assert.AreEqual("bar", words[1]);
            Assert.AreEqual("baz", words[2]);
        }

        [Test, Description("Starting inside a word")]
        public void GetWordSpans2()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(new SnapshotPoint(_buffer.CurrentSnapshot, 1), WordKind.NormalWord, SearchKind.Forward)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("oo", words[0]);
            Assert.AreEqual("bar", words[1]);
            Assert.AreEqual("baz", words[2]);
        }

        [Test, Description("End of the buffer with wrap") ]
        public void GetWordSpans3()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, WordKind.NormalWord, SearchKind.ForwardWithWrap)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("foo", words[0]);
            Assert.AreEqual("bar", words[1]);
            Assert.AreEqual("baz", words[2]);
        }

        [Test, Description("End of the line without wrap shouldn't return anything")]
        public void GetWordSpans4()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, WordKind.NormalWord, SearchKind.Forward)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(0, words.Count);
        }

        [Test]
        public void GetWordSpans5()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End, WordKind.NormalWord, SearchKind.BackwardWithWrap)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("baz", words[0]);
            Assert.AreEqual("bar", words[1]);
            Assert.AreEqual("foo", words[2]);
        }

        [Test, Description("Backwards in the middle of a word")]
        public void GetWordSpans6()
        {
            Create("foo bar baz");
            var words = TssUtil
                .GetWordSpans(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End.Subtract(1), WordKind.NormalWord, SearchKind.Backward)
                .Select(x => x.GetText())
                .ToList();
            Assert.AreEqual(3, words.Count);
            Assert.AreEqual("ba", words[0]);
            Assert.AreEqual("bar", words[1]);
            Assert.AreEqual("foo", words[2]);
        }

        [Test]
        public void FindAnyWordSpan1()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent, WordKind.BigWord, SearchKind.Forward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("foo", span.Value.GetText());
        }

        [Test, Description("Search is restricted to the passed in span")]
        public void FindAnyWordSpan2()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(new SnapshotSpan(_buffer.CurrentSnapshot, 0,2), WordKind.BigWord, SearchKind.Forward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("fo", span.Value.GetText());
        }

        [Test]
        public void FindAnyWordSpan3()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent, WordKind.NormalWord, SearchKind.Backward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("baz", span.Value.GetText());
        }

        [Test]
        public void FindAnyWordSpan4()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(
                new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End.Subtract(1).Position),
                WordKind.NormalWord,
                SearchKind.BackwardWithWrap);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("ba", span.Value.GetText());            
        }

        [Test]
        public void FindFirstNoneWhitespaceCharacter1()
        {
            Create("foo");
            var point = TssUtil.FindFirstNonWhitespaceCharacter(_buffer.GetLine(0));
            Assert.AreEqual(_buffer.GetLine(0).Start, point);
        }

        [Test]
        public void FindFirstNoneWhitespaceCharacter2()
        {
            Create(" foo");
            var point = TssUtil.FindFirstNonWhitespaceCharacter(_buffer.GetLine(0));
            Assert.AreEqual(_buffer.GetLine(0).Start.Add(1), point);
        }

        [Test]
        public void FindFirstNoneWhitespaceCharacter3()
        {
            Create("");
            var point = TssUtil.FindFirstNonWhitespaceCharacter(_buffer.GetLine(0));
            Assert.AreEqual(_buffer.GetLine(0).Start, point);
        }

        [Test]
        public void FindFirstNoneWhitespaceCharacter4()
        {
            Create("  bar");
            var point = TssUtil.FindFirstNonWhitespaceCharacter(_buffer.GetLine(0));
            Assert.AreEqual(_buffer.GetLine(0).Start.Add(2), point);
        }


    }
}
