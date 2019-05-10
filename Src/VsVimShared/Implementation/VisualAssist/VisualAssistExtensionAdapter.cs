using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio.Implementation.VisualAssist
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class VisualAssistExtensionAdapter : VimExtensionAdapter
    {
        private IVisualAssistUtil _visualAssistUtil;

        [ImportingConstructor]
        internal VisualAssistExtensionAdapter(IVisualAssistUtil visualAssistUtil)
        {
            _visualAssistUtil = visualAssistUtil;
        }

        // Use default caret when VisualAssist is installed. Visual Assist
        // Intellisense is predicated on the insertion cursor being visible.
        protected override bool UseDefaultCaret =>
            IsInstalled;

        private bool IsInstalled => _visualAssistUtil.IsInstalled;
    }
}
