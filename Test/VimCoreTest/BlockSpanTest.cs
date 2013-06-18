using EditorUtils;
using Microsoft.VisualStudio.Text;
using System.Reflection;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class BlockSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public sealed class ColumnSpacesTest : BlockSpanTest
        {
            [Fact]
            public void StartOfLine()
            {
                Create("cat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(0, blockSpan.ColumnSpaces);
            }

            [Fact]
            public void InsideLine()
            {
                Create("cat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(1, blockSpan.ColumnSpaces);
            }

            [Fact]
            public void TabStart()
            {
                Create("\tcat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(4, blockSpan.ColumnSpaces);
            }
        }

        public sealed class EndTest : BlockSpanTest
        {
            /// <summary>
            /// Make sure the end point is correct for a single line BlockSpanData
            /// </summary>
            [Fact]
            public void SingleLine()
            {
                Create("cat", "dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 1);
                Assert.Equal(_textBuffer.GetLine(0).Start.Add(2), blockSpan.End);
            }

            /// <summary>
            /// Make sure the end point is correct for a multiline BlockSpanData
            /// </summary>
            [Fact]
            public void MultiLine()
            {
                Create("cat", "dog", "fish");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 2);
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(2), blockSpan.End);
            }

            /// <summary>
            /// When End is partially through a tab then it should actually be the
            /// point after the SnapshotPoint it is partially through.  Selection
            /// depends on this
            /// </summary>
            [Fact]
            public void EndPartiallyThroughTab()
            {
                Create("cat", "\tdog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: 4, spaces: 1, height: 2);
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), blockSpan.End);
            }
        }

        public sealed class MiscTest : BlockSpanTest
        {
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
                    EqualityUnit.Create(new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 2))
                        .WithEqualValues(new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 2))
                        .WithNotEqualValues(
                            new BlockSpan(_textBuffer.GetPoint(1), 4, 2, 2),
                            new BlockSpan(_textBuffer.GetPoint(1), 4, 2, 3)));
            }

            /// <summary>
            /// Make sure we don't screw up the trick to get the correct number of fields
            /// in the type
            /// </summary>
            [Fact]
            public void FieldCount()
            {
                var type = typeof(BlockSpan);
                Assert.Equal(4, type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length);
            }
        }
    }
}
