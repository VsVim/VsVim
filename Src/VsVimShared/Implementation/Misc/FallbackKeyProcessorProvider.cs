using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
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
        private readonly IKeyUtil _keyUtil;
        private readonly _DTE _dte;
        private readonly IVimApplicationSettings _vimApplicationSettings;

        private KeyProcessor _keyProcessor;

        [ImportingConstructor]
        internal FallbackKeyProcessorProvider(SVsServiceProvider serviceProvider, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings)
        {
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _keyProcessor = null;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (_keyProcessor == null)
            {
                _keyProcessor = new FallbackKeyProcessor(_dte, _keyUtil, _vimApplicationSettings);
            }
            return _keyProcessor;
        }
    }
}