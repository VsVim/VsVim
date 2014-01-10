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
    [Order(After = Constants.VsKeyProcessorName)]
    [Name(Constants.FallbackKeyProcessorName)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [ContentType(Vim.Constants.AnyContentType)]
    internal sealed class FallbackKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVsAdapter _adapter;
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;
        private readonly IReportDesignerUtil _reportDesignerUtil;
        private readonly _DTE _dte;
        private readonly IKeyBindingService _keyBindingService;

        [ImportingConstructor]
        internal FallbackKeyProcessorProvider(IVim vim, IVsAdapter adapter, IKeyUtil keyUtil, SVsServiceProvider serviceProvider, IKeyBindingService keyBindingService)
        {
            _vim = vim;
            _adapter = adapter;
            _keyUtil = keyUtil;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _keyBindingService = keyBindingService;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return new FallbackKeyProcessor(_dte, _keyUtil, _keyBindingService, wpfTextView);
        }
    }
}