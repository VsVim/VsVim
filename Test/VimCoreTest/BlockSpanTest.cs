using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using System.Linq;
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
            [WpfFact]
            public void StartOfLine()
            {
                Create("cat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(0, blockSpan.BeforeSpaces);
            }

            [WpfFact]
            public void InsideLine()
            {
                Create("cat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(1, blockSpan.BeforeSpaces);
            }

            [WpfFact]
            public void TabStart()
            {
                Create("\tcat dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), tabStop: 4, spaces: 1, height: 1);
                Assert.Equal(4, blockSpan.BeforeSpaces);
            }
        }

        public sealed class EndTest : BlockSpanTest
        {
            /// <summary>
            /// Make sure the end point is correct for a single line BlockSpanData
            /// </summary>
            [WpfFact]
            public void SingleLine()
            {
                Create("cat", "dog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 1);
                Assert.Equal(_textBuffer.GetColumn(lineNumber: 0, columnNumber: 2), blockSpan.End);
            }

            /// <summary>
            /// Make sure the end point is correct for a multiline BlockSpanData
            /// </summary>
            [WpfFact]
            public void MultiLine()
            {
                Create("cat", "dog", "fish");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 4, 2, 2);
                Assert.Equal(_textBuffer.GetColumn(lineNumber: 1, columnNumber: 2), blockSpan.End);
            }

            /// <summary>
            /// When End is partially through a tab then it should actually be the
            /// point after the SnapshotPoint it is partially through.  Selection
            /// depends on this
            /// </summary>
            [WpfFact]
            public void EndPartiallyThroughTab()
            {
                Create("cat", "\tdog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: 4, spaces: 1, height: 2);
                Assert.Equal(_textBuffer.GetColumn(lineNumber: 1, columnNumber: 1), blockSpan.End);
            }
        }

        public sealed class MiscTest : BlockSpanTest
        {
            /// <summary>
            /// Make sure operator equality functions as expected
            /// </summary>
            [WpfFact]
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
            [WpfFact]
            public void FieldCount()
            {
                var type = typeof(BlockSpan);
                Assert.Equal(4, type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length);
            }

            /// <summary>
            /// Block selection should include all non spacing characters
            /// </summary>
            [WpfFact]
            public void NonSpacing()
            {
                string[] lines = new string[] { "hello", "h\u0327e\u0301\u200bllo\u030a\u0305" };
                Create(lines);
                var blockSpan = _textBuffer.GetBlockSpan(0, length: 6, startLine: 0, lineCount: 2, tabStop: 4);
                Assert.Equal(lines[1], blockSpan.BlockOverlapColumnSpans.Rest.Head.InnerSpan.GetText());
            }
        }

        public sealed class TabTest : BlockSpanTest
        {
            [WpfFact]
            public void PartialTab()
            {
                Create("trucker", "\tdog");
                var blockSpan = new BlockSpan(_textBuffer.GetPoint(position: 2), tabStop: 4, spaces: 3, height: 2);
                Assert.Equal(
                    new[] { "uck", "  d" },
                    blockSpan.BlockOverlapColumnSpans.Select(x => x.GetText()));
            }
        }
    }
}
