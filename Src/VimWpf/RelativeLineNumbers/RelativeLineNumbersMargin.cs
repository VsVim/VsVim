using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

using Vim.UI.Wpf.RelativeLineNumbers.Util;

using Task = System.Threading.Tasks.Task;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    internal sealed class RelativeLineNumbersMargin : VerticalCanvasMargin
    {
        private readonly IWpfTextView _textView;

        private readonly ILineFormatTracker _formatTracker;

        private readonly LineNumbersTracker _linesTracker;

        private readonly LineNumberDrawer _lineNumberDrawer;

        private readonly SafeRefreshLock _refreshLock;

        private readonly LineNumbersCalculator _lineNumbersCalculator;

        public override bool Enabled =>
            _textView.Properties.GetProperty<bool>(LineNumbersMarginOptions.LineNumbersMarginOptionName);

        public RelativeLineNumbersMargin(IWpfTextView textView, ILineFormatTracker formatTracker)
            : base(LineNumbersMarginOptions.LineNumbersMarginOptionName)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            _lineNumbersCalculator = new LineNumbersCalculator(_textView, formatTracker);

            _refreshLock = new SafeRefreshLock();

            _lineNumberDrawer = new LineNumberDrawer(Canvas, _formatTracker);

            _linesTracker = new LineNumbersTracker(_textView);

            _linesTracker.LineNumbersChanged += async (x, y) => await RedrawLinesAsync().ConfigureAwait(true);
            _formatTracker.VimNumbersFormatChanged += async (x, y) => await RedrawLinesAsync().ConfigureAwait(true);
        }

        private async Task RedrawLinesAsync()
        {
            await _refreshLock.ExecuteInLockAsync(RedrawLinesInternalAsync)
                .ConfigureAwait(true);

            async Task RedrawLinesInternalAsync()
            {
                Canvas.Width = _formatTracker.NumberWidth * _linesTracker.LinesCountWidthChars;

                var newLineNumbers = _lineNumbersCalculator.CalculateLineNumbers();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _lineNumberDrawer.UpdateLines(newLineNumbers);
            }
        }
    }
}
