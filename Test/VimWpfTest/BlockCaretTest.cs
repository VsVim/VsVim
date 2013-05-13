using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UnitTest;
using Vim.UI.Wpf.Implementation;
using Vim.UI.Wpf.Implementation.BlockCaret;
using Vim.UI.Wpf.Implementation.CharDisplay;

namespace Vim.UI.Wpf.UnitTest
{
    public class BlockCaretTest : VimTestBase
    {
        private Mock<ITextView> _textview;
        private Mock<ITextCaret> _caret;
        private Mock<IEditorFormatMap> _formatMap;
        private Mock<IAdornmentLayer> _layer;
        private Mock<IClassificationFormatMap> _classificationFormatMap;
        private BlockCaret _blockCaretRaw;
        private IBlockCaret _blockCaret;

        private void Create()
        {
            _caret = new Mock<ITextCaret>(MockBehavior.Strict);
            _textview = new Mock<ITextView>(MockBehavior.Strict);
            _textview.SetupGet(x => x.Caret).Returns(_caret.Object);
            _formatMap = new Mock<IEditorFormatMap>(MockBehavior.Strict);
            _classificationFormatMap = new Mock<IClassificationFormatMap>(MockBehavior.Strict);
            _layer = new Mock<IAdornmentLayer>(MockBehavior.Strict);
            _blockCaretRaw = new BlockCaret(_textview.Object, _classificationFormatMap.Object, _formatMap.Object, _layer.Object, new ControlCharUtil(), ProtectedOperations);
            _blockCaret = _blockCaretRaw;
        }

        [Fact]
        public void TextView1()
        {
            Create();
            Assert.Same(_textview.Object, _blockCaret.TextView);
        }

        /// <summary>
        /// Don't throw when ContaintingTextViewLine throws
        /// </summary>
        [Fact]
        public void Show1()
        {
            Create();
            _caret.SetupGet(x => x.ContainingTextViewLine).Throws(new InvalidOperationException());
            _blockCaret.CaretDisplay = CaretDisplay.HalfBlock;
            _caret.Verify();
            Assert.Equal(CaretDisplay.HalfBlock, _blockCaret.CaretDisplay);
        }

        [Fact]
        public void Hide1()
        {
            Create();
            _caret.Setup(x => x.ContainingTextViewLine).Throws(new InvalidOperationException());
            _blockCaret.CaretDisplay = CaretDisplay.HalfBlock;
            _caret.Verify();
            _blockCaret.CaretDisplay = CaretDisplay.NormalCaret;
            _caret.Verify();
        }
    }
}
