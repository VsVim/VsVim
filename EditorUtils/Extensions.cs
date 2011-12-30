using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils
{
    public static class Extensions
    {
        #region Span

        public static Span CreateOverarching(this Span left, Span right)
        {
            var start = Math.Min(left.Start, right.Start);
            var end = Math.Max(left.End, right.End);
            return Span.FromBounds(start, end);
        }

        #endregion

        #region SnapshotSpan

        public static SnapshotSpan CreateOverarching(this SnapshotSpan left, SnapshotSpan right)
        {
            Contract.Requires(left.Snapshot == right.Snapshot);
            var span = left.Span.CreateOverarching(right.Span);
            return new SnapshotSpan(left.Snapshot, span);
        }

        public static ITextSnapshotLine GetStartLine(this SnapshotSpan span)
        {
            return span.Start.GetContainingLine();
        }

        /// <summary>
        /// Get the last line included in the SnapshotSpan
        ///
        /// TODO: Should provide a bool option to dictate how we handle an empty last line
        /// </summary>
        public static ITextSnapshotLine GetLastLine(this SnapshotSpan span)
        {
            var snapshot = span.Snapshot;
            var snapshotEndPoint = new SnapshotPoint(snapshot, snapshot.Length);
            if (snapshotEndPoint == span.End)
            {
                var line = span.End.GetContainingLine();
                if (line.Length == 0)
                {
                    return line;
                }
            }

            return span.Length > 0
                ? span.End.Subtract(1).GetContainingLine()
                : GetStartLine(span);
        }

        #endregion

        #region ITextSnapshot

        /// <summary>
        /// Get the SnapshotSpan for the extent of the entire ITextSnapshot
        /// </summary>
        public static SnapshotSpan GetExtent(this ITextSnapshot snapshot)
        {
            return new SnapshotSpan(snapshot, 0, snapshot.Length);
        }

        public static SnapshotSpan GetSpan(this ITextSnapshot snapshot, int start, int length)
        {
            return new SnapshotSpan(snapshot, start, length);
        }

        #endregion

        #region ITextView

        /// <summary>
        /// Return the overaching SnapshotLineRange for the visible lines in the ITextView
        /// </summary>
        public static SnapshotLineRange? GetVisibleSnapshotLineRange(this ITextView textView)
        {
            if (textView.InLayout)
            {
                return null;
            }
            var snapshot = textView.TextSnapshot;
            var lines = textView.TextViewLines;
            var startLine = lines.FirstVisibleLine.Start.GetContainingLine().LineNumber;
            var lastLine = lines.LastVisibleLine.End.GetContainingLine().LineNumber;
            return SnapshotLineRange.CreateForLineNumberRange(textView.TextSnapshot, startLine, lastLine);
        }

        #endregion

        #region PropertyCollection

        public static bool TryGetPropertySafe<TProperty>(this PropertyCollection propertyCollection, object key, out TProperty value)
        {
            try
            {
                return propertyCollection.TryGetProperty<TProperty>(key, out value);
            }
            catch (Exception)
            {
                // If the value exists but is not convertible to the provided type then
                // an exception will be thrown.  Collapse this into an empty option.  
                // Helps guard against cases where other extensions override our values
                // with ones of unexpected types
                value = default(TProperty);
                return false;
            }
        }

        #endregion

        #region ITrackingSpan

        public static SnapshotSpan? GetSpanSafe(this ITrackingSpan trackingSpan, ITextSnapshot snapshot)
        {
            try
            {
                return trackingSpan.GetSpan(snapshot);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        #endregion

        #region NormalizedSnapshotSpanCollection

        public static SnapshotSpan GetOverarchingSpan(this NormalizedSnapshotSpanCollection collection)
        {
            var start = collection[0];
            var end = collection[collection.Count - 1];
            return new SnapshotSpan(start.Start, end.End);
        }

        #endregion

        #region Collections

        public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumerable)
        {
            return new ReadOnlyCollection<T>(enumerable.ToList());
        }

        public static ReadOnlyCollection<T> ToReadOnlyCollectionShallow<T>(this List<T> list)
        {
            return new ReadOnlyCollection<T>(list);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> enumerable)
        {
            return new HashSet<T>(enumerable);
        }

        #endregion
    }
}
