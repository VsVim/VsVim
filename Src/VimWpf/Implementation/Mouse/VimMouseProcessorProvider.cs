using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Mouse
{
    [Export(typeof(IMouseProcessorProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Default Mouse Processor")]
    internal sealed class VimMouseProcessorProvider : IMouseProcessorProvider
    {
        private readonly IVim _vim;

        [ImportingConstructor]
        internal VimMouseProcessorProvider(IVim vim)
        {
            _vim = vim;
        }

        #region IMouseProcessorProvider

        IMouseProcessor IMouseProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return new VimMouseProcessor(vimBuffer);
            }

            return null;
        }

        #endregion
    }
}
