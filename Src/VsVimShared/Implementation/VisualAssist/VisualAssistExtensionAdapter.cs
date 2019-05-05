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

        private bool IsInstalled => _visualAssistUtil.IsInstalled;

        private bool IsSelectionCommand(string command, string argument)
        {
            if (!_visualAssistUtil.IsInstalled)
            {
                return false;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(command, "VAssistX.SmartSelectExtend"))
            {
                return true;
            }

            return false;
        }

        protected override bool ShouldKeepSelectionAfterHostCommand(string command, string argument) =>
            IsSelectionCommand(command, argument);

        protected override bool UseDefaultCaret => IsInstalled;
    }
}
