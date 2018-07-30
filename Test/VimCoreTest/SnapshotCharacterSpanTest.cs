using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    // TOOD: need to test specifying a point that is the second character of a line break
    // TODO: Consider removing position Apis and replace with start and end 
    // TODO: rename width to length
    // TODO: test add past end
    // TODO: test subtract before start
    public abstract class SnapshotCharacterSpanTest : VimTestBase
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

        public sealed class AddSubtractTest : SnapshotCharacterSpanTest
        {
            [WpfFact]
            public void AddSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotCharacterSpan(_textBuffer.GetPoint(0));
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
                var original = new SnapshotCharacterSpan(_textBuffer.GetPoint(0));
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
                var point = new SnapshotCharacterSpan(_textBuffer.GetPoint(0));
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
                var original = new SnapshotCharacterSpan(_textBuffer.GetLine(1).Start);
                var column = original.Add(-2);
                Assert.Equal(2, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
            }

            [WpfFact]
            public void SubtractSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotCharacterSpan(_textBuffer.GetPoint(1));
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
                var original = new SnapshotCharacterSpan(_textBuffer.GetLine(1).Start);
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
                var point = new SnapshotCharacterSpan(_textBuffer.GetEndPoint());
                point = point.Subtract(2);
                while (point.Point.Position != 0)
                {
                    Assert.True(point.IsLineBreak);
                    point = point.Subtract(1);
                    Assert.Equal(letters[i], point.GetText());

                    if (point.Point.Position != 0)
                    {
                        point = point.Subtract(1);
                        i--;
                    }
                }
            }
        }

        public sealed class Ctor : SnapshotCharacterSpanTest
        {
            [WpfFact]
            public void PointSimple()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(1);
                var column = new SnapshotCharacterSpan(point);
                Assert.Equal(0, column.LineNumber);
                Assert.Equal(1, column.ColumnNumber);
                Assert.False(column.IsLineBreak);
            }

            [WpfFact]
            public void PointInsideLineBreak()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(_textBuffer.GetLine(0).End);
                var column = new SnapshotCharacterSpan(point);
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
                var characterSpan = new SnapshotCharacterSpan(point);
                Assert.True(characterSpan.Point.Position == _textBuffer.CurrentSnapshot.Length);
            }
        }
    }
}
