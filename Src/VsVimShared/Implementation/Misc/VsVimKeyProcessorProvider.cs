using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = Constants.FallbackKeyProcessorName)]
    [Order(Before = Constants.VisualStudioKeyProcessorName)]
    [Name(VimConstants.MainKeyProcessorName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType(VimConstants.ContentType)]
    internal sealed class VsVimKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVimBufferCoordinatorFactory _bufferCoordinatorFactory;
        private readonly IVsAdapter _adapter;
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;
        private readonly IReportDesignerUtil _reportDesignerUtil;

        [ImportingConstructor]
        internal VsVimKeyProcessorProvider(IVim vim, IVsAdapter adapter, IVimBufferCoordinatorFactory bufferCoordinatorFactory, IKeyUtil keyUtil, IReportDesignerUtil reportDesignerUtil)
        {
            _vim = vim;
            _adapter = adapter;
            _bufferCoordinatorFactory = bufferCoordinatorFactory;
            _keyUtil = keyUtil;
            _reportDesignerUtil = reportDesignerUtil;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            var vimBufferCoordinator = _bufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            return new VsVimKeyProcessor(_adapter, vimBufferCoordinator, _keyUtil, _reportDesignerUtil);
        }
    }
}
