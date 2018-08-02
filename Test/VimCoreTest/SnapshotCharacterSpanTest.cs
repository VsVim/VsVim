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
                var column = SnapshotColumn.TryCreateForLineAndColumnNumber(_textBuffer.CurrentSnapshot, -1, -1, FSharpOption.False);
                Assert.True(column.IsNone());
            }

            [WpfFact]
            public void TryCreateColumnSimple()
            {
                Create("cat");
                var column = SnapshotColumn.TryCreateForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 1, includeLineBreak: FSharpOption.False);
                Assert.True(column.IsSome(x => x.IsCharacter('a')));
            }

            [WpfFact]
            public void EmptyLineIsLineBreak()
            {
                Create("", "dog");
                var column = SnapshotColumn.TryCreateForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 0, includeLineBreak: FSharpOption.False);
                Assert.True(column.IsNone());
                column = SnapshotColumn.TryCreateForLineAndColumnNumber(_textBuffer.CurrentSnapshot, lineNumber: 0, columnNumber: 0, includeLineBreak: FSharpOption.True);
                Assert.True(column.IsSome(x => x.IsLineBreak));
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
    }
}
