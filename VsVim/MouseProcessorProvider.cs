using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace VsVim
{
    [Export(typeof(IMouseProcessorProvider))]
    [ContentType("text")]
    [Name("VsVimMouseProcessorProvider")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public class MouseProcessorProvider : IMouseProcessorProvider
    {
        [Import]
        private Vim.IVimFactoryService _factory = null;

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            VsVimBuffer buffer = null;
            if (wpfTextView.TryGetVimBuffer(out buffer))
            {
                return _factory.CreateMouseProcessor(buffer.VimBuffer);
            }

            return null;
        }
    }
}
