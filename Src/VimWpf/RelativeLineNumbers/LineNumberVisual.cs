using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public class LineNumberVisual : UIElement
    {
        private readonly System.Windows.Media.TextFormatting.TextLine _textLine;

        private readonly List<Point> _renderTargets;

        public LineNumberVisual(System.Windows.Media.TextFormatting.TextLine textLine)
        {
            _textLine = textLine
                ?? throw new ArgumentNullException(nameof(textLine));

            _renderTargets = new List<Point>();

            IsHitTestVisible = false;
        }

        public void ReplaceRenderTargets(IEnumerable<Line> newTargets, double width)
        {
            _renderTargets.Clear();
            
            foreach (var newTarget in newTargets)
            {
                AddRenderTarget(newTarget, width);
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            foreach (var renderTarget in _renderTargets)
            {
                _textLine.Draw(drawingContext, renderTarget, InvertAxes.None);
            }
        }

        private void AddRenderTarget(Line line, double width)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            var verticalOffset = line.Baseline - _textLine.TextBaseline;

            var horizontalOffset = line.IsCaretLine
                                       ? 0
                                       : width - _textLine.Width;

            var point = new Point(horizontalOffset, verticalOffset);

            _renderTargets.Add(point);
        }
    }
}
