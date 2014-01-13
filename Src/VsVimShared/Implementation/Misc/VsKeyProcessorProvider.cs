﻿using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = Constants.FallbackKeyProcessorName)]
    [Order(Before = Constants.VisualStudioKeyProcessorName)]
    [Name(Constants.VsKeyProcessorName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [ContentType(Vim.Constants.ContentType)]
    internal sealed class VsKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly FallbackKeyProcessorProvider _fallbackKeyProcessorProvider;
        private readonly IVimBufferCoordinatorFactory _bufferCoordinatorFactory;
        private readonly IVsAdapter _adapter;
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;
        private readonly IReportDesignerUtil _reportDesignerUtil;

        [ImportingConstructor]
        internal VsKeyProcessorProvider(IVim vim, IVsAdapter adapter, IVimBufferCoordinatorFactory bufferCoordinatorFactory, IKeyUtil keyUtil, IReportDesignerUtil reportDesignerUtil, FallbackKeyProcessorProvider fallbackKeyProcessorProvider)
        {
            _vim = vim;
            _adapter = adapter;
            _bufferCoordinatorFactory = bufferCoordinatorFactory;
            _keyUtil = keyUtil;
            _reportDesignerUtil = reportDesignerUtil;
            _fallbackKeyProcessorProvider = fallbackKeyProcessorProvider;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            var fallbackKeyProcessor = _fallbackKeyProcessorProvider.GetOrCreateFallbackProcessor(wpfTextView);
            var vimBufferCoordinator = _bufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            return new VsKeyProcessor(fallbackKeyProcessor, _adapter, vimBufferCoordinator, _keyUtil, _reportDesignerUtil, wpfTextView);
        }
    }
}
