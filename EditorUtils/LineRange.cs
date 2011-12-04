using System;
using System.Collections.Generic;
using System.Linq;

namespace EditorUtils
{
    /// <summary>
    /// A simple line range 
    /// </summary>
    public struct LineRange : IEquatable<LineRange>
    {
        private readonly int _startLine;
        private readonly int _count;

        public int StartLineNumber
        {
            get { return _startLine; }
        }

        public int LastLineNumber
        {
            get { return _startLine + (_count - 1); }
        }

        public int Count
        {
            get { return _count; }
        }

        public IEnumerable<int> LineNumbers
        {
            get { return Enumerable.Range(_startLine, _count); }
        }

        public LineRange(int startLine, int count)
        {
            _startLine = startLine;
            _count = count;
        }

        public bool ContainsLineNumber(int lineNumber)
        {
            return lineNumber >= _startLine && lineNumber <= LastLineNumber;
        }

        public bool Contains(LineRange lineRange)
        {
            return
                StartLineNumber <= lineRange.StartLineNumber &&
                LastLineNumber >= lineRange.LastLineNumber;
        }

        public bool Intersects(LineRange lineRange)
        {
            return
                ContainsLineNumber(lineRange.StartLineNumber) ||
                ContainsLineNumber(lineRange.LastLineNumber) ||
                LastLineNumber + 1 == lineRange.StartLineNumber ||
                StartLineNumber == lineRange.LastLineNumber + 1;
        }

        public override int GetHashCode()
        {
            return _startLine ^ _count;
        }

        public override bool Equals(object obj)
        {
            if (obj is LineRange)
            {
                return Equals((LineRange)obj);
            }

            return false;
        }

        public bool Equals(LineRange lineRange)
        {
            return StartLineNumber == lineRange.StartLineNumber && Count == lineRange.Count;
        }

        public override string ToString()
        {
            return String.Format("[{0} - {1}]", StartLineNumber, LastLineNumber);
        }

        public static bool operator ==(LineRange left, LineRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LineRange left, LineRange right)
        {
            return !left.Equals(right);
        }

        public static LineRange CreateFromBounds(int startLineNumber, int lastLineNumber)
        {
            if (lastLineNumber < startLineNumber)
            {
                throw new ArgumentOutOfRangeException("lastLineNumber");
            }

            var count = (lastLineNumber - startLineNumber) + 1;
            return new LineRange(startLineNumber, count);
        }

        public static LineRange CreateOverarching(LineRange left, LineRange right)
        {
            var startLineNumber = Math.Min(left.StartLineNumber, right.StartLineNumber);
            var lastLineNumber = Math.Max(left.LastLineNumber, right.LastLineNumber);
            return CreateFromBounds(startLineNumber, lastLineNumber);
        }
    }
}
