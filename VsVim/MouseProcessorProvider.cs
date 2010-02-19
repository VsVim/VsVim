using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim
{
    [Export(typeof(IMouseProcessorProvider))]
    [ContentType("text")]
    [Name("VsVimMouseProcessorProvider")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public class MouseProcessorProvider : IMouseProcessorProvider
    {
        private readonly IVimFactoryService _vimFactoryService;

        [ImportingConstructor]
        public MouseProcessorProvider(IVimFactoryService vimFactoryService)
        {
            _vimFactoryService = vimFactoryService;
        }

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var buffer = _vimFactoryService.Vim.GetOrCreateBuffer(wpfTextView);
            return _vimFactoryService.CreateMouseProcessor(buffer);
        }
    }
}
