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

        // Use default caret when VisualAssist is installed.
        protected override bool UseDefaultCaret =>
            IsInstalled;

        private bool IsInstalled => _visualAssistUtil.IsInstalled;
    }
}
