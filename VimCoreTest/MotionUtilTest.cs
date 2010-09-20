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
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a.", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences2()
        {
            Create("a! b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences3()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences4()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences5()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), SearchKind.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " b.", "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences6()
        {
            Create("a? b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(2), SearchKind.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences7()
        {
            Create("a?)]' b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?)]'", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences8()
        {
            Create("a?) b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), SearchKind.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " b.", "a?)" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences9()
        {
            Create("a?) b.");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(3), SearchKind.BackwardWithWrap);
            CollectionAssert.AreEquivalent(
                new string[] { "a?)", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Only a sentence end if followed by certain items")]
        public void GetSentence10()
        {
            Create("a!b. c");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!b.", " c" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Only a sentence end if followed by certain items")]
        public void GetSentence11()
        {
            Create("a!b. c");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), SearchKind.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { " c", "a!b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Blank lines are sentence boundaries")]
        public void GetSentence12()
        {
            Create("a", "", "b");
            var ret = MotionUtil.GetSentences(_snapshot.GetEndPoint(), SearchKind.Backward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine, "" + Environment.NewLine, "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Blank lines are sentence boundaries")]
        public void GetSentence13()
        {
            Create("a", "", "", "b");
            var ret = MotionUtil.GetSentences(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine, "" + Environment.NewLine, "" + Environment.NewLine, "b" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs1()
        {
            Create("a", "b", "", "c");
            var ret = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), SearchKind.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a" + Environment.NewLine + "b" + Environment.NewLine, "" + Environment.NewLine, "c" },
                ret.Select(x => x.Span.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs2()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), SearchKind.Forward).ToList();
            Assert.AreEqual(Paragraph.NewContent(_snapshot.GetLineSpanIncludingLineBreak(0, 1)), list[0]);
            Assert.AreEqual(Paragraph.NewBoundary(2, _snapshot.GetLineSpanIncludingLineBreak(2)), list[1]);
            Assert.AreEqual(Paragraph.NewContent(_snapshot.GetLineSpanIncludingLineBreak(3)), list[2]);
        }

        [Test]
        public void GetParagraphs3()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(0), SearchKind.Forward).Take(1).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetLineSpanIncludingLineBreak(0,1))
                },
                list);
        }

        [Test]
        public void GetParagraphs4()
        {
            Create("a?) b.");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(3), SearchKind.ForwardWithWrap).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetSpan(3, 3)),
                    Paragraph.NewContent(_snapshot.GetSpan(0, 3))
                },
                list);
        }

        [Test]
        public void GetParagraphs5()
        {
            Create("a?) b.");
            var list = MotionUtil.GetParagraphs(_snapshot.GetPoint(3), SearchKind.BackwardWithWrap).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetSpan(0, 3)),
                    Paragraph.NewContent(_snapshot.GetSpan(3, 3))
                },
                list);
        }

        [Test]
        public void GetParagraphsInSpan1()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphsInSpan(_snapshot.GetLineSpan(0), SearchKind.Forward).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetLineSpan(0))
                },
                list);
        }

        [Test]
        [Description("Wrap should be ignored")]
        public void GetParagraphsInSpan2()
        {
            Create("a", "b", "", "c");
            var list = MotionUtil.GetParagraphsInSpan(_snapshot.GetLineSpan(0), SearchKind.ForwardWithWrap).ToList();
            CollectionAssert.AreEquivalent(
                new Paragraph[] {
                    Paragraph.NewContent(_snapshot.GetLineSpan(0))
                },
                list);
        }

        [Test]
        public void GetFullParagraph1()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(1).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(0, 2), span);
        }

        [Test]
        public void GetFullParagraph2()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(0).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(0, 1), span);
        }

        [Test]
        public void GetFullParagraph3()
        {
            Create("a", "b", "", "c");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(2, 3), span);
        }

        [Test]
        [Description("Get from a content boundary end")]
        public void GetFullParagraph4()
        {
            Create("dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(2, 3), span);
        }

        [Test]
        [Description("Get from a content boundary end with preceeding boundaries")]
        public void GetFullParagraph5()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(3).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(3, 4), span);
        }

        [Test]
        [Description("Get from within a content portion")]
        public void GetFullParagraph6()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetLine(2).Start);
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(1, 3), span);
        }

        [Test]
        [Description("Get from within a boundary portion")]
        public void GetFullParagraph7()
        {
            Create("", "dog", "cat", "", "pig");
            var span = MotionUtil.GetFullParagraph(_snapshot.GetPoint(0));
            Assert.AreEqual(_snapshot.GetLineSpanIncludingLineBreak(0, 2), span);
        }
    }
}
