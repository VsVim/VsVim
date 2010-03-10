using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class VimBufferCreationListener : IVimBufferCreationListener
    {
        private readonly IBlockCaretFactoryService _blockCaretFactoryService;

        [ImportingConstructor]
        internal VimBufferCreationListener(IBlockCaretFactoryService blockCaretFactoryService)
        {
            _blockCaretFactoryService = blockCaretFactoryService;
        }

        public void VimBufferCreated(IVimBuffer buffer)
        {
            var textView = buffer.TextView as IWpfTextView;
            if (textView == null)
            {
                return;
            }

            // Setup the block caret 
            var caret = _blockCaretFactoryService.CreateBlockCaret(textView);
            var caretController = new BlockCaretController(buffer, caret);
        }
    }
}
