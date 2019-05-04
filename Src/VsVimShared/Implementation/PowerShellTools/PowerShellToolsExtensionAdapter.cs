using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.PowerShellTools
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class PowerShellToolsExtensionAdapter : IExtensionAdapter
    {
        //https://github.com/adamdriscoll/poshtools/blob/dev/ReplWindow/Repl/ReplConstants.cs
        private const string ReplContentTypeName = "PowerShellREPLCode";
        private readonly IPowerShellToolsUtil _powerShellToolsUtil;

        [ImportingConstructor]
        internal PowerShellToolsExtensionAdapter(IPowerShellToolsUtil powerShellToolsUtil)
        {
            _powerShellToolsUtil = powerShellToolsUtil;
        }

        internal bool? ShouldCreateVimBuffer(ITextView textView)
        {
            if (!_powerShellToolsUtil.IsInstalled)
                return null;

            var contentTypeDisplayName = textView.TextDataModel.DocumentBuffer.ContentType.DisplayName;
            if (contentTypeDisplayName == ReplContentTypeName)
                return false;

            return null;
        }

        #region IExtensionAdapter

        bool? IExtensionAdapter.IsUndoRedoExpected
        {
            get { return null; }
        }

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            return null;
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            return ShouldCreateVimBuffer(textView);
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.UseDefaultCaret
        {
            get { return null; }
        }

        #endregion
    }
}
