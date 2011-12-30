using EditorUtils.UnitTest;
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
        /// An empty selection should produce a block span of width 1.  Block's have to 
        /// have at least this width.  It's a corner case that comes up again and again
        /// </summary>
        [Test]
        public void CreateForSelection_Block_Empty()
        {
            Create("hello world");
            var visualSpan = VisualSpan.CreateForSelection(_textView, VisualKind.Block);
            Assert.AreEqual(1, visualSpan.EditSpan.OverarchingSpan.Length);
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
    }
}
