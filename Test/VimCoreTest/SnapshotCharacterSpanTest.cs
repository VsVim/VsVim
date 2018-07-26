using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class SnapshotCharacterSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
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

            [WpfFact]
            public void AddNextLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotCharacterSpan(_textBuffer.GetPoint(0));
                var column = original.Add(4);
                Assert.Equal(0, column.ColumnNumber);
                Assert.Equal(1, column.LineNumber);
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

            [WpfFact]
            public void SubtractBeforeLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotCharacterSpan(_textBuffer.GetLine(1).Start);
                var column = original.Subtract(2);
                Assert.Equal(2, column.ColumnNumber);
                Assert.Equal(0, column.LineNumber);
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
        }

        public sealed class SurrogatePairs : SnapshotCharacterSpanTest
        {
            [WpfFact]
            public void SurrogatePair()
            {
                // Extraterrestrial alien emoji from issue #1786.
                Create("'\U0001F47D'", "");
                Assert.Equal(6, _textBuffer.GetLine(0).ExtentIncludingLineBreak.GetText().Length);
                var column = new SnapshotCharacterSpan(_textBuffer.GetLine(0).Start);
                Assert.Equal(4, column.ColumnCountIncludingLineBreak);
                Assert.Equal(1, column.Width);
                column = new SnapshotCharacterSpan(column, 1);
                Assert.Equal(2, column.Width);
                column = new SnapshotCharacterSpan(column, 2);
                Assert.Equal(1, column.Width);
                column = new SnapshotCharacterSpan(column, 3);
                Assert.Equal(Environment.NewLine.Length, column.Width);
            }
        }
    }
}
