using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Vim;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Moq;
using Vim.UI.Wpf.Implementation;
using Vim.UnitTest;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class BlockCaretTest : VimTestBase
    {
        private Mock<ITextView> _textview;
        private Mock<ITextCaret> _caret;
        private Mock<IEditorFormatMap> _formatMap;
        private Mock<IAdornmentLayer> _layer;
        private BlockCaret _blockCaretRaw;
        private IBlockCaret _blockCaret;

        private void Create()
        {
            _caret = new Mock<ITextCaret>(MockBehavior.Strict);
            _textview = new Mock<ITextView>(MockBehavior.Strict);
            _textview.SetupGet(x => x.Caret).Returns(_caret.Object);
            _formatMap = new Mock<IEditorFormatMap>(MockBehavior.Strict);
            _layer = new Mock<IAdornmentLayer>(MockBehavior.Strict);
            _blockCaretRaw = new BlockCaret(_textview.Object, _formatMap.Object, _layer.Object, ProtectedOperations);
            _blockCaret = _blockCaretRaw;
        }

        [Test]
        public void TextView1()
        {
            Create();
            Assert.AreSame(_textview.Object, _blockCaret.TextView);
        }

        [Test, Description("Don't throw when ContaintingTextViewLine throws")]
        public void Show1()
        {
            Create();
            _caret.SetupGet(x => x.ContainingTextViewLine).Throws(new InvalidOperationException());
            _blockCaret.CaretDisplay = CaretDisplay.HalfBlock;
            _caret.Verify();
            Assert.AreEqual(CaretDisplay.HalfBlock, _blockCaret.CaretDisplay);
        }

        [Test]
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
