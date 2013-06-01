using System;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class CharacterSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public abstract class ConstructionTest : CharacterSpanTest
        {
            public sealed class StandardTest : ConstructionTest
            {
                [Fact]
                public void SingleLineWhichIsEmpty()
                {
                    Create("cat", "", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetLine(1).Start, 1, 0);
                    Assert.Equal(2, characterSpan.Length);
                    Assert.Equal(2, characterSpan.LastLineLength);
                }

                /// <summary>
                /// A character span can't extend into the line break
                /// </summary>
                [Fact]
                public void IntoLineBreak()
                {
                    Create("cat", "", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetLine(0).Start, 0, 4);
                    Assert.Equal(3, characterSpan.LastLineLength);
                }

                [Fact]
                public void IntoLineBreakDeep()
                {
                    Create("cat", "", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetLine(0).Start, 0, 5);
                    Assert.Equal(3, characterSpan.LastLineLength);
                }

                /// <summary>
                /// If the last line is empty it should still be included in the CharacterSpan.  This is
                /// an odd special case we have to handle
                /// </summary>
                [Fact]
                public void LastLineEmpty()
                {
                    Create("cat", "", "dog");
                    var characterSpan = new CharacterSpan(_textBuffer.GetPoint(0), 2, 0);
                    Assert.Equal(2, characterSpan.LineCount);
                    Assert.Equal(2, characterSpan.LastLineLength);
                    Assert.Equal(_textBuffer.GetLine(2).Start, characterSpan.End);
                }
            }

            public sealed class SpanTest : ConstructionTest
            {
                /// <summary>
                /// Consider the case where there is an empty line and the span end in the line
                /// break of the empty line.  The last line must be included but it shouldn't
                /// have any length
                /// </summary>
                [Fact]
                public void LastLineEmpty()
                {
                    Create("cat", "", "dog");
                    var endPoint = _textBuffer.GetLine(1).End.Add(1);
                    var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
                    var characterSpan = new CharacterSpan(span);
                    Assert.Equal(characterSpan.End, _textBuffer.GetLine(1).EndIncludingLineBreak);

                    // The last line is included even though it's blank
                    Assert.Equal(2, characterSpan.LineCount);
                    Assert.Equal(2, characterSpan.LastLineLength);
                }

                /// <summary>
                /// Make sure we don't include the next line when including line break and the next
                /// line is empty
                /// </summary>
                [Fact]
                public void IncludeLineBreakNextLineEmpty()
                {
                    Create("cat", "", "dog");
                    var span = _textBuffer.GetLine(0).ExtentIncludingLineBreak;
                    var characterSpan = new CharacterSpan(span);
                    Assert.Equal(1, characterSpan.LineCount);
                    Assert.Equal(span, characterSpan.Span);
                }

                /// <summary>
                /// Similar case to the last line empty is column 0 in the last line is included
                /// </summary>
                [Fact]
                public void LastLineLengthOfOne()
                {
                    Create("cat", "dog", "fish");
                    var endPoint = _textBuffer.GetLine(1).Start.Add(1);
                    var span = new SnapshotSpan(_textBuffer.GetPoint(0), endPoint);
                    var characterSpan = new CharacterSpan(span);
                    Assert.Equal(endPoint, characterSpan.End);
                    Assert.Equal(2, characterSpan.LineCount);
                }
            }
        }

        public sealed class IncludeLastLineLineBreakTest : CharacterSpanTest
        {
            [Fact]
            public void EmptyLastLine()
            {
                Create("cat", "", "dog");
                var characterSpan = new CharacterSpan(_textBuffer.GetPoint(0), 2, 0);
                Assert.True(characterSpan.IncludeLastLineLineBreak);
            }

            [Fact]
            public void EmptyLine()
            {
                Create("cat", "", "dog");
                var characterSpan = new CharacterSpan(_textBuffer.GetLine(1).Start, 1, 0);
                Assert.True(characterSpan.IncludeLastLineLineBreak);
            }

            [Fact]
            public void EndOfNonEmptyLine()
            {
                Create("cat", "", "dog");
                var characterSpan = new CharacterSpan(_textBuffer.GetPoint(2), 1, 2);
                Assert.True(characterSpan.IncludeLastLineLineBreak);
            }

            [Fact]
            public void UpToLineBreak()
            {
                Create("cat", "", "dog");
                var characterSpan = new CharacterSpan(_textBuffer.GetPoint(0), 1, 3);
                Assert.False(characterSpan.IncludeLastLineLineBreak);
            }
        }

        public sealed class MiscTest : CharacterSpanTest
        {
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
                var characterSpan = new CharacterSpan(_textBuffer.GetSpan(0, 3));
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
                var characterSpan = new CharacterSpan(_textBuffer.GetSpan(0, 0));
                Assert.False(characterSpan.Last.IsSome());
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

            /// <summary>
            /// One of the trickiest items to get correct in the code is to distinguish between the 
            /// following cases.  Consider the below buffer sample for context
            ///
            ///   dog
            ///     
            ///   cat
            ///
            /// This is a 3 line buffer where the second line is blank (it has length 0).  The question 
            /// is how to you distinguish between a character span which includes the entire first line
            /// but nothing of the second line and one which includes the second line.  Here is the code 
            /// to differentiate the two 
            /// </summary>
            [Fact]
            public void EmptyLineDifferentiationTest()
            {
                Create("dog", "", "cat");
                var span1 = new CharacterSpan(_textBuffer.GetPoint(0), 1, 5);
                Assert.Equal(1, span1.LineCount);
                Assert.Equal(3, span1.LastLine.Length);

                var span2 = new CharacterSpan(_textBuffer.GetPoint(0), 2, 0);
                Assert.Equal(2, span2.LineCount);
                Assert.Equal(0, span2.LastLine.Length);

                Assert.NotEqual(span1, span2);
            }
        }
    }
}
