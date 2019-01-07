using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(LineNumbersMarginOptions.LineNumbersMarginOptionName)]
    [MarginContainer(PredefinedMarginNames.LeftSelection)]
    [Order(Before = PredefinedMarginNames.Spacer, After = PredefinedMarginNames.LineNumber)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
    [DeferCreation(OptionName = LineNumbersMarginOptions.LineNumbersMarginOptionName)]
    public sealed class RelativeLineNumbersMarginFactory : IWpfTextViewMarginProvider
    {
        private readonly IClassificationFormatMapService _formatMapService;
        private readonly IClassificationTypeRegistryService _typeRegistryService;

        [ImportingConstructor]
        public RelativeLineNumbersMarginFactory(
            IClassificationFormatMapService formatMapService,
            IClassificationTypeRegistryService typeRegistryService)
        {
            _formatMapService = formatMapService
                ?? throw new ArgumentNullException(nameof(formatMapService));

            _typeRegistryService = typeRegistryService
                ?? throw new ArgumentNullException(nameof(formatMapService));
        }

        public IWpfTextViewMargin CreateMargin(
            IWpfTextViewHost wpfTextViewHost,
            IWpfTextViewMargin marginContainer)
        {
            var textView = wpfTextViewHost?.TextView
                ?? throw new ArgumentNullException(nameof(wpfTextViewHost));

            var formatMap = _formatMapService.GetClassificationFormatMap(textView);

            var formatProvider = new LineNumberFormatTracker(
                textView,
                formatMap,
                _typeRegistryService);

            return new RelativeLineNumbersMargin(textView, formatProvider);
        }
    }
}