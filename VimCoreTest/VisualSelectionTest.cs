using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class VisualSelectionTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between block selection.  Only test the 
        /// top caret locations here as for a single line we will never give back a bottom one since
        /// it's just one line
        /// </summary>
        [Fact]
        public void BackAndForth_Block_SingleLine()
        {
            Create("cats", "dogs", "fish");
            var all = new[] { BlockCaretLocation.TopLeft, BlockCaretLocation.TopRight };
            var blockSpanData = _textView.GetBlockSpan(1, 2, 0, 1);
            foreach (var blockCaretLocation in all)
            {
                var visualSelection = VisualSelection.NewBlock(blockSpanData, blockCaretLocation);
                visualSelection.SelectAndMoveCaret(_textView);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive);
                Assert.Equal(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between block selection
        /// </summary>
        [Fact]
        public void BackAndForth_Block_MultiLine()
        {
            Create("cats", "dogs", "fish");
            var all = new[] { BlockCaretLocation.TopLeft, BlockCaretLocation.TopRight, BlockCaretLocation.BottomLeft, BlockCaretLocation.BottomRight };
            var blockSpan = _textView.GetBlockSpan(1, 2, 0, 2);
            foreach (var blockCaretLocation in all)
            {
                var visualSelection = VisualSelection.NewBlock(blockSpan, blockCaretLocation);
                visualSelection.SelectAndMoveCaret(_textView);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive);
                Assert.Equal(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between character selection
        /// </summary>
        [Fact]
        public void BackAndForth_Character_SingleLine()
        {
            Create("cats", "dogs");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 2));
            var all = new[] { Path.Forward, Path.Backward };
            foreach (var path in all)
            {
                var visualSelection = VisualSelection.NewCharacter(characterSpan, path);
                visualSelection.SelectAndMoveCaret(_textView);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive);
                Assert.Equal(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between character selection
        /// </summary>
        [Fact]
        public void BackAndForth_Character_MultiLine()
        {
            Create("cats", "dogs", "fish");
            var characterSpan = new CharacterSpan(_textView.GetLine(0).Start, 2, 4);
            var all = new[] { Path.Forward, Path.Backward };
            foreach (var path in all)
            {
                var visualSelection = VisualSelection.NewCharacter(characterSpan, path);
                visualSelection.SelectAndMoveCaret(_textView);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive);
                Assert.Equal(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between line selection.  Don't check 
        /// forward / back on single line as they're the same
        /// </summary>
        [Fact]
        public void BackAndForth_Line_SingleLine()
        {
            Create("cats", "dogs", "fish");
            var lineRange = _textView.GetLineRange(0, 0);
            var all = Enumerable.Range(0, 3);
            foreach (var column in all)
            {
                var visualSelection = VisualSelection.NewLine(lineRange, Path.Backward, column);
                visualSelection.SelectAndMoveCaret(_textView);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive);
                Assert.Equal(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between line selection
        /// </summary>
        [Fact]
        public void BackAndForth_Line_MultiLine()
        {
            Create("cats", "dogs", "fish");
            var lineRange = _textView.GetLineRange(0, 1);
            var all = Enumerable.Range(0, 3);
            var allDirections = new[] { Path.Forward, Path.Backward };
            foreach (var path in allDirections)
            {
                foreach (var column in all)
                {
                    var visualSelection = VisualSelection.NewLine(lineRange, path, column);
                    visualSelection.SelectAndMoveCaret(_textView);
                    var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive);
                    Assert.Equal(visualSelection, currentVisualSelection);
                }
            }
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a forward character span
        /// </summary>
        [Fact]
        public void GetCaretPoint_Character_Forward()
        {
            Create("cats", "dogs");
            var visualSelection = VisualSelection.NewCharacter(
                CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 2)),
                Path.Forward);
            Assert.Equal(_textBuffer.GetPoint(1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a backward character span
        /// </summary>
        [Fact]
        public void GetCaretPoint_Character_Backward()
        {
            Create("cats", "dogs");
            var visualSelection = VisualSelection.NewCharacter(
                CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 2)),
                Path.Backward);
            Assert.Equal(_textBuffer.GetPoint(0), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
        }

        /// <summary>
        /// When the last line in a character selection is empty the caret -1 offset
        /// shouldn't be applied to the End location.  Else we'd end up in the line
        /// break instead of the start of the line
        /// </summary>
        [Fact]
        public void GetCaretPoint_Character_EmptyLastLine()
        {
            Create("cat", "", "dog");
            var visualSelection = VisualSelection.NewCharacter(
                new CharacterSpan(_textBuffer.GetPoint(0), 2, 1),
                Path.Forward);
            Assert.Equal(_textBuffer.GetLine(1).Start, visualSelection.GetCaretPoint(SelectionKind.Inclusive));
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a top right block selection
        /// </summary>
        [Fact]
        public void GetCaretPoint_Block_TopRight()
        {
            Create("cats", "dogs", "fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.TopRight);
            Assert.Equal(_textBuffer.GetPoint(2), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
        }

        /// <summary>
        /// Ensure the caret point is appropriately on the bottom right for a block selection
        /// </summary>
        [Fact]
        public void GetCaretPoint_Block_BottomRight()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomRight);
            Assert.Equal(_textView.GetPointInLine(1, 1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
        }

        /// <summary>
        /// Make sure we properly create from a forward selection
        /// </summary>
        [Fact]
        public void Create_Character()
        {
            Create("hello world");
            var span = new SnapshotSpan(_textView.GetLine(0).Start, 2);
            _textView.SelectAndMoveCaret(span);
            var visualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive);
            Assert.Equal(span, visualSelection.VisualSpan.EditSpan.OverarchingSpan);
            Assert.Equal(ModeKind.VisualCharacter, visualSelection.ModeKind);
            Assert.True(visualSelection.IsCharacterForward);
        }

        /// <summary>
        /// Create from a Block VisualSpan and make sure we get the appropriate CaretPoint
        /// </summary>
        [Fact]
        public void CreateForVisualSpan_Block()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlockSpan(0, 2, 0, 2));
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            Assert.Equal(_textView.GetLine(1).Start.Add(1), visualSelection.GetCaretPoint(SelectionKind.Inclusive));
            Assert.Equal(visualSpan, visualSelection.VisualSpan);
        }

        /// <summary>
        /// Ensure that a backwards character span includes the caret point in the span
        /// </summary>
        [Fact]
        public void CreateForPoints_Character_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.Equal("ats", visualSelection.VisualSpan.EditSpan.OverarchingSpan.GetText());
        }

        /// <summary>
        /// Ensure that a backwards line span includes the entire line
        /// </summary>
        [Fact]
        public void CreateForPoints_Line_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.Equal(_textBuffer.GetLineRange(0), visualSelection.AsLine().Item1);
        }

        /// <summary>
        /// When the caret is at the start of a line make sure that we include that line in the 
        /// selection.  This is different than the activePoint which wouldn't cause the line to 
        /// be included because it's past the selection
        /// </summary>
        [Fact]
        public void CreateForPoints_Line_CaretAtStart()
        {
            Create("cat", "dog", "bear");
            var visualSpan = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(0), caretPoint: _textBuffer.GetPointInLine(1, 0));
            Assert.Equal(_textBuffer.GetLineRange(0, 1), visualSpan.AsLine().Item1);
        }

        /// <summary>
        /// Ensure that a backwards block span includes the entire line
        /// </summary>
        [Fact]
        public void CreateForPoints_Block_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Block, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.Equal(_textBuffer.GetSpan(1, 3), visualSelection.AsBlock().Item1.BlockSpans.Head);
        }

        /// <summary>
        /// Get the selection for a backwards block that spans multiple lines.  In this case the 
        /// point is actually ahead of the anchor point in the position sense but the selection is 
        /// still backwards
        /// </summary>
        [Fact]
        public void CreateForPoints_Block_BackwardSeveralLines()
        {
            Create("big cat", "big dog");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPointInLine(1, 1));
            Assert.Equal(_textBuffer.GetBlockSpan(1, 2, 0, 2), visualSelection.AsBlock().Item1);
        }

        /// <summary>
        /// Block selection of width 1 with exclusive selection should still have a single
        /// column.  Even though normal block selections are shrunk by a column
        /// </summary>
        [Fact]
        public void AdjustForSelectionKind_Block_SingleColumnExclusive()
        {
            Create("cats", "dogs", "trees");
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            Assert.Equal(visualSelection, visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive));
        }

        /// <summary>
        /// Block selection should lose a column in Exclusive
        /// </summary>
        [Fact]
        public void AdjustForSelectionKind_Block_MultiColumnExclusive()
        {
            Create("cats", "dogs", "trees");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            var otherVisualSelection = visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive);
            var otherBlockSpan = otherVisualSelection.AsBlock().Item1;
            Assert.Equal(blockSpan.Start, otherBlockSpan.Start);
            Assert.Equal(1, otherBlockSpan.Width);
            Assert.Equal(2, otherBlockSpan.Height);
        }

        /// <summary>
        /// A character span should lose one if we adjust it for exclusive 
        /// </summary>
        [Fact]
        public void AdjustForSelectionKind_Character_Forward()
        {
            Create("cat dog bear");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 4));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            Assert.Equal("cat", visualSelection.AdjustForSelectionKind(SelectionKind.Exclusive).VisualSpan.EditSpan.OverarchingSpan.GetText());
        }

    }
}
