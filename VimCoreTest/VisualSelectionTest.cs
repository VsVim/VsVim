using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VisualSelectionTest
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
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
                true);
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
                false);
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
        /// Make sure we properly create from a forward selection
        /// </summary>
        [Test]
        public void Create_Character()
        {
            Create("hello world");
            var span = new SnapshotSpan(_textView.GetLine(0).Start, 2);
            _textView.SelectAndUpdateCaret(span);
            var visualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character);
            Assert.AreEqual(span, visualSelection.VisualSpan.EditSpan.OverarchingSpan);
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
            var visualSelection = VisualSelection.CreateForVisualSpan(visualSpan);
            Assert.AreEqual(_textView.GetLine(1).Start.Add(1), visualSelection.CaretPoint);
            Assert.AreEqual(visualSpan, visualSelection.VisualSpan);
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
                CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block);
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
            var blockSpanData = _textView.GetBlockSpan(1, 2, 0, 2);
            foreach (var blockCaretLocation in all)
            {
                var visualSelection = VisualSelection.NewBlock(blockSpanData, blockCaretLocation);
                CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Block);
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
            var all = new[] { true, false };
            foreach (var isForward in all)
            {
                var visualSelection = VisualSelection.NewCharacter(characterSpan, isForward);
                CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character);
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
            var all = new[] { true, false };
            foreach (var isForward in all)
            {
                var visualSelection = VisualSelection.NewCharacter(characterSpan, isForward);
                CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Character);
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
                var visualSelection = VisualSelection.NewLine(lineRange, false, column);
                CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line);
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
            var allDirections = new[] { true, false };
            foreach (var isForward in allDirections)
            {
                foreach (var column in all)
                {
                    var visualSelection = VisualSelection.NewLine(lineRange, isForward, column);
                    CommonUtil.SelectAndUpdateCaret(_textView, visualSelection);
                    var currentVisualSelection = VisualSelection.CreateForSelection(_textView, VisualKind.Line);
                    Assert.AreEqual(visualSelection, currentVisualSelection);
                }
            }
        }
    }
}
