using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.PowerShellTools
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class PowerShellToolsExtensionAdapter : VimExtensionAdapter
    {
        //https://github.com/adamdriscoll/poshtools/blob/dev/ReplWindow/Repl/ReplConstants.cs
        private const string ReplContentTypeName = "PowerShellREPLCode";
        private readonly IPowerShellToolsUtil _powerShellToolsUtil;

        [ImportingConstructor]
        internal PowerShellToolsExtensionAdapter(IPowerShellToolsUtil powerShellToolsUtil)
        {
            _powerShellToolsUtil = powerShellToolsUtil;
        }

        private bool IsInteractive(ITextView textView)
        {
            if (!_powerShellToolsUtil.IsInstalled)
                return false;

            var contentTypeDisplayName = textView.TextDataModel.DocumentBuffer.ContentType.DisplayName;
            return contentTypeDisplayName == ReplContentTypeName;
        }

        protected override bool ShouldCreateVimBuffer(ITextView textView) => IsInteractive(textView);
    }
}
