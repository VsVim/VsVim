using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Vim;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Moq;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class BlockCaretTest
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
            _blockCaretRaw = new BlockCaret(_textview.Object, _formatMap.Object, _layer.Object);
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
            _caret.SetupSet(x => x.IsHidden = true).Verifiable();
            _blockCaret.Show();
            _caret.Verify();
            Assert.IsTrue(_blockCaret.IsShown);
        }

        [Test, Description("If not shown, do nothing")]
        public void Hide1()
        {
            Create();
            _blockCaret.Hide();
            Assert.IsFalse(_blockCaret.IsShown);
        }

        [Test]
        public void Hide2()
        {
            Create();
            _caret.SetupSet(x => x.IsHidden = true).Verifiable();
            _caret.Setup(x => x.ContainingTextViewLine).Throws(new InvalidOperationException());
            _blockCaret.Show();
            _caret.Verify();
            _caret.SetupSet(x => x.IsHidden = false).Verifiable();
            _layer.Setup(x => x.RemoveAdornmentsByTag(It.IsAny<object>())).Verifiable();
            _blockCaret.Hide();
            _caret.Verify();
            _layer.Verify();
        }
    }
}
