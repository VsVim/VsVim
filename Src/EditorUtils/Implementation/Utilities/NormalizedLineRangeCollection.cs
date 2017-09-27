using System.Collections.Generic;
using System.Linq;

namespace EditorUtils.Implementation.Utilities
{
    /// <summary>
    /// The goal of this collection is to efficiently track the set of LineRange values that have 
    /// been visited for a given larger LineRange.  The order in which, or original granualarity
    /// of visits is less important than the overall range which is visited.  
    /// 
    /// For example if both ranges 1-3 and 2-5 are visited then the collection will only record
    /// that 1-5 is visited. 
    /// </summary>
    internal sealed class NormalizedLineRangeCollection : IEnumerable<LineRange>
    {
        private readonly List<LineRange> _list = new List<LineRange>();

        internal LineRange? OverarchingLineRange
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

        internal int Count
        {
            get { return _list.Count; }
        }

        internal LineRange this[int index]
        {
            get { return _list[index]; }
        }

        internal NormalizedLineRangeCollection()
        {

        }

        internal NormalizedLineRangeCollection(IEnumerable<LineRange> visited)
        {
            foreach (var lineRange in visited)
            {
                Add(lineRange);
            }
        }

        internal void Add(LineRange lineRange)
        {
            var index = FindInsertionPoint(lineRange.StartLineNumber);
            if (index == -1)
            {
                // Just insert at the end and let the collapse code do the work in this case 
                _list.Add(lineRange);
                CollapseIntersecting(_list.Count - 1);
            }
            else
            {
                // Quick optimization check to avoid copying the contents of the List
                // structure down on insert
                var item = _list[index];
                if (item.StartLineNumber == lineRange.StartLineNumber || lineRange.ContainsLineNumber(item.StartLineNumber))
                {
                    _list[index] = LineRange.CreateOverarching(item, lineRange);
                }
                else
                {
                    _list.Insert(index, lineRange);
                }

                CollapseIntersecting(index);
            }
        }

        internal bool Contains(LineRange lineRange)
        {
            return _list.Any(current => current.Contains(lineRange));
        }

        internal void Clear()
        {
            _list.Clear();
        }

        internal NormalizedLineRangeCollection Copy()
        {
            return new NormalizedLineRangeCollection(_list);
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

        /// <summary>
        /// This is the helper method for Add which will now collapse elements that intersect.   We only have
        /// to look at the item before the insert and all items after and not the entire collection.
        /// </summary>
        private void CollapseIntersecting(int index)
        {
            // It's possible this new LineRange actually intersects with the LineRange before
            // the insertion point.  LineRange values are ordered by start line.  Hence the LineRange
            // before could have an extent which intersects the new LineRange but not the previous 
            // LineRange at this index.  Do a quick check for this and if it's true just start the
            // collapse one index backwards
            var lineRange = _list[index];
            if (index > 0 && _list[index - 1].Intersects(lineRange))
            {
                CollapseIntersecting(index - 1);
                return;
            }

            var removeCount = 0;
            var current = index + 1;
            while (current < _list.Count)
            {
                var currentLineRange = _list[current];
                if (!lineRange.Intersects(currentLineRange))
                {
                    break;
                }

                lineRange = LineRange.CreateOverarching(lineRange, currentLineRange);
                _list[index] = lineRange;
                removeCount++;
                current++;
            }

            if (removeCount > 0)
            {
                _list.RemoveRange(index + 1, removeCount);
            }
        }

        internal int FindInsertionPoint(int startLineNumber)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (startLineNumber <= _list[i].StartLineNumber)
                {
                    return i;
                }
            }

            return -1;
        }

        #region IEnumerable<LineRange>

        IEnumerator<LineRange> IEnumerable<LineRange>.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        #endregion
    }
}
