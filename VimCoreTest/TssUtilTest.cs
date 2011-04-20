using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
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

        ITextBuffer _textBuffer = null;
        ITextSnapshot _snapshot = null;

        [SetUp]
        public void Init()
        {
            Create(s_lines);
        }

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _textBuffer.CurrentSnapshot;
        }

        [Test]
        public void FindNextWordStart1()
        {
            Create("foo bar");
            var p = TssUtil.FindNextWordStart(new SnapshotPoint(_snapshot, 1), 1, WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start of word should give back the current word")]
        public void FindNextWordStart2()
        {
            Create("foo bar");
            var p = TssUtil.FindNextWordStart(new SnapshotPoint(_snapshot, 0), 1, WordKind.NormalWord);
            Assert.AreEqual(4, p.Position);
        }

        [Test, Description("Start on non-first line")]
        public void FindNextWordStart3()
        {
            Create("foo", "bar baz");
            var line = _snapshot.GetLineFromLineNumber(1);
            var p = TssUtil.FindNextWordStart(line.Start, 1, WordKind.NormalWord);
            Assert.AreNotEqual(line.Start, p);
        }

        [Test, Description("Start on non-first line with non-first word")]
        public void FindNextWordStart4()
        {
            Create("foo", "bar caz dang");
            var line = _snapshot.GetLineFromLineNumber(1);
            var p = line.Start + 4;
            Assert.AreEqual('c', p.GetChar());
            var p2 = TssUtil.FindNextWordStart(line.Start + 4, 1, WordKind.NormalWord);
            Assert.AreNotEqual(p2, p);
            Assert.AreEqual(p + 4, p2);
        }

        [Test, Description("Find word across line boundary")]
        public void FindNextWordStart5()
        {
            Create("foo", "bar daz");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = TssUtil.FindNextWordStart(line.End, 1, WordKind.NormalWord);
            var other = _snapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(other.Start, point);
        }

        [Test, Description("At end of buffer it should give back the last point")]
        public void FindNextWordStart6()
        {
            Create("foo bar");
            var line = _snapshot.GetLineFromLineNumber(0);
            var point = line.Start.Add(5);
            var other = TssUtil.FindNextWordStart(point, 1, WordKind.NormalWord);
            Assert.AreEqual(line.End, other);
        }

        [Test]
        public void FindNextWordStart7()
        {
            Create("foo bar jazz");
            var next = TssUtil.FindNextWordStart(_snapshot.GetPoint(0), 2, WordKind.NormalWord);
            Assert.AreEqual('j', next.GetChar());
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
        public void FindPreviousWordStart1()
        {
            Create("foo bar jazz dog");
            var prev = TssUtil.FindPreviousWordStart(_snapshot.GetLine(0).End, 1, WordKind.NormalWord);
            Assert.AreEqual('d', prev.GetChar());
        }

        [Test]
        public void FindPreviousWordStart2()
        {
            Create("foo bar jazz dog");
            var prev = TssUtil.FindPreviousWordStart(_snapshot.GetLine(0).End, 2, WordKind.NormalWord);
            Assert.AreEqual('j', prev.GetChar());
        }

        [Test]
        public void FindIndentPosition()
        {
            Create("  foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(2, TssUtil.FindIndentPosition(line, 1));
        }

        [Test]
        public void FindIndentPosition2()
        {
            Create("foo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(0, TssUtil.FindIndentPosition(line, 1));
        }

        [Test]
        public void FindIndentPosition3()
        {
            Create("\tfoo");
            var line = _snapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(4, TssUtil.FindIndentPosition(line, 4));
        }

        [Test]
        public void GetReverseCharacterSpan1()
        {
            Create("foo");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(1), 1);
            Assert.AreEqual("f", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan2()
        {
            Create("foo");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 2);
            Assert.AreEqual("fo", span.GetText());
        }

        [Test]
        public void GetReverseCharacterSpan3()
        {
            Create("foo");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var span = TssUtil.GetReverseCharacterSpan(line.Start.Add(2), 200);
            Assert.AreEqual("fo", span.GetText());
        }



        [Test, Description("End of line should not have a current word")]
        public void FindCurrentWordSpan1()
        {
            Create("foo bar");
            var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0);
            var opt = TssUtil.FindCurrentWordSpan(line.End, WordKind.NormalWord);
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void FindAnyWordSpan1()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent, WordKind.BigWord, Path.Forward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("foo", span.Value.GetText());
        }

        [Test, Description("Search is restricted to the passed in span")]
        public void FindAnyWordSpan2()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 2), WordKind.BigWord, Path.Forward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("fo", span.Value.GetText());
        }

        [Test]
        public void FindAnyWordSpan3()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(_textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent, WordKind.NormalWord, Path.Backward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("baz", span.Value.GetText());
        }

        [Test]
        public void FindAnyWordSpan4()
        {
            Create("foo bar baz");
            var span = TssUtil.FindAnyWordSpan(
                new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).End.Subtract(1).Position),
                WordKind.NormalWord,
                Path.Backward);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("ba", span.Value.GetText());
        }

        [Test]
        public void FindFirstNoneWhiteSpaceCharacter1()
        {
            Create("foo");
            var point = TssUtil.FindFirstNonWhiteSpaceCharacter(_textBuffer.GetLine(0));
            Assert.AreEqual(_textBuffer.GetLine(0).Start, point);
        }

        [Test]
        public void FindFirstNoneWhiteSpaceCharacter2()
        {
            Create(" foo");
            var point = TssUtil.FindFirstNonWhiteSpaceCharacter(_textBuffer.GetLine(0));
            Assert.AreEqual(_textBuffer.GetLine(0).Start.Add(1), point);
        }

        [Test]
        public void FindFirstNoneWhiteSpaceCharacter3()
        {
            Create("");
            var point = TssUtil.FindFirstNonWhiteSpaceCharacter(_textBuffer.GetLine(0));
            Assert.AreEqual(_textBuffer.GetLine(0).Start, point);
        }

        [Test]
        public void FindFirstNoneWhiteSpaceCharacter4()
        {
            Create("  bar");
            var point = TssUtil.FindFirstNonWhiteSpaceCharacter(_textBuffer.GetLine(0));
            Assert.AreEqual(_textBuffer.GetLine(0).Start.Add(2), point);
        }

        [Test]
        public void FindNextOccurranceOfCharOnLine1()
        {
            Create("foo bar jaz");
            var next = TssUtil.FindNextOccurranceOfCharOnLine(_textBuffer.GetLine(0).Start, 'o', 1);
            Assert.IsTrue(next.IsSome());
            Assert.AreEqual(1, next.Value.Position);
        }

        [Test]
        public void FindNextOccurranceOfCharOnLine2()
        {
            Create("foo bar jaz");
            var next = TssUtil.FindNextOccurranceOfCharOnLine(_textBuffer.GetLine(0).Start, 'q', 1);
            Assert.IsFalse(next.IsSome());
        }

        [Test, Description("Search starts on then next char")]
        public void FindNextOccurranceOfCharOnLine3()
        {
            Create("foo bar jaz");
            var next = TssUtil.FindNextOccurranceOfCharOnLine(_textBuffer.GetLine(0).Start, 'f', 1);
            Assert.IsFalse(next.IsSome());
        }

        [Test]
        public void FindNextOccurranceOfCharOnLine4()
        {
            Create("foo bar jaz");
            var next = TssUtil.FindNextOccurranceOfCharOnLine(_textBuffer.GetLine(0).Start, 'a', 2);
            Assert.IsTrue(next.IsSome());
            Assert.AreEqual(9, next.Value.Position);
        }

        [Test]
        public void FindTillNextOccuranceOfCharOnLine1()
        {
            Create("foo bar jaz");
            var next = TssUtil.FindTillNextOccurranceOfCharOnLine(_textBuffer.GetLine(0).Start, 'o', 1);
            Assert.IsTrue(next.IsSome());
            Assert.AreEqual(0, next.Value.Position);
        }

        [Test]
        public void FindTillNextOccuranceOfCharOnLine2()
        {
            Create("foo bar baz");
            var next = TssUtil.FindTillNextOccurranceOfCharOnLine(_textBuffer.GetPoint(0), 'o', 1);
            Assert.IsTrue(next.IsSome());
            Assert.AreEqual(0, next.Value.Position);
        }

        [Test]
        public void FindPreviousOccuranceOfCharOnLine1()
        {
            Create("foo bar baz");
            var prev = TssUtil.FindPreviousOccurranceOfCharOnLine(_textBuffer.GetPoint(0), 'f', 1);
            Assert.IsFalse(prev.IsSome());
        }

        [Test]
        public void FindPreviousOccuranceOfCharOnLine2()
        {
            Create("foo bar baz");
            var prev = TssUtil.FindPreviousOccurranceOfCharOnLine(_textBuffer.GetPoint(5), 'f', 1);
            Assert.IsTrue(prev.IsSome());
            Assert.AreEqual(0, prev.Value.Position);
        }

        [Test]
        public void FindPreviousOccuranceOfCharOnLine3()
        {
            Create("foo bar baz");
            var prev = TssUtil.FindPreviousOccurranceOfCharOnLine(_textBuffer.GetPoint(5), 'o', 2);
            Assert.IsTrue(prev.IsSome());
            Assert.AreEqual(1, prev.Value.Position);
        }

        [Test]
        public void FindTillPreviousOccuranceOfCharOnLine1()
        {
            Create("foo", "bar", "baz");
            var prev = TssUtil.FindTillPreviousOccurranceOfCharOnLine(_textBuffer.GetLine(2).Start, 'r', 1);
            Assert.IsFalse(prev.IsSome());
        }

        [Test]
        public void FindTillPreviousOccuranceOfCharOnLine2()
        {
            Create("foo", "bar", "baz");
            var prev = TssUtil.FindTillPreviousOccurranceOfCharOnLine(_textBuffer.GetLine(1).End, 'r', 1);
            Assert.IsFalse(prev.IsSome());
        }

        [Test]
        public void FindTillPreviousOccuranceOfCharOnLine3()
        {
            Create("foo", "bar", "baz");
            var prev = TssUtil.FindTillPreviousOccurranceOfCharOnLine(_textBuffer.GetLine(1).End, 'b', 1);
            Assert.IsTrue(prev.IsSome());
            Assert.AreEqual(_textBuffer.GetLine(1).Start.Add(1), prev.Value);
        }

    }
}
