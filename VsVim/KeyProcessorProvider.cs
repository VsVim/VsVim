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
    [ContentType(Constants.ContentType)]
    public sealed class KeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVimFactoryService _factory;

        [ImportingConstructor]
        public KeyProcessorProvider(IVimFactoryService factory)
        {
            _factory = factory;
        }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var buffer = _factory.Vim.GetOrCreateBuffer(wpfTextView);
            return _factory.CreateKeyProcessor(buffer);
        }
    }
}
