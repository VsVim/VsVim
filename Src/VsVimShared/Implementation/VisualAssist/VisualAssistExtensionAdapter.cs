using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio.Implementation.VisualAssist
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class VisualAssistExtensionAdapter : IExtensionAdapter
    {
        private IVisualAssistUtil _visualAssistUtil;

        [ImportingConstructor]
        internal VisualAssistExtensionAdapter(IVisualAssistUtil visualAssistUtil)
        {
            _visualAssistUtil = visualAssistUtil;
        }

        #region IExtensionAdapter

        bool? IExtensionAdapter.IsUndoRedoExpected
        {
            get { return null; }
        }

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            if (!_visualAssistUtil.IsInstalled)
            {
                return null;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(command, "VAssistX.SmartSelectExtend"))
            {
                return true;
            }

            return null;
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.UseDefaultCaret
        {
            get
            {
                if (!_visualAssistUtil.IsInstalled)
                {
                    return null;
                }

                // Visual Assist Intellisense is predicated on the insertion cursor being visible.
                return true;
            }
        }

        #endregion
    }
}
