using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace VsVim
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = "VisualStudioKeyProcessor")]
    [Name("VsVim")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType("text")]
    public class KeyProcessorProvider : IKeyProcessorProvider
    {
        public Microsoft.VisualStudio.Text.Editor.KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            VsVimBuffer buffer;
            if (wpfTextView.TryGetVimBuffer(out buffer))
            {
                return new Vim.Components.KeyProcessor(buffer.VimBuffer);
            }

            return null;
        }
    }


}
