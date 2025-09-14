using System;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

using WpfTextLine = System.Windows.Media.TextFormatting.TextLine;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class LineNumberFormatTracker : ILineFormatTracker
    {
        private readonly IFormattedLineSource _formattedLineSource;
        private readonly IWpfTextView _textView;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IClassificationTypeRegistryService _classificationTypeRegistry;
        private TextFormattingRunProperties _formatting;
        private TextFormattingRunProperties _selectedLineNumberFormatting;
        private TextFormatter _textFormatter;
        private bool _formatChanged;
        public Brush Background { get; private set; }
        public double NumberWidth { get; private set; }

        public LineNumberFormatTracker(
            IWpfTextView textView,
            IClassificationFormatMap classificationFormatMap,
            IClassificationTypeRegistryService classificationTypeRegistry)
        {
            _textView = textView
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

        public WpfTextLine MakeTextLine(int lineNumber, bool isCurrentLineNumber)
        {
            // Use '~' for the phantom line, otherwise the line number.
            string text = lineNumber == -1 ? "~" :
                lineNumber.ToString(CultureInfo.CurrentUICulture.NumberFormat);

            var formatting = isCurrentLineNumber ? _selectedLineNumberFormatting : _formatting;
            var textSource = new LineNumberTextSource(text, formatting);
            var format = new TextFormattingParagraphProperties(formatting);

            return _textFormatter.FormatLine(textSource, 0, 0, format, null);
        }

        private void UpdateFormat()
        {
            var lineNumberType = _classificationTypeRegistry.GetClassificationType("line number");
            _formatting = _classificationFormatMap.GetTextProperties(lineNumberType);

            var selectedLineNumberType = _classificationTypeRegistry.GetClassificationType("Selected Line Number");
            _selectedLineNumberFormatting =
                selectedLineNumberType == null ?
                    _formatting :
                    _classificationFormatMap.GetTextProperties(selectedLineNumberType);

            Background = _formatting.BackgroundBrush;

            _textFormatter = _formattedLineSource.UseDisplayMode
                                 ? TextFormatter.Create(TextFormattingMode.Display)
                                 : TextFormatter.Create(TextFormattingMode.Ideal);

            int currentLineNumber = _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber + 1;

            NumberWidth = Enumerable.Range(0, 10).Max(x => MakeTextLine(x, x == currentLineNumber).Width);

            _formatChanged = true;
        }
    }
}
