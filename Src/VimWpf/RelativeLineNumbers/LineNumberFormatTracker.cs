using System;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    internal class LineNumberFormatTracker : ILineFormatTracker
    {
        private readonly IWpfTextView _textView;
        private readonly IFormattedLineSource _formattedLineSource;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IClassificationTypeRegistryService _classificationTypeRegistry;

        private TextFormattingRunProperties _formatting;

        private TextFormatter _textFormatter;

        private bool _formatChanged;

        private bool _numbers;

        private bool _relativeNumbers;

        public Brush Background { get; private set; }

        public double NumberWidth { get; private set; }

        public bool Numbers
        {
            get => _numbers;
            private set
            {
                if (_numbers != value)
                {
                    _numbers = value;
                    OnVimNumbersFormatChanged();
                }
            }
        }

        public bool RelativeNumbers
        {
            get => _relativeNumbers;
            private set
            {
                if (_relativeNumbers != value)
                {
                    _relativeNumbers = value;
                    OnVimNumbersFormatChanged();
                }
            }
        }

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

            _textView.Options.OptionChanged += (s, e) => TryUpdateVimOption(e.OptionId);

            UpdateFormat();
        }

        private void TryUpdateVimOption(string optionId)
        {
            var numbersOption = LineNumbersMarginOptions.NumberOptionId;
            var relativeNumbersOption = LineNumbersMarginOptions.RelativeNumberOptionId;

            if (optionId == relativeNumbersOption.Name)
            {
                RelativeNumbers = _textView.Options.GetOptionValue(relativeNumbersOption);
            }

            if (optionId == numbersOption.Name)
            {
                Numbers = _textView.Options.GetOptionValue(numbersOption);
            }
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

        public event EventHandler<EventArgs> VimNumbersFormatChanged;

        public System.Windows.Media.TextFormatting.TextLine MakeTextLine(int lineNumber)
        {
            string text = lineNumber.ToString(CultureInfo.CurrentUICulture.NumberFormat);

            var textSource = new LineNumberTextSource(text, _formatting);
            var format = new TextFormattingParagraphProperties(_formatting);

            return _textFormatter.FormatLine(textSource, 0, 0, format, null);
        }

        protected virtual void OnVimNumbersFormatChanged()
        {
            VimNumbersFormatChanged?.Invoke(this, EventArgs.Empty);
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
