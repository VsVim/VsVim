using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class NormalModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = Utils.EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
        }

        [Test]
        public void dd_OnLastLine()
        {
            CreateBuffer("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _buffer.ProcessAsString("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }
    }
}
