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
        private IVimLocalSettings _localSettings;
        private IVimGlobalSettings _globalSettings;

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
            _globalSettings = _buffer.GlobalSettings;
            _localSettings = _buffer.LocalSettings;

            var operations = EditorUtil.FactoryService.CommonOperationsFactory.GetCommonOperations(_buffer.VimBufferData);
            _insertUtilRaw = new InsertUtil(
                _buffer.VimBufferData,
                operations,
                new TextChangeTracker(_buffer.TextView, operations));
            _insertUtil = _insertUtilRaw;
        }

        /// <summary>
        /// Make sure the caret position is correct when inserting in the middle of a word
        /// </summary>
        [Test]
        public void InsertTab_MiddleOfText()
        {
            Create("hello");
            _textView.MoveCaretTo(2);
            _localSettings.ExpandTab = true;
            _globalSettings.ShiftWidth = 3;
            _insertUtilRaw.InsertTab();
            Assert.AreEqual("he   llo", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
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
