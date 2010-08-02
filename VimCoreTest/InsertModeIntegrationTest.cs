using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class InsertModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = Utils.EditorUtil.CreateViewAndOperations(lines);
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
    }
}
