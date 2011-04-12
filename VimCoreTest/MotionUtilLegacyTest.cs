using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MotionUtilLegacyTest
    {
        private ITextBuffer _textBuffer;
        private ITextSnapshot _snapshot;

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _snapshot = _textBuffer.CurrentSnapshot;
        }

        [Test]
        public void GetSentences1()
        {
            Create("a. b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a.", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences2()
        {
            Create("a! b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences3()
        {
            Create("a? b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a?", " b." },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetSentences4()
        {
            Create("a? b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetEndPoint(), Path.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure the return doesn't include an empty span for the end point
        /// </summary>
        [Test]
        public void GetSentences_BackwardFromEndOfBuffer()
        {
            Create("a? b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetEndPoint(), Path.Backward);
            CollectionAssert.AreEquivalent(
                new[] { " b.", "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Sentences are an exclusive motion and hence backward from a single whitespace 
        /// to a sentence boundary should not include the whitespace
        /// </summary>
        [Test]
        public void GetSentences_BackwardFromSingleWhitespace()
        {
            Create("a? b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(2), Path.Backward);
            CollectionAssert.AreEquivalent(
                new[] { "a?" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure we include many legal trailing characters
        /// </summary>
        [Test]
        public void GetSentences_ManyTrailingChars()
        {
            Create("a?)]' b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[] { "a?)]' ", "b." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// The character should go on the previous sentence
        /// </summary>
        [Test]
        public void GetSentences_BackwardWithCharBetween()
        {
            Create("a?) b.");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetEndPoint(), Path.Backward);
            CollectionAssert.AreEquivalent(
                new[] { "b.", "a?) " },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        [Description("Only a sentence end if followed by certain items")]
        public void GetSentence9()
        {
            Create("a!b. c");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new string[] { "a!b.", " c" },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Only a valid boundary if the end character is followed by one of the 
        /// legal follow up characters (spaces, tabs, end of line after trailing chars)
        /// </summary>
        [Test]
        public void GetSentence_IncompleteBoundary()
        {
            Create("a!b. c");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetEndPoint(), Path.Backward);
            CollectionAssert.AreEquivalent(
                new[] { " c", "a!b." },
                ret.Select(x => x.GetText()).ToList());
        }

        /// <summary>
        /// Make sure blank lines are included as sentence boundaries
        /// </summary>
        [Test]
        public void GetSentence_ForwardBlankLinesAreBoundaries()
        {
            Create("a", "", "", "b");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 0).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(1, 3).ExtentIncludingLineBreak
                },
                ret.ToList());
        }

        [Test]
        public void GetSentence13()
        {
            Create("dog", "cat", "bear");
            var ret = MotionUtilLegacy.GetSentences(_snapshot.GetEndPoint().Subtract(1), Path.Forward);
            CollectionAssert.AreEquivalent(
                new [] { "r" },
                ret.Select(x => x.GetText()).ToList());
        }

        [Test]
        public void GetParagraphs_SingleBreak()
        {
            Create("a", "b", "", "c");
            var ret = MotionUtilLegacy.GetParagraphs(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 3).ExtentIncludingLineBreak
                },
                ret.ToList());
        }

        /// <summary>
        /// Consequtive breaks should not produce separate paragraphs.  They are treated as 
        /// part of the same paragraph
        /// </summary>
        [Test]
        public void GetParagraphs_ConsequtiveBreaks()
        {
            Create("a", "b", "", "", "c");
            var ret = MotionUtilLegacy.GetParagraphs(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 4).ExtentIncludingLineBreak
                },
                ret.ToList());
        }

        /// <summary>
        /// Formfeed is a section and hence a paragraph boundary
        /// </summary>
        [Test]
        public void GetParagraphs_FormFeedShouldBeBoundary()
        {
            Create("a", "b", "\f", "", "c");
            var ret = MotionUtilLegacy.GetParagraphs(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 2).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(3, 4).ExtentIncludingLineBreak
                },
                ret.ToList());
        }

        /// <summary>
        /// A formfeed is a section boundary and should not count as a consequtive paragraph
        /// boundary
        /// </summary>
        [Test]
        public void GetParagraphs_FormFeedIsNotConsequtive()
        {
            Create("a", "b", "\f", "", "c");
            var ret = MotionUtilLegacy.GetParagraphs(_snapshot.GetPoint(0), Path.Forward);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(2, 2).ExtentIncludingLineBreak,
                    _textBuffer.GetLineRange(3, 4).ExtentIncludingLineBreak
                },
                ret.ToList());
        }
    }
}
