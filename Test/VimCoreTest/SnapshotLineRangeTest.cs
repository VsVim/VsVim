using System;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class SnapshotLineRangeTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        public void Create(params string[] lines)
        {
            _textBuffer = VimEditorHost.CreateTextBuffer(lines);
        }

        public sealed class CreateTest : SnapshotLineRangeTest
        {
            [WpfFact]
            public void ForExtent()
            {
                Create("cat", "dog");
                var lineRange = SnapshotLineRange.CreateForExtent(_textBuffer.CurrentSnapshot);
                Assert.Equal(2, lineRange.Count);
                Assert.Equal(_textBuffer.CurrentSnapshot.Length, lineRange.ExtentIncludingLineBreak.Length);
            }

            [WpfFact]
            public void ForExtentWithTrailingLineBreak()
            {
                Create("cat", "dog", "");
                var lineRange = SnapshotLineRange.CreateForExtent(_textBuffer.CurrentSnapshot);
                Assert.Equal(2, lineRange.Count);
                Assert.Equal(_textBuffer.CurrentSnapshot.Length, lineRange.ExtentIncludingLineBreak.Length);
            }

            [WpfFact]
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
            [WpfFact]
            public void GetText()
            {
                Create("cat", "dog");
                Assert.Equal("dog", _textBuffer.GetLineRange(1).GetText());
            }

            [WpfFact]
            public void GetTextIncludingLineBreak()
            {
                Create("cat", "dog");
                Assert.Equal("cat" + Environment.NewLine, _textBuffer.GetLineRange(0).GetTextIncludingLineBreak());
            }

            [WpfFact]
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

            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog", "fish");
                var equalityUnit = EqualityUnit
                    .Create(_textBuffer.GetLineRange(1))
                    .WithEqualValues(_textBuffer.GetLineRange(1))
                    .WithNotEqualValues(_textBuffer.GetLineRange(1, 2), _textBuffer.GetLineRange(2));
                Run(equalityUnit);
            }

            [WpfFact]
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
