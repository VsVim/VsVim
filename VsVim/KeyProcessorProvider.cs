using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Vim;
using Microsoft.VisualStudio.Utilities;

namespace VsVim
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = "VisualStudioKeyProcessor")]
    [Name("VsVim")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType("text")]
    public sealed class KeyProcessorProvider : IKeyProcessorProvider
    {
        [Import]
        private IVimFactoryService _vimFactory = null;

        public KeyProcessorProvider() { }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            VsVimBuffer buffer;
            if (wpfTextView.TryGetVimBuffer(out buffer))
            {
                return _vimFactory.CreateKeyProcessor(buffer.VimBuffer);
            }

            return null;
        }
    }
}
