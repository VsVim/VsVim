using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UnitTest;
using Vim.UI.Wpf.Implementation;
using Vim.UI.Wpf.Implementation.BlockCaret;
using Vim.UI.Wpf.Implementation.CharDisplay;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.UnitTest
{
    public class BlockCaretTest : VimTestBase
    {
        private Mock<IVimBufferData> _vimBufferData;
        private Mock<IWpfTextView> _textView;
        private Mock<ITextCaret> _caret;
        private Mock<ITextSelection> _selection;
        private Mock<IWpfTextViewLineCollection> _lines;
        private Mock<IWpfTextViewLine> _caretLine;
        private Mock<IEditorFormatMap> _formatMap;
        private Mock<IAdornmentLayer> _layer;
        private Mock<IClassificationFormatMap> _classificationFormatMap;
        private Mock<ISelectionUtil> _selectionUtil;
        private BlockCaret _blockCaretRaw;
        private IBlockCaret _blockCaret;

        private void Create()
        {
            _caret = new Mock<ITextCaret>(MockBehavior.Strict);
            _caret.SetupGet(x => x.Position).Returns(new CaretPosition());
            _selection = new Mock<ITextSelection>(MockBehavior.Strict);
            _textView = new Mock<IWpfTextView>(MockBehavior.Strict);
            _textView.SetupGet(x => x.Caret).Returns(_caret.Object);
            _textView.SetupGet(x => x.Selection).Returns(_selection.Object);
            _textView.SetupGet(x => x.IsClosed).Returns(false);
            _textView.SetupGet(x => x.InLayout).Returns(false);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);
            _caretLine = new Mock<IWpfTextViewLine>(MockBehavior.Strict);
            _caretLine.SetupGet(x => x.IsValid).Returns(true);
            _caretLine.SetupGet(x => x.VisibilityState).Returns(VisibilityState.FullyVisible);
            _lines = new Mock<IWpfTextViewLineCollection>(MockBehavior.Strict);
            _lines.SetupGet(x => x.IsValid).Returns(true);
            _lines.Setup(x => x.GetTextViewLineContainingBufferPosition(It.IsAny<SnapshotPoint>())).Returns(_caretLine.Object);
            _textView.SetupGet(x => x.TextViewLines).Returns(_lines.Object);
            _formatMap = new Mock<IEditorFormatMap>(MockBehavior.Strict);
            _classificationFormatMap = new Mock<IClassificationFormatMap>(MockBehavior.Strict);
            _layer = new Mock<IAdornmentLayer>(MockBehavior.Strict);
            _selectionUtil = new Mock<ISelectionUtil>(MockBehavior.Strict);
            _selectionUtil.SetupGet(x => x.IsMultiSelectionSupported).Returns(false);
            _vimBufferData = new Mock<IVimBufferData>(MockBehavior.Strict);
            _vimBufferData.SetupGet(x => x.TextView).Returns(_textView.Object);
            _vimBufferData.SetupGet(x => x.SelectionUtil).Returns(_selectionUtil.Object);
            _blockCaretRaw = new BlockCaret(_vimBufferData.Object, _classificationFormatMap.Object, _formatMap.Object, _layer.Object, new ControlCharUtil(), ProtectedOperations);
            _blockCaret = _blockCaretRaw;
        }

        [WpfFact]
        public void TextView1()
        {
            Create();
            Assert.Same(_textView.Object, _blockCaret.TextView);
        }

        /// <summary>
        /// Don't throw when GetTextViewLineContainingBufferPosition throws
        /// </summary>
        [WpfFact]
        public void Show1()
        {
            Create();
            _lines
                .Setup(x => x.GetTextViewLineContainingBufferPosition(It.IsAny<SnapshotPoint>()))
                .Throws(new InvalidOperationException())
                .Verifiable();
            _blockCaret.CaretDisplay = CaretDisplay.HalfBlock;
            _lines.Verify();
            Assert.Equal(CaretDisplay.HalfBlock, _blockCaret.CaretDisplay);
        }

        [WpfFact]
        public void Hide1()
        {
            Create();
            _lines
                .Setup(x => x.GetTextViewLineContainingBufferPosition(It.IsAny<SnapshotPoint>()))
                .Throws(new InvalidOperationException())
                .Verifiable();
            _blockCaret.CaretDisplay = CaretDisplay.HalfBlock;
            _lines.Verify();
            _blockCaret.CaretDisplay = CaretDisplay.NormalCaret;
            _lines.Verify();
        }
    }
}
