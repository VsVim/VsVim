using System;
using EditorUtils.UnitTest;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class CharacterSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        /// <summary>
        /// Verify End is correct for a single line
        /// </summary>
        [Test]
        public void End_SingleLine()
        {
            Create("cats", "dog");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 1, 2);
            Assert.AreEqual("at", characterSpan.Span.GetText());
        }

        /// <summary>
        /// Verify End is correct for multiple lines
        /// </summary>
        [Test]
        public void End_MultiLine()
        {
            Create("cats", "dogs");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 2, 2);
            Assert.AreEqual("ats" + Environment.NewLine + "do", characterSpan.Span.GetText());
        }

        /// <summary>
        /// The last point should be the last included point in the CharacterSpan
        /// </summary>
        [Test]
        public void Last_Simple()
        {
            Create("cats", "dogs");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 3));
            Assert.IsTrue(characterSpan.Last.IsSome());
            Assert.AreEqual('t', characterSpan.Last.Value.GetChar());
        }

        /// <summary>
        /// Zero length spans should have no Last value
        /// </summary>
        [Test]
        public void Last_ZeroLength()
        {
            Create("cats", "dogs");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 0));
            Assert.IsFalse(characterSpan.Last.IsSome());
        }

        /// <summary>
        /// Consider the case where there is an empty line and the span end in the line
        /// break of the empty line.  The last line must be included but it shouldn't
        /// have any length
        /// </summary>
        [Test]
        public void Create_LastLineEmpty()
        {
            Create("cat", "", "dog");
            var endPoint = _textBuffer.GetLine(1).End.Add(1);
            var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
            var characterSpan = CharacterSpan.CreateForSpan(span);
            Assert.AreEqual(endPoint, characterSpan.End);

            // The last line is included even though it's blank
            Assert.AreEqual(2, characterSpan.LineCount);
        }

        /// <summary>
        /// Similar case to the last line empty is column 0 in the last line is included
        /// </summary>
        [Test]
        public void Create_LastLineLengthOfOne()
        {
            Create("cat", "dog", "fish");
            var endPoint = _textBuffer.GetLine(1).Start.Add(1);
            var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
            var characterSpan = CharacterSpan.CreateForSpan(span);
            Assert.AreEqual(endPoint, characterSpan.End);
            Assert.AreEqual(2, characterSpan.LineCount);
        }

        /// <summary>
        /// Make sure operator equality functions as expected
        /// </summary>
        [Test]
        public void Equality_Operator()
        {
            Create("cat", "dog");
            EqualityUtil.RunAll(
                (left, right) => left == right,
                (left, right) => left != right,
                false,
                false,
                EqualityUnit.Create(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2))
                    .WithEqualValues(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2))
                    .WithNotEqualValues(new CharacterSpan(_textBuffer.GetPoint(1), 1, 2)));
        }
    }
}
