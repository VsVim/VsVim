using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Test the functionality of the InsertUtil set of operations
    /// </summary>
    [TestFixture]
    public sealed class InsertUtilTest : VimTestBase
    {
        private IVimBuffer _buffer;
        private ITextView _textView;
        private InsertUtil _insertUtilRaw;
        private IInsertUtil _insertUtil;

        /// <summary>
        /// Create the IVimBuffer with the given set of lines.  Note that we intentionally don't
        /// set the mode to Insert here because the given commands should work irrespective of the 
        /// mode
        /// </summary>
        /// <param name="lines"></param>
        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _insertUtilRaw = new InsertUtil(
                _buffer.VimBufferData,
                EditorUtil.FactoryService.CommonOperationsFactory.GetCommonOperations(_buffer.VimBufferData));
            _insertUtil = _insertUtilRaw;
        }

        /// <summary>
        /// Make sure that shift left functions correctly when the caret is in virtual
        /// space.  The virtual space should just be converted to spaces and processed
        /// as such
        /// </summary>
        [Test]
        public void ShiftLeft_FromVirtualSpace()
        {
            Create("", "dog");
            _buffer.GlobalSettings.ShiftWidth = 4;
            _textView.MoveCaretTo(0, 8);
            _insertUtilRaw.ShiftLineLeft();
            Assert.AreEqual("    ", _textView.GetLine(0).GetText());
        }
    }
}
