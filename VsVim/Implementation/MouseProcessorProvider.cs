using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(IMouseProcessorProvider))]
    [ContentType(Constants.ContentType)]
    [Name("VsVimMouseProcessorProvider")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class MouseProcessorProvider : IMouseProcessorProvider
    {
        private readonly IVim _vim;

        [ImportingConstructor]
        public MouseProcessorProvider(IVim vim)
        {
            _vim = vim;
        }

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var buffer = _vim.GetOrCreateBuffer(wpfTextView);
            return new Vim.UI.Wpf.MouseProcessor(buffer);
        }
    }
}
