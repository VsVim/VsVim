using System.Linq;
using EditorUtils.UnitTest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
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
        [Test]
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
                Assert.AreEqual(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between block selection
        /// </summary>
        [Test]
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
                Assert.AreEqual(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between character selection
        /// </summary>
        [Test]
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
                Assert.AreEqual(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between character selection
        /// </summary>
        [Test]
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
                Assert.AreEqual(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between line selection.  Don't check 
        /// forward / back on single line as they're the same
        /// </summary>
        [Test]
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
                Assert.AreEqual(visualSelection, currentVisualSelection);
            }
        }

        /// <summary>
        /// Make sure we get parity when going back and forth between line selection
        /// </summary>
        [Test]
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
                    Assert.AreEqual(visualSelection, currentVisualSelection);
                }
            }
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a forward character span
        /// </summary>
        [Test]
        public void CaretPoint_Character_Forward()
        {
            Create("cats", "dogs");
            var visualSelection = VisualSelection.NewCharacter(
                CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 2)),
                Path.Forward);
            Assert.AreEqual(_textBuffer.GetPoint(1), visualSelection.CaretPoint);
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a backward character span
        /// </summary>
        [Test]
        public void CaretPoint_Character_Backward()
        {
            Create("cats", "dogs");
            var visualSelection = VisualSelection.NewCharacter(
                CharacterSpan.CreateForSpan(_textBuffer.GetSpan(0, 2)),
                Path.Backward);
            Assert.AreEqual(_textBuffer.GetPoint(0), visualSelection.CaretPoint);
        }

        /// <summary>
        /// Get the appropriate Caret SnapshotPoint for a top right block selection
        /// </summary>
        [Test]
        public void CaretPoint_Block_TopRight()
        {
            Create("cats", "dogs", "fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.TopRight);
            Assert.AreEqual(_textBuffer.GetPoint(2), visualSelection.CaretPoint);
        }

        /// <summary>
        /// Ensure the caret point is appropriately on the bottom right for a block selection
        /// </summary>
        [Test]
        public void CaretPoint_Block_BottomRight()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomRight);
            Assert.AreEqual(_textView.GetPointInLine(1, 1), visualSelection.CaretPoint);
        }

        /// <summary>
        /// Make sure we properly create from a forward selection
        /// </summary>
        [Test]
        public void Create_Character()
        {
            Create("hello world");
            var span = new SnapshotSpan(_textView.GetLine(0).Start, 2);
            _textView.SelectAndMoveCaret(span);
            var visualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive);
            Assert.AreEqual(span, visualSelection.GetVisualSpan(SelectionKind.Inclusive).EditSpan.OverarchingSpan);
            Assert.AreEqual(ModeKind.VisualCharacter, visualSelection.ModeKind);
            Assert.IsTrue(visualSelection.IsCharacterForward);
        }

        /// <summary>
        /// Create from a Block VisualSpan and make sure we get the appropriate CaretPoint
        /// </summary>
        [Test]
        public void CreateForVisualSpan_Block()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlockSpan(0, 2, 0, 2));
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            Assert.AreEqual(_textView.GetLine(1).Start.Add(1), visualSelection.CaretPoint);
            Assert.AreEqual(visualSpan, visualSelection.GetVisualSpan(SelectionKind.Inclusive));
        }

        /// <summary>
        /// Ensure that a backwards character span includes the caret point in the span
        /// </summary>
        [Test]
        public void CreateForPoints_Character_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Character, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.AreEqual("ats", visualSelection.GetEditSpan(SelectionKind.Inclusive).OverarchingSpan.GetText());
        }

        /// <summary>
        /// Ensure that a backwards line span includes the entire line
        /// </summary>
        [Test]
        public void CreateForPoints_Line_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.AreEqual(_textBuffer.GetLineRange(0), visualSelection.AsLine().Item1);
        }

        /// <summary>
        /// When the caret is at the start of a line make sure that we include that line in the 
        /// selection.  This is different than the activePoint which wouldn't cause the line to 
        /// be included because it's past the selection
        /// </summary>
        [Test]
        public void CreateForPoints_Line_CaretAtStart()
        {
            Create("cat", "dog", "bear");
            var visualSpan = VisualSelection.CreateForPoints(VisualKind.Line, _textBuffer.GetPoint(0), caretPoint: _textBuffer.GetPointInLine(1, 0));
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), visualSpan.AsLine().Item1);
        }

        /// <summary>
        /// Ensure that a backwards block span includes the entire line
        /// </summary>
        [Test]
        public void CreateForPoints_Block_Backwards()
        {
            Create("cats dogs");
            var visualSelection = VisualSelection.CreateForPoints(VisualKind.Block, _textBuffer.GetPoint(3), _textBuffer.GetPoint(1));
            Assert.AreEqual(_textBuffer.GetSpan(1, 3), visualSelection.AsBlock().Item1.BlockSpans.Head);
        }

        /// <summary>
        /// Block selection of width 1 with exclusive selection should still have a single
        /// column.  Even though normal block selections are shrunk by a column
        /// </summary>
        [Test]
        public void GetVisualSpan_Block_SingleColumnExclusive()
        {
            Create("cats", "dogs", "trees");
            var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            Assert.AreEqual(visualSpan, visualSelection.GetVisualSpan(SelectionKind.Exclusive));
        }

        /// <summary>
        /// Block selection should lose a column in Exclusive
        /// </summary>
        [Test]
        public void GetVisualSpan_Block_MultiColumnExclusive()
        {
            Create("cats", "dogs", "trees");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            var otherBlockSpan = visualSelection.GetVisualSpan(SelectionKind.Exclusive).AsBlock().Item;
            Assert.AreEqual(blockSpan.Start, otherBlockSpan.Start);
            Assert.AreEqual(1, otherBlockSpan.Width);
            Assert.AreEqual(2, otherBlockSpan.Height);
        }
    }
}
