using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace VsVim.Implementation.Misc
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = Constants.VisualStudioKeyProcessorName)]
    [Name(Constants.VsKeyProcessorName)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [ContentType(Vim.Constants.AnyContentType)]
    internal sealed class VsKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVimBufferCoordinatorFactory _bufferCoordinatorFactory;
        private readonly IVsAdapter _adapter;
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;
        private readonly IReportDesignerUtil _reportDesignerUtil;
        private readonly _DTE _dte;

        [ImportingConstructor]
        internal VsKeyProcessorProvider(IVim vim, IVsAdapter adapter, IVimBufferCoordinatorFactory bufferCoordinatorFactory, IKeyUtil keyUtil, IReportDesignerUtil reportDesignerUtil, SVsServiceProvider serviceProvider)
        {
            _vim = vim;
            _adapter = adapter;
            _bufferCoordinatorFactory = bufferCoordinatorFactory;
            _keyUtil = keyUtil;
            _reportDesignerUtil = reportDesignerUtil;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return new ForwardingKeyProcessor(_dte, _keyUtil, wpfTextView);
            }

            var vimBufferCoordinator = _bufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            return new VsKeyProcessor(_adapter, vimBufferCoordinator, _keyUtil, _reportDesignerUtil, wpfTextView);
        }
    }
}
