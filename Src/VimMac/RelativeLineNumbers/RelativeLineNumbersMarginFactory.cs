using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Cocoa.Implementation.RelativeLineNumbers
{
    [Name(RelativeLineNumbersMarginFactory.LineNumbersMarginName)]
    [Export(typeof(ICocoaTextViewMarginProvider))]
    [Order(Before = PredefinedMarginNames.Spacer)]
    [MarginContainer(PredefinedMarginNames.LeftSelection)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class RelativeLineNumbersMarginFactory : ICocoaTextViewMarginProvider
    {
        private readonly ICocoaClassificationFormatMapService _formatMapService;
        private readonly IClassificationTypeRegistryService _typeRegistryService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVim _vim;

        public const string LineNumbersMarginName = "vsvim_linenumbers2";

        [ImportingConstructor]
        internal RelativeLineNumbersMarginFactory(
            ICocoaClassificationFormatMapService formatMapService,
            IClassificationTypeRegistryService typeRegistryService,
            IVim vim)
        {
            _formatMapService = formatMapService
                ?? throw new ArgumentNullException(nameof(formatMapService));

            _typeRegistryService = typeRegistryService
                ?? throw new ArgumentNullException(nameof(typeRegistryService));

            _vim = vim
                ?? throw new ArgumentNullException(nameof(vim));
        }

        public ICocoaTextViewMargin CreateMargin(
            ICocoaTextViewHost wpfTextViewHost,
            ICocoaTextViewMargin marginContainer)
        {
            var textView = wpfTextViewHost?.TextView
                ?? throw new ArgumentNullException(nameof(wpfTextViewHost));

            var vimBuffer = _vim.GetOrCreateVimBuffer(textView);
                        
            var formatMap = _formatMapService.GetClassificationFormatMap(textView);

            return new RelativeLineNumbersMargin(
                textView,
                marginContainer,
                formatMap,
                _typeRegistryService,
                vimBuffer.LocalSettings);
        }
    }
}
