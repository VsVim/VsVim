using System;
using System.Collections.Generic;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class LineNumberVisualStore
    {
        private readonly Dictionary<Line, LineNumberVisual> _cache;

        private readonly ILineFormatTracker _formatTracker;
        private readonly bool _isRelative;

        internal LineNumberVisualStore(ILineFormatTracker formatTracker, bool isRelative)
        {
            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));
            _isRelative = isRelative;
            _cache = new Dictionary<Line, LineNumberVisual>();
        }

        public LineNumberVisual this[Line line]
        {
            get
            {
                if (_cache.TryGetValue(line, out var visual))
                {
                    return visual;
                }

                var textLine = _formatTracker.MakeTextLine(line.Number, line.IsCaretLine);
                visual = new LineNumberVisual(textLine, _isRelative);
                _cache[line] = visual;

                return visual;
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
