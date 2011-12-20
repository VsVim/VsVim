using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace EditorUtils
{
    /// <summary>
    /// Represents a range of lines in an ITextSnapshot.  Different from a SnapshotSpan
    /// because it declaratively supports lines instead of a position range
    /// </summary>
    public struct SnapshotLineRange : IEquatable<SnapshotLineRange>
    {
        private readonly ITextSnapshot _snapshot;
        private readonly int _startLine;
        private readonly int _count;

        public ITextSnapshot Snapshot
        {
            get { return _snapshot; }
        }

        public int StartLineNumber
        {
            get { return _startLine; }
        }

        public ITextSnapshotLine StartLine
        {
            get { return _snapshot.GetLineFromLineNumber(StartLineNumber); }
        }

        public SnapshotPoint Start
        {
            get { return StartLine.Start; }
        }

        public int Count
        {
            get { return _count; }
        }

        public int LastLineNumber
        {
            get { return _startLine + (_count - 1); }
        }

        public ITextSnapshotLine LastLine
        {
            get { return _snapshot.GetLineFromLineNumber(LastLineNumber); }
        }

        public LineRange LineRange
        {
            get { return new LineRange(_startLine, _count); }
        }

        public SnapshotPoint End
        {
            get { return LastLine.End; }
        }

        public SnapshotPoint EndIncludingLineBreak
        {
            get { return LastLine.EndIncludingLineBreak; }
        }

        public SnapshotSpan Extent
        {
            get { return new SnapshotSpan(Start, End); }
        }

        public SnapshotSpan ExtentIncludingLineBreak
        {
            get { return new SnapshotSpan(Start, EndIncludingLineBreak); }
        }

        public IEnumerable<ITextSnapshotLine> Lines
        {
            get { return Enumerable.Range(StartLineNumber, Count).Select(_snapshot.GetLineFromLineNumber); }
        }

        public SnapshotLineRange(ITextSnapshot snapshot, int startLine, int count)
        {
            if (startLine >= snapshot.LineCount)
            {
                throw new ArgumentException("startLine", EditorUtilsResources.InvalidLineNumber);
            }

            if (startLine + (count - 1) >= snapshot.LineCount || count < 1)
            {
                throw new ArgumentException("count", EditorUtilsResources.InvalidLineNumber);
            }

            _snapshot = snapshot;
            _startLine = startLine;
            _count = count;
        }

        public string GetText()
        {
            return Extent.GetText();
        }

        public string GetTextIncludingLineBreak()
        {
            return ExtentIncludingLineBreak.GetText();
        }

        public override int GetHashCode()
        {
            return _startLine ^ _count;
        }

        public override bool Equals(object obj)
        {
            if (obj is SnapshotLineRange)
            {
                return Equals((SnapshotLineRange)obj);
            }

            return false;
        }

        public bool Equals(SnapshotLineRange other)
        {
            return
                _snapshot == other._snapshot &&
                _startLine == other._startLine &&
                _count == other._count;
        }

        public static bool operator ==(SnapshotLineRange left, SnapshotLineRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SnapshotLineRange left, SnapshotLineRange right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return String.Format("[{0} - {1}] {2}", StartLineNumber, LastLineNumber, Snapshot);
        }

        /// <summary>
        /// Create for the entire ITextSnapshot
        /// </summary>
        public static SnapshotLineRange CreateForExtent(ITextSnapshot snapshot)
        {
            return new SnapshotLineRange(snapshot, 0, snapshot.LineCount);
        }

        /// <summary>
        /// Create for a single ITextSnapshotLine
        /// </summary>
        public static SnapshotLineRange CreateForLine(ITextSnapshotLine snapshotLine)
        {
            return new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, 1);
        }

        public static SnapshotLineRange CreateForSpan(SnapshotSpan span)
        {
            var startLine = span.GetStartLine();
            var lastLine = span.GetLastLine();
            return CreateForLineRange(startLine, lastLine);
        }

        /// <summary>
        /// Create a range for the provided ITextSnapshotLine and with at most count 
        /// length.  If count pushes the range past the end of the buffer then the 
        /// span will go to the end of the buffer
        /// </summary>
        public static SnapshotLineRange CreateForLineAndMaxCount(ITextSnapshotLine snapshotLine, int count)
        {
            var maxCount = (snapshotLine.Snapshot.LineCount - snapshotLine.LineNumber);
            count = Math.Min(count, maxCount);
            return new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, count);
        }

        /// <summary>
        /// Create a SnapshotLineRange which includes the 2 lines
        /// </summary>
        public static SnapshotLineRange CreateForLineRange(ITextSnapshotLine startLine, ITextSnapshotLine lastLine)
        {
            Contract.Requires(startLine.Snapshot == lastLine.Snapshot);
            var count = (lastLine.LineNumber - startLine.LineNumber) + 1;
            return new SnapshotLineRange(startLine.Snapshot, startLine.LineNumber, count);
        }

        /// <summary>
        /// Create a SnapshotLineRange which includes the 2 lines
        /// </summary>
        public static SnapshotLineRange? CreateForLineNumberRange(ITextSnapshot snapshot, int startLine, int lastLine)
        {
            Contract.Requires(startLine <= lastLine);
            if (startLine >= snapshot.LineCount || lastLine >= snapshot.LineCount)
            {
                return null;
            }

            return new SnapshotLineRange(snapshot, startLine, (lastLine - startLine) + 1);
        }
    }
}
