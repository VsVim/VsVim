using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class VisualSpanTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
        }

        /// <summary>
        /// The selection of a reverse character span should cause a reversed selection
        /// </summary>
        [Test]
        public void Select_Character_Backwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 3));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            visualSpan.Select(_textView, Path.Backward);
            Assert.IsTrue(_textView.Selection.IsReversed);
            Assert.AreEqual(characterSpan.Span, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a forward character span should cause a forward selection
        /// </summary>
        [Test]
        public void Select_Character_Forwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 3));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            visualSpan.Select(_textView, Path.Forward);
            Assert.IsFalse(_textView.Selection.IsReversed);
            Assert.AreEqual(characterSpan.Span, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a reverse line span should cause a reversed selection
        /// </summary>
        [Test]
        public void Select_Line_Backwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var lineRange = _textBuffer.GetLineRange(1);
            var visualSpan = VisualSpan.NewLine(lineRange);
            visualSpan.Select(_textView, Path.Backward);
            Assert.IsTrue(_textView.Selection.IsReversed);
            Assert.AreEqual(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// The selection of a forward line span should cause a forward selection
        /// </summary>
        [Test]
        public void Select_Line_Forward()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var lineRange = _textBuffer.GetLineRange(1);
            var visualSpan = VisualSpan.NewLine(lineRange);
            visualSpan.Select(_textView, Path.Forward);
            Assert.IsFalse(_textView.Selection.IsReversed);
            Assert.AreEqual(lineRange.ExtentIncludingLineBreak, _textView.GetSelectionSpan());
        }

        /// <summary>
        /// Simple selection of a block 
        /// </summary>
        [Test]
        public void Select_Block_Simple()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var blockSpan = _textBuffer.GetBlockSpan(1, 2, 0, 2);
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            visualSpan.Select(_textView, Path.Forward);
            Assert.AreEqual(blockSpan, _textView.GetSelectionBlockSpan());
            Assert.AreEqual(TextSelectionMode.Box, _textView.Selection.Mode);
        }

        /// <summary>
        /// An empty selection should produce an empty VisualSpan for character
        /// </summary>
        [Test]
        public void CreateForSelection_Character_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Character);
            Assert.AreEqual(0, visualSpan.EditSpan.OverarchingSpan.Length);
        }

        /// <summary>
        /// VisualSpan doesn't understand weird Vim semantics.  An empty selection is an 
        /// empty selection even if it's block
        /// </summary>
        [Test]
        public void CreateForSelection_Block_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Block);
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 0, 1);
            Assert.AreEqual(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// An empty selection should still produce a complete line selection for line
        /// </summary>
        [Test]
        public void CreateForSelection_Line_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Line);
            Assert.AreEqual(_textBuffer.GetLineRange(0), visualSpan.AsLine().Item);
        }

        /// <summary>
        /// Ensure creating a VisualSpan for an empty points results in an empty selection
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Block_Empty()
        {
            Create("dog cat");
            var point = _textBuffer.GetPoint(2);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, point, point);
            var blockSpan = new BlockSpan(point, 0, 1);
            Assert.AreEqual(blockSpan, visualSpan.AsBlock().Item);
        }

        [Test]
        public void CreateForSelectionPoints_Block_Backwards()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPoint(0));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 1);
            Assert.AreEqual(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Make sure that we properly handle the backward block selection which spans 
        /// multiple lines
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Block_BackwardsMultipleLines()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(2), _textBuffer.GetPointInLine(1, 1));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), 1, 2);
            Assert.AreEqual(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Make sure that we properly handle the forward block selection which spans 
        /// multiple lines
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Block_ForwardsMultipleLines()
        {
            Create("big cat", "big dog");
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Block, _textBuffer.GetPoint(1), _textBuffer.GetPointInLine(1, 3));
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(1), 2, 2);
            Assert.AreEqual(blockSpan, visualSpan.AsBlock().Item);
        }

        /// <summary>
        /// Ensure creating a VisualSpan for an empty points results in an empty selection
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Character_Empty()
        {
            Create("dog cat");
            var point = _textBuffer.GetPoint(2);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Character, point, point);
            Assert.AreEqual(point, visualSpan.AsCharacter().Item.Start);
            Assert.AreEqual(0, visualSpan.AsCharacter().Item.Length);
        }

        /// <summary>
        /// Ensure we handle the case where the start and end point are the same point at the 
        /// start of the line.  The code should return the single line range for the line 
        /// containing the points
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Line_SamePoint()
        {
            Create("cat", "dog", "tree");
            var point = _textBuffer.GetLine(1).Start;
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point);
            Assert.AreEqual(_textBuffer.GetLineRange(1), visualSpan.AsLine().LineRange);
        }

        /// <summary>
        /// Make sure the code handles the case where the caret is positioned at the end of the
        /// ITextSnapshot.  Should return the last line
        /// </summary>
        [Test]
        public void CreateForSelectionPoints_Line_EndOfSnapshot()
        {
            Create("cat", "dog");
            var point = new SnapshotPoint(_textBuffer.CurrentSnapshot, _textBuffer.CurrentSnapshot.Length);
            var visualSpan = VisualSpan.CreateForSelectionPoints(VisualKind.Line, point, point);
            Assert.AreEqual(1, visualSpan.AsLine().LineRange.LastLineNumber);
        }
    }
}
