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
        private readonly IKeyboardDevice _keyboardDevice;

        [ImportingConstructor]
        internal VimMouseProcessorProvider(IVim vim, IKeyboardDevice keyboardDevice)
        {
            _vim = vim;
            _keyboardDevice = keyboardDevice;
        }

        #region IMouseProcessorProvider

        IMouseProcessor IMouseProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return new VimMouseProcessor(vimBuffer, _keyboardDevice);
            }

            return null;
        }

        #endregion
    }
}
