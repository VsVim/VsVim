﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

// Disambiguate WPF TextLine with Vim.TextLine
using WpfTextLine = System.Windows.Media.TextFormatting.TextLine;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal class LineNumberVisual : UIElement
    {
        private readonly WpfTextLine _textLine;
        private readonly bool _isRelative;
        private readonly List<Point> _renderTargets;

        internal LineNumberVisual(WpfTextLine textLine, bool isRelative)
        {
            _textLine = textLine
                ?? throw new ArgumentNullException(nameof(textLine));
            _isRelative = isRelative;
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
            var verticalOffset = line.Baseline - _textLine.TextBaseline;

            var horizontalOffset = line.IsCaretLine || !_isRelative
                                       ? 0
                                       : width - _textLine.Width;

            var point = new Point(horizontalOffset, verticalOffset);

            _renderTargets.Add(point);
        }
    }
}
