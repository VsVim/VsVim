using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Interpreter;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    public sealed class InterpreterTest : VimTestBase
    {
        private VimBufferData _vimBufferData;
        private ITextBuffer _textBuffer;
        private Interpreter _interpreter;

        private void Create(params string[] lines)
        {
            _vimBufferData = CreateVimBufferData(lines);
            _textBuffer = _vimBufferData.TextBuffer;
            _interpreter = new Interpreter(
                _vimBufferData,
                CommonOperationsFactory.GetCommonOperations(_vimBufferData),
                FoldManagerFactory.GetFoldManager(_vimBufferData.TextView),
                new FileSystem());
        }

        /// <summary>
        /// Handle the case where the adjustment simply occurs on the current line 
        /// </summary>
        [Test]
        public void GetLine_AdjustmentOnCurrent()
        {
            Create("cat", "dog", "bear");
            var range = _interpreter.GetLine(LineSpecifier.NewAdjustmentOnCurrent(1));
            Assert.AreEqual(_textBuffer.GetLine(1).LineNumber, range.Value.LineNumber);
        }
    }
}
