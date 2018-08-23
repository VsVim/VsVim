using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.EditorHost;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class SnapshotColumnTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        private void CreateRaw(string content)
        {
            _textBuffer = CreateTextBufferRaw(content);
        }

        public sealed class AddSubtractTest : SnapshotColumnTest
        {
            [WpfFact]
            public void AddSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(0));
                var column = original.Add(1);
                Assert.Equal(1, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
            }

            [WpfTheory]
            [InlineData("\n")]
            [InlineData("\r\n")]
            public void AddNextLine(string lineBreakText)
            {
                CreateRaw("cat" + lineBreakText + "dog" + lineBreakText + "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(0));
                var column = original.Add(4);
                Assert.Equal(0, column.ColumnNumber);
                Assert.Equal(1, column.LineNumber);
            }

            [WpfTheory]
            [InlineData("\n")]
            [InlineData("\r\n")]
            public void AddManyLines(string lineBreakText)
            {
                var letters = CharUtil.LettersLower.Select(x => x.ToString()).ToArray();
                var content = string.Join(lineBreakText, letters);
                CreateRaw(content);
                var point = new SnapshotColumn(_textBuffer.GetPoint(0));
                for (var i = 0; i < letters.Length; i++)
                {
                    Assert.Equal(letters[i], point.GetText());
                    if (i + 1 < letters.Length)
                    {
                        point = point.Add(1);
                        Assert.True(point.IsLineBreak);
                        point = point.Add(1);
                    }
                }
            }

            [WpfFact]
            public void AddBeforeLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetLine(1).Start);
                var column = original.Add(-2);
                Assert.Equal(2, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
            }

            [WpfFact]
            public void SubtractSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(1));
                var column = original.Subtract(1);
                Assert.Equal(0, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
            }

            [WpfTheory]
            [InlineData("\n")]
            [InlineData("\r\n")]
            public void SubtractBeforeLine(string lineBreakText)
            {
                CreateRaw("cat" +lineBreakText + "dog" + lineBreakText + "fish");
                var original = new SnapshotColumn(_textBuffer.GetLine(1).Start);
                var column = original.Subtract(2);
                Assert.Equal(2, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
            }

            [WpfTheory]
            [InlineData("\n")]
            [InlineData("\r\n")]
            public void SubtractManyLines(string lineBreakText)
            {
                var letters = CharUtil.LettersLower.Select(x => x.ToString()).ToArray();
                var content = string.Join(lineBreakText, letters);
                CreateRaw(content);
                var i = letters.Length - 2;
                var point = new SnapshotColumn(_textBuffer.GetEndPoint());
                point = point.Subtract(2);
                while (!point.IsStartColumn)
                {
                    Assert.True(point.IsLineBreak);
                    point = point.Subtract(1);
                    Assert.Equal(letters[i], point.GetText());

                    if (!point.IsStartColumn)
                    {
                        point = point.Subtract(1);
                        i--;
                    }
                }
            }

            [WpfFact]
            public void TrySubtractInSameLineBetweenLines()
            {
                Create("cat", "dog");
                var column = new SnapshotColumn(_textBuffer.GetPointInLine(line: 1, column: 1));
                var previous = column.TrySubtractInLine(1);
                Assert.True(previous.IsSome(c => c.IsCharacter('d')));
                previous = previous.Value.TrySubtractInLine(1);
                Assert.True(previous.IsNone());
            }

            [WpfFact]
            public void TrySubtractInSameLineStartOfBuffer()
            {
                Create("cat", "dog");
                var column = new SnapshotColumn(_textBuffer.GetStartPoint());
                var previous = column.TrySubtractInLine(1);
                Assert.True(previous.IsNone());
            }
        }

        public sealed class TryAddInSameLineTest : SnapshotColumnTest
        {
            [WpfFact]
            public void ToLineBreak()
            {
                Create("cat", "dog");
                var column = new SnapshotColumn(_textBuffer.GetPointInLine(line: 0, column: 2));
                var next = column.TryAddInLine(1);
                Assert.True(next.IsNone());
            }

            [WpfFact]
            public void ToLineBreakWhenAllowed()
            {
                Create("cat", "dog");
                var column = new SnapshotColumn(_textBuffer.GetPointInLine(line: 0, column: 2));
                var next = column.TryAddInLine(1, includeLineBreak: FSharpOption.True);
                Assert.True(next.IsSome(x => x.IsLineBreak));
            }

            [WpfFact]
            public void EndPoint()
            {
                Create("cat", "dog");
                var column = new SnapshotColumn(_textBuffer.GetPointInLine(line: 1, column: 2));
                var next = column.TryAddInLine(1, includeLineBreak: FSharpOption.True);
                Assert.True(next.IsSome(x => x.IsEndColumn));

                next = column.TryAddInLine(1, includeLineBreak: FSharpOption.False);
                Assert.True(next.IsSome(x => x.IsEndColumn));
            }
        }

        public sealed class Ctor : SnapshotColumnTest
        {
            [WpfFact]
            public void PointSimple()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(1);
                var column = new SnapshotColumn(point);
                Assert.Equal(0, column.LineNumber);
                Assert.Equal(1, column.ColumnNumber);
                Assert.False(column.IsLineBreak);
            }

            [WpfFact]
            public void PointInsideLineBreak()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(_textBuffer.GetLine(0).End);
                var column = new SnapshotColumn(point);
                Assert.Equal(0, column.LineNumber);
                Assert.Equal(3, column.ColumnNumber);
                Assert.True(column.IsLineBreak);
                Assert.Equal("cat", column.Line.GetText());
            }

            [WpfFact]
            public void EndPoint()
            {
                Create("cat");
                var point = _textBuffer.GetEndPoint();
                var column = new SnapshotColumn(point);
                Assert.True(column.StartPoint.Position == _textBuffer.CurrentSnapshot.Length);
            }

            [WpfFact]
            public void EndPointOnEmptyLine()
            {
                Create("cat", "");
                var point = _textBuffer.GetEndPoint();
                var column = new SnapshotColumn(point);
                Assert.True(column.StartPoint.Position == _textBuffer.CurrentSnapshot.Length);
            }

            [WpfFact]
            public void EmptyLineAtEnd()
            {
                Create("cat", "");
                var point = _textBuffer.GetPointInLine(line: 1, column: 0);
                var column = new SnapshotColumn(point);
                Assert.True(column.StartPoint.Position == _textBuffer.CurrentSnapshot.Length);
            }

            [WpfFact]
            public void SecondPositionOfLineBreak()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(4);
                var column = new SnapshotColumn(point);
                Assert.True(column.StartPoint.Position == 3);
            }
        }

        public sealed class TryCreateTests : SnapshotColumnTest
        {
            [WpfFact]
            public void TryCreateLineNegative()
            {
                Create("cat");
                var column = SnapshotColumn.GetForLineAndColumnNumber(_textBuffer.CurrentSnapshot, -1, -1, FSharpOption.False);
                Assert.True(column.IsNone());
            }

            [WpfFact]
            public void TryCreateColumnSimple()
            {
                Create("cat");
                var column = SnapshotColumn.GetForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 1, includeLineBreak: FSharpOption.False);
                Assert.True(column.IsSome(x => x.IsCharacter('a')));
            }

            [WpfFact]
            public void EmptyLineIsLineBreak()
            {
                Create("", "dog");
                var column = SnapshotColumn.GetForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 0, includeLineBreak: FSharpOption.False);
                Assert.True(column.IsNone());
                column = SnapshotColumn.GetForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 0, includeLineBreak: FSharpOption.True);
                Assert.True(column.IsSome(x => x.IsLineBreak));
            }
        }

        public sealed class GetSpacesTest : SnapshotColumnTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat");
                var column = _textBuffer.GetColumnFromPosition(0);
                Assert.Equal(1, column.GetSpaces(42));
            }

            [WpfFact]
            public void Tab()
            {
                Create("c\tt");
                var column = _textBuffer.GetColumnFromPosition(1);
                Assert.Equal(42, column.GetSpaces(42));
                Assert.Equal(2, column.GetSpaces(2));
            }

            [WpfTheory]
            [InlineData("\n")]
            [InlineData("\r\n")]
            public void LineBreaks(string lineBreakText)
            {
                CreateRaw(string.Join(lineBreakText, "cat", "dog"));
                var column = _textBuffer.GetColumnFromPosition(3);
                Assert.True(column.IsLineBreak);
                Assert.Equal(1, column.GetSpaces(42));
            }

            [WpfFact]
            public void SurrogatePair()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien}{alien}");
                for (var i = 0; i < 4; i++)
                {
                    var column = _textBuffer.GetColumnFromPosition(i);
                    Assert.Equal(1, column.GetSpaces(42));
                    Assert.Equal(alien, column.GetText());
                }
            }
        }

        public sealed class GetSpacesInContextTest : SnapshotColumnTest
        {
            [WpfFact]
            public void TabOffset()
            {
                Create("a\tb");
                var column = _textBuffer.GetColumnFromPosition(1);
                Assert.Equal(8, column.GetSpaces(tabStop: 8));
                Assert.Equal(7, column.GetSpacesInContext(tabStop: 8));
            }

            [WpfFact]
            public void TabOnZero()
            {
                Create("\t\tb");
                var column = _textBuffer.GetColumnFromPosition(1);
                Assert.Equal(8, column.GetSpaces(tabStop: 8));
                Assert.Equal(8, column.GetSpacesInContext(tabStop: 8));
            }
        }

        public sealed class GetSpacesToColumn : SnapshotColumnTest
        {
            [WpfFact]
            public void AfterTab()
            {
                Create("\tcat");
                var column = _textBuffer.GetColumnFromPosition(1);
                Assert.Equal(3, column.GetSpacesToColumn(3));
                Assert.Equal(42, column.GetSpacesToColumn(42));
            }

            [WpfFact]
            public void TabInMiddle()
            {
                Create("c\tat");
                var column = _textBuffer.GetColumnFromPosition(1);
                Assert.Equal(1, column.GetSpacesToColumn(8));
                Assert.Equal(8, column.GetSpacesIncludingToColumn(8));
            }

            [WpfFact]
            public void AfterSurrogatePair()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien}cat");
                var column = _textBuffer.GetColumnFromPosition(2);
                Assert.Equal(1, column.GetSpacesToColumn(42));
            }

            [WpfFact]
            public void AfterSurrogatePair2()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien}{alien}cat");
                var column = _textBuffer.GetColumnFromPosition(4);
                Assert.Equal(2, column.GetSpacesToColumn(42));
            }

            [WpfFact]
            public void AfterSurrogatePair3()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien}a{alien}cat");
                var column = _textBuffer.GetColumnFromPosition(5);
                Assert.Equal(3, column.GetSpacesToColumn(42));
            }
        }

        public sealed class MiscTest : SnapshotColumnTest
        {
            [WpfFact]
            public void EndPoint()
            {
                Create("cat");
                var point = _textBuffer.GetEndPoint();
                var column = new SnapshotColumn(point);
                Assert.True(column.IsEndColumn);
                Assert.False(column.IsLineBreak);
            }
        }

        public sealed class EqualsTest : SnapshotCodePointTest
        {
            [WpfFact]
            public void SameBuffer()
            {
                var textBuffer = CreateTextBuffer("cat", "dog");
                EqualityUnit
                    .Create(textBuffer.GetColumnFromPosition(0))
                    .WithEqualValues(textBuffer.GetColumnFromPosition(0))
                    .WithNotEqualValues(
                        textBuffer.GetColumn(lineNumber: 1, columnNumber: 0),
                        textBuffer.GetColumnFromPosition(1),
                        textBuffer.GetColumnFromPosition(2))
                    .RunAll(
                        compEqualsOperator: (x, y) => x == y,
                        compNotEqualsOperator: (x, y) => x != y);
            }

            [WpfFact]
            public void SameBufferSurrogatePair()
            {
                const string alien = "\U0001F47D"; // 👽
                var textBuffer = CreateTextBuffer($"{alien}{alien}cat", "dog");
                EqualityUnit
                    .Create(textBuffer.GetColumnFromPosition(0))
                    .WithEqualValues(
                        textBuffer.GetColumnFromPosition(0),
                        textBuffer.GetColumnFromPosition(1))
                    .WithNotEqualValues(
                        textBuffer.GetColumnFromPosition(2),
                        textBuffer.GetColumnFromPosition(3),
                        textBuffer.GetColumnFromPosition(4))
                    .RunAll(
                        compEqualsOperator: (x, y) => x == y,
                        compNotEqualsOperator: (x, y) => x != y);
            }

            [WpfFact]
            public void DifferentBuffer()
            {
                var textBuffer1 = CreateTextBuffer("cat", "dog");
                var textBuffer2 = CreateTextBuffer("cat", "dog");
                EqualityUnit
                    .Create(textBuffer1.GetColumnFromPosition(0))
                    .WithEqualValues(textBuffer1.GetColumnFromPosition(0))
                    .WithNotEqualValues(
                        textBuffer2.GetColumnFromPosition(0),
                        textBuffer1.GetColumnFromPosition(1),
                        textBuffer1.GetColumnFromPosition(2))
                    .RunAll(
                        compEqualsOperator: (x, y) => x == y,
                        compNotEqualsOperator: (x, y) => x != y);
            }
        }
    }
}
