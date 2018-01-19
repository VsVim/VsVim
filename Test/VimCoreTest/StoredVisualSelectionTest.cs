using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class StoredVisualSelectionTest : VimTestBase
    {
        public sealed class GetVisualSpanTest : StoredVisualSelectionTest
        {
            [WpfTheory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void SimpleCharacter(int count)
            {
                var buffer = CreateTextBuffer("hello");
                var storedVisualSelection = StoredVisualSelection.NewCharacter(count);
                var visualSpan = storedVisualSelection.GetVisualSpan(buffer.GetStartPoint(), 1);
                Assert.Equal(count, visualSpan.AsCharacter().Item.Length);
                Assert.Equal("hello".Substring(0, count), visualSpan.Spans.Single().GetText());
            }

            [WpfTheory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void SimpleLine(int count)
            {
                var buffer = CreateTextBuffer("dog", "cat", "tree", "pony");
                var storedVisualSelection = StoredVisualSelection.NewLine(count);
                var visualSpan = storedVisualSelection.GetVisualSpan(buffer.GetStartPoint(), 1);
                Assert.Equal(count, visualSpan.AsLine().Item.Count);
            }

            [WpfFact]
            public void CharacterIntoLineBreak()
            {
                var buffer = CreateTextBuffer("dog", "cat");

            }
        }
    }
}
