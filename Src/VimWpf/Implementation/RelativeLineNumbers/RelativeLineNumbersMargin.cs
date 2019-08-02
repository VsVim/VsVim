using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

using Task = System.Threading.Tasks.Task;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class RelativeLineNumbersMargin : VerticalCanvasMargin
    {
        private static readonly ICollection<Line> s_empty = new Line[0];

        private readonly IWpfTextView _textView;
        private readonly ILineFormatTracker _formatTracker;
        private readonly IVimLocalSettings _localSettings;
        private readonly IWpfTextViewMargin _marginContainer;
        private readonly IProtectedOperations _protectedOperations;

        private readonly LineNumbersTracker _linesTracker;
        private readonly LineNumberDrawer _lineNumberDrawer;
        private readonly LineNumbersCalculator _lineNumbersCalculator;

        private double _width = double.NaN;
        private double _minWidth = 0.0;
        private double _maxWidth = double.PositiveInfinity;

        // The VsVim line number margin is enabled whenever the native line
        // number margin is enabled.
        public override bool Enabled =>
            _textView.Options.GetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId);

        internal RelativeLineNumbersMargin(
            IWpfTextView textView,
            ILineFormatTracker formatTracker,
            IVimLocalSettings localSettings,
            IWpfTextViewMargin marginContainer,
            IProtectedOperations protectedOperations)
            : base(VimWpfConstants.LineNumbersMarginName)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            _localSettings = localSettings
                ?? throw new ArgumentNullException(nameof(localSettings));

            _marginContainer = marginContainer
                ?? throw new ArgumentNullException(nameof(marginContainer));

            _protectedOperations = protectedOperations
                ?? throw new ArgumentNullException(nameof(protectedOperations));

            _lineNumbersCalculator = new LineNumbersCalculator(_textView, _localSettings);

            _lineNumberDrawer = new LineNumberDrawer(Canvas, _formatTracker);

            _linesTracker = new LineNumbersTracker(_textView);

            _linesTracker.LineNumbersChanged += (x, y) => RedrawLines();
            _localSettings.SettingChanged += (s, e) => UpdateVimNumberSettings(e);
            _textView.Options.OptionChanged += (s, e) => OnEditorOptionsChanged(e);

            SetVisualStudioMarginVisibility(Visibility.Hidden);
            if (_textView.VisualElement.IsLoaded)
            {
                RedrawLines();
            }
        }

        private void SetVisualStudioMarginVisibility(Visibility visibility)
        {
            var visualStudioMargin =
                _marginContainer.GetTextViewMargin(PredefinedMarginNames.LineNumber);

            if (visualStudioMargin is IWpfTextViewMargin lineNumberMargin)
            {
                var element = lineNumberMargin.VisualElement;
                if (element.Visibility != visibility)
                {
                    if (element.Visibility == Visibility.Visible)
                    {
                        _width = element.Width;
                        _minWidth = element.MinWidth;
                        _maxWidth = element.MaxWidth;
                        element.Width = 0.0;
                        element.MinWidth = 0.0;
                        element.MaxWidth = 0.0;
                    }
                    else if (element.Visibility == Visibility.Hidden)
                    {
                        element.Width = _width;
                        element.MinWidth = _minWidth;
                        element.MaxWidth = _maxWidth;
                    }
                    element.Visibility = visibility;
                    element.UpdateLayout();
                }
            }
        }

        private void RedrawLines()
        {
            UpdateLines(GetNewLineNumbers());
        }

        private void UpdateVimNumberSettings(SettingEventArgs eventArgs)
        {
            SetVisualStudioMarginVisibility(Visibility.Hidden);
            if (_localSettings.Number || _localSettings.RelativeNumber)
            {
                RedrawLines();
            }
        }

        private void OnEditorOptionsChanged(EditorOptionChangedEventArgs eventArgs)
        {
            SetVisualStudioMarginVisibility(Visibility.Hidden);
            if (Enabled)
            {
                RedrawLines();
            }
        }

        private ICollection<Line> GetNewLineNumbers()
        {
            // Avoid hard crashing when async as it will bring down all of
            // Visual Studio.
            try
            {
                Canvas.Width = _formatTracker.NumberWidth * _linesTracker.LinesCountWidthChars;
                return _lineNumbersCalculator.CalculateLineNumbers();
            }
            catch (Exception ex)
            {
                var message = $"Unable to get new line numbers: {ex.Message}";
                var exception = new Exception(message, ex);
                _protectedOperations.Report(exception);
                return s_empty;
            }
        }

        private void UpdateLines(ICollection<Line> newLineNumbers)
        {
            _lineNumberDrawer.UpdateLines(newLineNumbers);
        }
    }
}
