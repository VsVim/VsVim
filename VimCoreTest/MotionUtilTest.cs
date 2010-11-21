using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class MotionUtilTest
    {
        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        private void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Test]
        public void GetSentences1()
        {
            Create("a. b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a.", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences2()
        {
            Create("a! b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences3()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences4()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences5()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), Direction.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " b.", "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences6()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(2), Direction.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences7()
        {
            Create("a?)]' b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?)]'", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences8()
        {
            Create("a?) b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), Direction.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " b.", "a?)" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Only a sentence end if followed by certain items")]
        public void GetSentence9()
        {
            Create("a!b. c");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!b.", " c" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Only a sentence end if followed by certain items")]
        public void GetSentence10()
        {
            Create("a!b. c");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), Direction.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " c", "a!b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Blank lines are sentence boundaries")]
        public void GetSentence11()
        {
            Create("a", "", "b");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), Direction.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine, "" + Environment.NewLine, "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Blank lines are sentence boundaries")]
        public void GetSentence12()
        {
            Create("a", "", "", "b");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine, "" + Environment.NewLine, "" + Environment.NewLine, "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentence13()
        {
            Create("dog", "cat", "bear");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint().Subtract(1), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "r" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs1()
        {
            Create("a", "b", "", "c");
            var ret = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), Direction.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine + "b" + Environment.NewLine, "" + Environment.NewLine, "c" },
                ret.Select(x => x.Span.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs2()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), Direction.Forward).ToList();
            Assert.AreEqual(Paragraph.NewContent(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak), list[0]);
            Assert.AreEqual(Paragraph.NewBoundary(2, _snapshot.GetLineRange(2).ExtentIncludingLineBreak), list[1]);
            Assert.AreEqual(Paragraph.NewContent(_snapshot.GetLineRange(3).ExtentIncludingLineBreak), list[2]);
        }

        [Test]
        public void GetParagraphs3()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), Direction.Forward).Take(1).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetLineRange(0,1).ExtentIncludingLineBreak)
                },
                list);
        }

        [Test]
        public void GetParagraphsInSpan1()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphsInSpan(_snapshot.GetLineRange(0).Extent, Direction.Forward).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetLineRange(0).Extent)
                },
                list);
        }

        [Test]
        public void GetFullParagraph1()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(1).Start);
            Assert.AreEqual(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
        }

        [Test]
        public void GetFullParagraph2()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(0).Start);
            Assert.AreEqual(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
        }

        [Test]
        public void GetFullParagraph3()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineRange(2, 3).ExtentIncludingLineBreak, span);
        }

        [Test]
        [Description("Get from a content boundary end")]
        public void GetFullParagraph4()
        {
            Create("dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineRange(2, 3).ExtentIncludingLineBreak, span);
        }

        [Test]
        [Description("Get from a content boundary end with preceeding boundaries")]
        public void GetFullParagraph5()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(3).Start);
            Assert.AreEqual(_snapshot.GetLineRange(3, 4).ExtentIncludingLineBreak, span);
        }

        [Test]
        [Description("Get from within a content portion")]
        public void GetFullParagraph6()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineRange(1, 3).ExtentIncludingLineBreak, span);
        }

        [Test]
        [Description("Get from within a boundary portion")]
        public void GetFullParagraph7()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetPoint(0));
            Assert.AreEqual(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
        }
    }
}
