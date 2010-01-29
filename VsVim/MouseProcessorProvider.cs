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
        private readonly IVsVimFactoryService _factory;

        [ImportingConstructor]
        public MouseProcessorProvider(IVsVimFactoryService factory)
        {
            _factory = factory;
        }

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var buffer = _factory.GetOrCreateBuffer(wpfTextView);
            return _factory.VimFactoryService.CreateMouseProcessor(buffer);
        }
    }
}
