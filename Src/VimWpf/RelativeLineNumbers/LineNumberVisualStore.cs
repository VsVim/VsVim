using System;
using System.Collections.Generic;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public class LineNumberVisualStore
    {
        private readonly Dictionary<int, LineNumberVisual> _cache;

        private readonly ILineFormatTracker _formatTracker;

        public LineNumberVisualStore(ILineFormatTracker formatTracker)
        {
            _formatTracker = formatTracker
                ?? throw new ArgumentNullException(nameof(formatTracker));

            _cache = new Dictionary<int, LineNumberVisual>();
        }

        public LineNumberVisual this[int lineNumber]
        {
            get
            {
                if (_cache.TryGetValue(lineNumber, out var visual))
                {
                    return visual;
                }

                var line = _formatTracker.MakeTextLine(lineNumber);
                visual = new LineNumberVisual(line);
                _cache[lineNumber] = visual;

                return visual;
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}