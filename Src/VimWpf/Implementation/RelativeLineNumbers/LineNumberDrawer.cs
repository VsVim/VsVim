using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class LineNumberDrawer
    {
        private readonly Canvas _canvas;
        private readonly ILineFormatTracker _formatTracker;
        private readonly LineNumberVisualStore _store;

        internal LineNumberDrawer(Canvas canvas, ILineFormatTracker formatTracker, bool isRelative)
        {
            _canvas = canvas
                ?? throw new ArgumentNullException(nameof(canvas));

            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            _store = new LineNumberVisualStore(formatTracker, isRelative);
        }

        public void UpdateLines(IEnumerable<Line> lines)
        {
            ProcessReformatRequests();

            _canvas.Children.Clear();

            double width = _canvas.Width;

            // Group by line number & caret line
            foreach (var line in lines.GroupBy(x => x))
            {
                var visual = _store[line.Key];
                visual.ReplaceRenderTargets(line, width);
                _canvas.Children.Add(visual);
            }
        }

        private void ProcessReformatRequests()
        {
            if (!_formatTracker.TryClearReformatRequest())
            {
                return;
            }

            _canvas.Background = _formatTracker.Background;
            _canvas.Margin = new Thickness(_formatTracker.NumberWidth, 0, 0, 0);

            _store.Clear();
        }
    }
}
