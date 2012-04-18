using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim.Implementation.VisualAssist
{
    // TODO: Clean up the native method code.  Need to use HandleRef and make sure I have IntPtr vs. int correct
    // TODO: Need to coordinate the key handling with IVimBufferCoordinator
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Order(Before = Constants.VisualStudioKeyProcessorName, After = Constants.VsKeyProcessorName)]
    [Export(typeof(IKeyProcessorProvider))]
    [Export(typeof(IVisualAssistUtil))]
    [Name("VisualAssistKeyProcessor")]
    internal sealed class VisualAssistKeyProcessorProvider : IKeyProcessorProvider, IVisualAssistUtil
    {
        private static readonly Guid VisualAssistPackageId = new Guid("{44630d46-96b5-488c-8df9-26e21db8c1a3}");

        private readonly IVim _vim;
        private readonly bool _isVisualAssistInstalled;

        [ImportingConstructor]
        internal VisualAssistKeyProcessorProvider(
            SVsServiceProvider serviceProvider,
            IVim vim)
        {
            _vim = vim;

            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isVisualAssistInstalled = vsShell.IsPackageInstalled(VisualAssistPackageId);
        }

        bool IVisualAssistUtil.IsInstalled
        {
            get { return _isVisualAssistInstalled; }
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (!_isVisualAssistInstalled)
            {
                return null;
            }

            var vimBuffer = _vim.GetOrCreateVimBuffer(wpfTextView);
            return new VisualAssistKeyProcessor(vimBuffer);
        }
    }
}
