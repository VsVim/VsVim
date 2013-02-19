using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
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
        [Fact]
        public void End_SingleLine()
        {
            Create("cats", "dog");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 1, 2);
            Assert.Equal("at", characterSpan.Span.GetText());
        }

        /// <summary>
        /// Verify End is correct for multiple lines
        /// </summary>
        [Fact]
        public void End_MultiLine()
        {
            Create("cats", "dogs");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 2, 2);
            Assert.Equal("ats" + Environment.NewLine + "do", characterSpan.Span.GetText());
        }

        /// <summary>
        /// The last point should be the last included point in the CharacterSpan
        /// </summary>
        [Fact]
        public void Last_Simple()
        {
            Create("cats", "dogs");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 3));
            Assert.True(characterSpan.Last.IsSome());
            Assert.Equal('t', characterSpan.Last.Value.GetChar());
        }

        /// <summary>
        /// Zero length spans should have no Last value
        /// </summary>
        [Fact]
        public void Last_ZeroLength()
        {
            Create("cats", "dogs");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 0));
            Assert.False(characterSpan.Last.IsSome());
        }

        /// <summary>
        /// Consider the case where there is an empty line and the span end in the line
        /// break of the empty line.  The last line must be included but it shouldn't
        /// have any length
        /// </summary>
        [Fact]
        public void Create_LastLineEmpty()
        {
            Create("cat", "", "dog");
            var endPoint = _textBuffer.GetLine(1).End.Add(1);
            var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
            var characterSpan = CharacterSpan.CreateForSpan(span);
            Assert.Equal(endPoint, characterSpan.End);

            // The last line is included even though it's blank
            Assert.Equal(2, characterSpan.LineCount);
        }

        /// <summary>
        /// Similar case to the last line empty is column 0 in the last line is included
        /// </summary>
        [Fact]
        public void Create_LastLineLengthOfOne()
        {
            Create("cat", "dog", "fish");
            var endPoint = _textBuffer.GetLine(1).Start.Add(1);
            var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
            var characterSpan = CharacterSpan.CreateForSpan(span);
            Assert.Equal(endPoint, characterSpan.End);
            Assert.Equal(2, characterSpan.LineCount);
        }

        /// <summary>
        /// Make sure operator equality functions as expected
        /// </summary>
        [Fact]
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
