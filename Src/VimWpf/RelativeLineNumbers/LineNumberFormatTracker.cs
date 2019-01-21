using System;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

using WpfTextLine = System.Windows.Media.TextFormatting.TextLine;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    internal class LineNumberFormatTracker : ILineFormatTracker
    {
        private readonly IFormattedLineSource _formattedLineSource;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IClassificationTypeRegistryService _classificationTypeRegistry;
        private TextFormattingRunProperties _formatting;
        private TextFormatter _textFormatter;
        private bool _formatChanged;
        public Brush Background { get; private set; }

        public double NumberWidth { get; private set; }

        public LineNumberFormatTracker(
            IWpfTextView textView,
            IClassificationFormatMap classificationFormatMap,
            IClassificationTypeRegistryService classificationTypeRegistry)
        {
            textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _classificationFormatMap = classificationFormatMap
                ?? throw new ArgumentNullException(nameof(classificationFormatMap));

            _classificationTypeRegistry = classificationTypeRegistry
                ?? throw new ArgumentNullException(nameof(classificationTypeRegistry));

            _formattedLineSource = textView.FormattedLineSource;

            _classificationFormatMap.ClassificationFormatMappingChanged += (s, e) => UpdateFormat();

            UpdateFormat();
        }

        public bool TryClearReformatRequest()
        {
            if (_formatChanged)
            {
                _formatChanged = false;
                return true;
            }

            return false;
        }

        public WpfTextLine MakeTextLine(int lineNumber)
        {
            string text = lineNumber.ToString(CultureInfo.CurrentUICulture.NumberFormat);

            var textSource = new LineNumberTextSource(text, _formatting);
            var format = new TextFormattingParagraphProperties(_formatting);

            return _textFormatter.FormatLine(textSource, 0, 0, format, null);
        }

        private void UpdateFormat()
        {
            var lineNumberType = _classificationTypeRegistry.GetClassificationType("line number");
            _formatting = _classificationFormatMap.GetTextProperties(lineNumberType);

            Background = _formatting.BackgroundBrush;

            _textFormatter = _formattedLineSource.UseDisplayMode
                                 ? TextFormatter.Create(TextFormattingMode.Display)
                                 : TextFormatter.Create(TextFormattingMode.Ideal);

            NumberWidth = Enumerable.Range(0, 10).Max(x => MakeTextLine(x).Width);

            _formatChanged = true;
        }
    }
}
