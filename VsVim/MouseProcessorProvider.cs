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
        private IVsVimFactoryService _factory = null;

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            VsVimBuffer buffer = _factory.GetOrCreateBuffer(wpfTextView);
            return _factory.VimFactoryService.CreateMouseProcessor(buffer.VimBuffer);
        }
    }
}
