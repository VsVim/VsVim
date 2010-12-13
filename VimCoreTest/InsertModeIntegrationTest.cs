using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class InsertModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
        }

        [Test]
        [Description("Make sure we don't access the ITextView on the way down")]
        public void CloseInInsertMode()
        {
            CreateBuffer("foo", "bar");
            _textView.Close();
        }

        /// <summary>
        /// This test is mainly a regression test against the selection change logic
        /// </summary>
        [Test]
        [Description("Make sure a minor selection change doesn't move us into Normal mode")]
        public void SelectionChange1()
        {
            CreateBuffer("foo", "bar");
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(0).Start, 0));
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

    }
}
