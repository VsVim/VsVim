using System.Collections.Generic;
using System.Linq;

namespace EditorUtils.Implementation.Utilities
{
    internal sealed class LineRangeVisited
    {
        private readonly List<LineRange> _list = new List<LineRange>();

        internal LineRange? LineRange
        {
            get
            {
                if (_list.Count == 0)
                {
                    return null;
                }

                if (_list.Count == 1)
                {
                    return _list[0];
                }

                var startLine = _list[0].StartLineNumber;
                var lastLine = _list[_list.Count - 1].LastLineNumber;
                return EditorUtils.LineRange.CreateFromBounds(startLine, lastLine);
            }
        }

        internal List<LineRange> List
        {
            get { return _list; }
        }

        internal LineRangeVisited()
        {

        }

        internal LineRangeVisited(IEnumerable<LineRange> visited)
        {
            _list.AddRange(visited);
            OrganizeLineRanges();
        }

        internal void Add(LineRange lineRange)
        {
            _list.Add(lineRange);
            OrganizeLineRanges();
        }

        internal bool Contains(LineRange lineRange)
        {
            return _list.Any(current => current.Contains(lineRange));
        }

        internal void Clear()
        {
            _list.Clear();
        }

        internal LineRange? GetUnvisited(LineRange lineRange)
        {
            foreach (var current in _list)
            {
                if (!current.Intersects(lineRange))
                {
                    continue;
                }

                // Already have a LineRange which completely contains the provided 
                // value.  No unvisited values
                if (current.Contains(lineRange))
                {
                    return null;
                }

                // The found range starts before and intersects.  The unvisited section 
                // is the range below
                if (current.StartLineNumber <= lineRange.StartLineNumber)
                {
                    return EditorUtils.LineRange.CreateFromBounds(current.LastLineNumber + 1, lineRange.LastLineNumber);
                }

                // The found range starts below and intersects.  The unvisited section 
                // is the line range above
                if (current.StartLineNumber > lineRange.StartLineNumber)
                {
                    return EditorUtils.LineRange.CreateFromBounds(lineRange.StartLineNumber, current.StartLineNumber - 1);
                }
            }

            return lineRange;
        }

        private void OrganizeLineRanges()
        {
            _list.Sort((x, y) => x.StartLineNumber.CompareTo(y.StartLineNumber));

            // Now collapse any LineRange which intersects with the given value
            var i = 0;
            while (i + 1 < _list.Count)
            {
                var current = _list[i];
                var next = _list[i + 1];
                if (current.Intersects(next))
                {
                    _list[i] = EditorUtils.LineRange.CreateOverarching(current, next);
                    _list.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
