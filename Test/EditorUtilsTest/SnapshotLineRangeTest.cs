using System;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public abstract class SnapshotLineRangeTest : EditorHostTest
    {
        private ITextBuffer _textBuffer;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorHost.CreateTextBuffer(lines);
        }

        public sealed class CreateTest : SnapshotLineRangeTest
        {
            [Fact]
            public void ForExtent()
            {
                Create("cat", "dog");
                var lineRange = SnapshotLineRange.CreateForExtent(_textBuffer.CurrentSnapshot);
                Assert.Equal(2, lineRange.Count);
                Assert.Equal(_textBuffer.CurrentSnapshot.Length, lineRange.ExtentIncludingLineBreak.Length);
            }

            [Fact]
            public void ForLine()
            {
                Create("cat", "dog");
                var line = _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1);
                var lineRange = SnapshotLineRange.CreateForLine(line);
                Assert.Equal(1, lineRange.StartLineNumber);
                Assert.Equal(1, lineRange.Count);
                Assert.Equal("dog", lineRange.GetText());
            }
        }

        public sealed class MiscTest : SnapshotLineRangeTest
        {
            [Fact]
            public void GetText()
            {
                Create("cat", "dog");
                Assert.Equal("dog", _textBuffer.GetLineRange(1).GetText());
            }

            [Fact]
            public void GetTextIncludingLineBreak()
            {
                Create("cat", "dog");
                Assert.Equal("cat" + Environment.NewLine, _textBuffer.GetLineRange(0).GetTextIncludingLineBreak());
            }

            [Fact]
            public void Lines1()
            {
                Create("a", "b");
                var lineRange = SnapshotLineRange.CreateForLineAndMaxCount(_textBuffer.GetLine(0), 400);
                Assert.Equal(2, lineRange.Count);
            }
        }

        public sealed class EqualityTest : SnapshotLineRangeTest
        {
            void Run(EqualityUnit<SnapshotLineRange> equalityUnit)
            {
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    equalityUnit);
            }

            [Fact]
            public void Simple()
            {
                Create("cat", "dog", "fish");
                var equalityUnit = EqualityUnit
                    .Create(_textBuffer.GetLineRange(1))
                    .WithEqualValues(_textBuffer.GetLineRange(1))
                    .WithNotEqualValues(_textBuffer.GetLineRange(1, 2), _textBuffer.GetLineRange(2));
                Run(equalityUnit);
            }

            [Fact]
            public void DifferentSnapshots()
            {
                Create("cat", "dog", "fish");
                var otherTextBuffer = CreateTextBuffer("cat", "dog", "fish");
                var equalityUnit = EqualityUnit
                    .Create(_textBuffer.GetLineRange(1))
                    .WithEqualValues(_textBuffer.GetLineRange(1))
                    .WithNotEqualValues(otherTextBuffer.GetLineRange(1));
                Run(equalityUnit);
            }
        }
    }
}
