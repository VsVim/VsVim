using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(VimWpfConstants.LineNumbersMarginName)]
    [MarginContainer(PredefinedMarginNames.LeftSelection)]
    [Order(Before = PredefinedMarginNames.Spacer, After = PredefinedMarginNames.LineNumber)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
    [DeferCreation(OptionName = DefaultTextViewHostOptions.LineNumberMarginName)]
    internal sealed class RelativeLineNumbersMarginFactory : IWpfTextViewMarginProvider
    {
        private readonly IClassificationFormatMapService _formatMapService;
        private readonly IClassificationTypeRegistryService _typeRegistryService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVim _vim;

        [ImportingConstructor]
        internal RelativeLineNumbersMarginFactory(
            IClassificationFormatMapService formatMapService,
            IClassificationTypeRegistryService typeRegistryService,
            IProtectedOperations protectedOperations,
            IVim vim)
        {
            _formatMapService = formatMapService
                ?? throw new ArgumentNullException(nameof(formatMapService));

            _typeRegistryService = typeRegistryService
                ?? throw new ArgumentNullException(nameof(typeRegistryService));

            _protectedOperations = protectedOperations
                ?? throw new ArgumentNullException(nameof(protectedOperations));

            _vim = vim
                ?? throw new ArgumentNullException(nameof(vim));
        }

        public IWpfTextViewMargin CreateMargin(
            IWpfTextViewHost wpfTextViewHost,
            IWpfTextViewMargin marginContainer)
        {
            var textView = wpfTextViewHost?.TextView
                ?? throw new ArgumentNullException(nameof(wpfTextViewHost));

            var vimBuffer = _vim.GetOrCreateVimBuffer(textView);
                        
            var formatMap = _formatMapService.GetClassificationFormatMap(textView);

            var formatProvider = new LineNumberFormatTracker(
                textView,
                formatMap,
                _typeRegistryService);

            return new RelativeLineNumbersMargin(
                textView,
                formatProvider,
                vimBuffer.LocalSettings,
                marginContainer,
                _protectedOperations);
        }
    }
}
