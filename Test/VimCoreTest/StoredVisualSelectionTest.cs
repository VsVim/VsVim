using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class StoredVisualSelectionTest : VimTestBase
    {
        public sealed class GetVisualSelectionTest : StoredVisualSelectionTest
        {
            [WpfTheory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void SimpleCharacter(int count)
            {
                var textBuffer = CreateTextBuffer("hello");
                var storedVisualSelection = StoredVisualSelection.NewCharacter(count);
                var visualSpan = storedVisualSelection.GetVisualSelection(textBuffer.GetStartPoint(), 1).VisualSpan;
                Assert.Equal(count, visualSpan.AsCharacter().Item.Length);
                Assert.Equal("hello".Substring(0, count), visualSpan.Spans.Single().GetText());
            }

            [WpfTheory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void SimpleLine(int count)
            {
                var textBuffer = CreateTextBuffer("dog", "cat", "tree", "pony");
                var storedVisualSelection = StoredVisualSelection.NewLine(count);
                var visualSpan = storedVisualSelection.GetVisualSelection(textBuffer.GetStartPoint(), 1).VisualSpan;
                Assert.Equal(count, visualSpan.AsLine().Item.Count);
            }

            [WpfFact]
            public void CharacterIntoLineBreak()
            {
                var textBuffer = CreateTextBuffer("dog", "");
                var sel = StoredVisualSelection.NewCharacter(width: 20);
                var visualSpan = sel.GetVisualSelection(textBuffer.GetStartPoint(), 1).VisualSpan;
                var span = visualSpan.Spans.Single();
                Assert.Equal(textBuffer.GetPointInLine(line: 1, column: 0), span.End);
                Assert.Equal("dog" + Environment.NewLine, span.GetText());
            }

            [WpfFact]
            public void CharacterLineIntoLineBreak()
            {
                var textBuffer = CreateTextBuffer("dog", "cat", "fish", "t");
                var sel = StoredVisualSelection.NewCharacterLine(lineCount: 2, lastLineMaxOffset: 1);
                var point = textBuffer.GetPointInLine(line: 2, column: 2);
                var visualSpan = sel.GetVisualSelection(point, count: 1).VisualSpan;
                var span = visualSpan.Spans.Single();
                Assert.Equal(point, span.Start);
                Assert.Equal(textBuffer.GetEndPoint(), span.End);
            }

            /// <summary>
            /// When it's not possible for the selection to expand out to the required number of lines then 
            /// the selection can actually reverse. 
            /// </summary>
            [WpfFact]
            public void ReverseSelection()
            {
                var textBuffer = CreateTextBuffer("dog", "cat", "fish", "tree");
                var sel = StoredVisualSelection.NewCharacterLine(lineCount: 2, lastLineMaxOffset: -1);
                var point = textBuffer.GetPointInLine(line: 3, column: 2);
                var visualSelection = sel.GetVisualSelection(point, count: 1);
                Assert.Equal(SearchPath.Backward, visualSelection.AsCharacter().Item2);

                var span = visualSelection.VisualSpan.Spans.Single();
                Assert.Equal(textBuffer.GetPointInLine(line: 3, column: 1), span.Start);
                Assert.Equal(textBuffer.GetPointInLine(line: 3, column: 3), span.End);
            }
        }

        public sealed class CreateFromVisualSpanTest : StoredVisualSelectionTest
        {
            [WpfFact]
            public void NegativeLastLineOffset1()
            {
                var textBuffer = CreateTextBuffer("cat", "dog");
                var characterSpan = new CharacterSpan(
                    textBuffer.GetPointInLine(line: 0, column: 2),
                    textBuffer.GetPointInLine(line: 1, column: 1));
                var sel = StoredVisualSelection.CreateFromVisualSpan(VisualSpan.NewCharacter(characterSpan));
                Assert.Equal(StoredVisualSelection.NewCharacterLine(lineCount: 2, lastLineMaxOffset: -2), sel);
            }

            [WpfFact]
            public void NegativeLastLineOffset2()
            {
                var textBuffer = CreateTextBuffer("cat", "dog");
                var characterSpan = new CharacterSpan(
                    textBuffer.GetPointInLine(line: 0, column: 2),
                    lineCount: 2,
                    lastLineMaxPositionCount: 2);
                var sel = StoredVisualSelection.CreateFromVisualSpan(VisualSpan.NewCharacter(characterSpan));
                Assert.Equal(StoredVisualSelection.NewCharacterLine(lineCount: 2, lastLineMaxOffset: -1), sel);
            }
        }
    }
}
