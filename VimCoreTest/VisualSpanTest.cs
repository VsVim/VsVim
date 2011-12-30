using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using EditorUtils;

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
        public void Select_Backwards()
        {
            Create("big dog", "big cat", "big tree", "big fish");
            var characterSpan = CharacterSpan.CreateForSpan(_textBuffer.GetSpan(1, 3));
            var visualSpan = VisualSpan.NewCharacter(characterSpan);
            visualSpan.Select(_textView, Path.Backward);
            Assert.IsTrue(_textView.Selection.IsReversed);
            Assert.AreEqual(characterSpan.Span, _textView.GetSelectionSpan());
        }
    }
}
