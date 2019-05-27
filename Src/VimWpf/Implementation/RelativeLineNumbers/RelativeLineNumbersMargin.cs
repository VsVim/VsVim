using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
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
        private readonly IJoinableTaskFactoryProvider _joinableTaskFactoryProvider;

        public override bool Enabled =>
            _textView.Properties.GetProperty<bool>(LineNumbersMarginOptions.LineNumbersMarginOptionName);

        public RelativeLineNumbersMargin(
            IWpfTextView textView,
            ILineFormatTracker formatTracker,
            IVimLocalSettings localSettings,
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

            _joinableTaskFactoryProvider = joinableTaskFactoryProvider;
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
                await RedrawLinesAsync().ConfigureAwait(true);
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
