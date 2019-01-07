using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public class LineNumberDrawer
    {
        private readonly Canvas _canvas;

        private readonly ILineFormatTracker _formatTracker;
        private readonly LineNumberVisualStore _store;

        public LineNumberDrawer(Canvas canvas, ILineFormatTracker formatTracker)
        {
            _canvas = canvas
                ?? throw new ArgumentNullException(nameof(canvas));

            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            _store = new LineNumberVisualStore(formatTracker);
        }

        public void UpdateLines(IEnumerable<Line> lines)
        {
            ProcessReformatRequests();

            _canvas.Children.Clear();

            double width = _canvas.Width;
            foreach (var numberTargets in lines.GroupBy(x => x.Number))
            {
                var visual = _store[numberTargets.Key];
            
                visual.ReplaceRenderTargets(numberTargets, width);

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