using System;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

using Task = System.Threading.Tasks.Task;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class RelativeLineNumbersMargin : VerticalCanvasMargin
    {
        private readonly IWpfTextView _textView;
        private readonly ILineFormatTracker _formatTracker;
        private readonly LineNumbersTracker _linesTracker;
        private readonly LineNumberDrawer _lineNumberDrawer;
        private readonly SafeRefreshLock _refreshLock;
        private readonly LineNumbersCalculator _lineNumbersCalculator;
        private readonly IWpfTextViewMargin _marginContainer;
        private readonly IJoinableTaskFactoryProvider _joinableTaskFactoryProvider;

        public override bool Enabled
        {
            get
            {
                if (_textView.Properties.TryGetProperty(LineNumbersMarginOptions.LineNumbersMarginOptionName, out bool enabled))
                {
                    return enabled;
                }
                return false;
            }
        }

        public RelativeLineNumbersMargin(
            IWpfTextView textView,
            ILineFormatTracker formatTracker,
            IVimLocalSettings localSettings,
            IWpfTextViewMargin marginContainer,
            IJoinableTaskFactoryProvider joinableTaskFactoryProvider)
            : base(LineNumbersMarginOptions.LineNumbersMarginOptionName)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            localSettings = localSettings
                ?? throw new ArgumentNullException(nameof(localSettings));

            _lineNumbersCalculator = new LineNumbersCalculator(_textView, localSettings);

            _refreshLock = new SafeRefreshLock();

            _lineNumberDrawer = new LineNumberDrawer(Canvas, _formatTracker);

            _linesTracker = new LineNumbersTracker(_textView);

            _linesTracker.LineNumbersChanged += async (x, y) => await RedrawLinesAsync().ConfigureAwait(true);
            localSettings.SettingChanged += async (s, e) => await UpdateVimNumberSettings(e).ConfigureAwait(true);
            _textView.Options.OptionChanged += async (s, e) => await OnEditorOptionsChanged(e).ConfigureAwait(true);

            _marginContainer = marginContainer;

            _joinableTaskFactoryProvider = joinableTaskFactoryProvider;

            HideVisualStudioLineNumbers();
        }

        private async Task UpdateVimNumberSettings(SettingEventArgs eventArgs)
        {
            if (!eventArgs.IsValueChanged)
            {
                return;
            }

            var settingName = eventArgs.Setting.Name;
            var isNumberSetting = settingName == LocalSettingNames.NumberName;
            var isRelativeNumberSetting = settingName == LocalSettingNames.RelativeNumberName;
            
            if (isNumberSetting || isRelativeNumberSetting)
            {
                HideVisualStudioLineNumbers();
                await RedrawLinesAsync().ConfigureAwait(true);
            }
        }

        private async Task OnEditorOptionsChanged(EditorOptionChangedEventArgs eventArgs)
        {
            if (Enabled)
            {
                HideVisualStudioLineNumbers();
                await RedrawLinesAsync().ConfigureAwait(true);
            }
        }

        private void HideVisualStudioLineNumbers()
        {
            if (_marginContainer.GetTextViewMargin(PredefinedMarginNames.LineNumber) is IWpfTextViewMargin lineNumberMargin)
            {
                var element = lineNumberMargin.VisualElement;
                element.Visibility = Visibility.Hidden;
                element.Width = 0.0;
                element.MinWidth = 0.0;
                element.MaxWidth = 0.0;
                element.UpdateLayout();
            }
        }

        private async Task RedrawLinesAsync()
        {
            await _refreshLock.ExecuteInLockAsync(RedrawLinesInternalAsync)
                .ConfigureAwait(true);

            async Task RedrawLinesInternalAsync()
            {
                Canvas.Width = _formatTracker.NumberWidth * _linesTracker.LinesCountWidthChars;

                var newLineNumbers = _lineNumbersCalculator.CalculateLineNumbers();

                await _joinableTaskFactoryProvider.JoinableTaskFactory.SwitchToMainThreadAsync();

                _lineNumberDrawer.UpdateLines(newLineNumbers);
            }
        }
    }
}
