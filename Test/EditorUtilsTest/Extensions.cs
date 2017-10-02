using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using EditorUtils.Implementation.Tagging;
using Microsoft.VisualStudio.Text.Classification;
using System.Threading;

namespace EditorUtils.UnitTest
{
    internal static class Extensions
    {
        #region ITagger<T>

        /// <summary>
        /// Get the ITagSpan values for the given SnapshotSpan
        /// </summary>
        internal static IEnumerable<ITagSpan<T>> GetTags<T>(this ITagger<T> tagger, SnapshotSpan span)
            where T : ITag
        {
            return tagger.GetTags(new NormalizedSnapshotSpanCollection(span));
        }

        #endregion

        #region ITextView

        internal static SnapshotPoint GetPoint(this ITextView textView, int position)
        {
            return new SnapshotPoint(textView.TextSnapshot, position);
        }

        internal static SnapshotPoint GetEndPoint(this ITextView textView)
        {
            return textView.TextSnapshot.GetEndPoint();
        }

        internal static ITextSnapshotLine GetLine(this ITextView textView, int line)
        {
            return textView.TextSnapshot.GetLineFromLineNumber(line);
        }

        internal static SnapshotLineRange GetLineRange(this ITextView textView, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRange.CreateForLineNumberRange(textView.TextSnapshot, startLine, endLine).Value;
        }

        internal static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int length)
        {
            return GetLineSpan(textView, lineNumber, 0, length);
        }

        internal static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int column, int length)
        {
            return GetLineSpan(textView.TextBuffer, lineNumber, column, length);
        }

        #endregion

        #region ITextBuffer

        internal static ITextSnapshotLine GetLineFromLineNumber(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        internal static ITextSnapshotLine GetLine(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        internal static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int length)
        {
            return GetLineSpan(buffer, lineNumber, 0, length);
        }

        internal static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int column, int length)
        {
            var line = buffer.GetLine(lineNumber);
            return new SnapshotSpan(line.Start.Add(column), length);
        }

        internal static SnapshotPoint GetPoint(this ITextBuffer buffer, int position)
        {
            return new SnapshotPoint(buffer.CurrentSnapshot, position);
        }

        internal static SnapshotPoint GetEndPoint(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetEndPoint();
        }

        internal static SnapshotSpan GetSpan(this ITextBuffer buffer, int start, int length)
        {
            return buffer.CurrentSnapshot.GetSpan(start, length);
        }

        #endregion

        #region ITextSnapshot

        internal static NormalizedSnapshotSpanCollection GetTaggerExtent(this ITextSnapshot snapshot)
        {
            var span = snapshot.GetExtent();
            return new NormalizedSnapshotSpanCollection(span);
        }

        #endregion

        #region SnapshotPoint

        /// <summary>
        /// Get the column that this SnapshotPoint occupies
        /// </summary>
        internal static int GetColumn(this SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            return point.Position - line.Start.Position;
        }

        internal static SnapshotSpan GetSpan(this SnapshotPoint point, int length)
        {
            return new SnapshotSpan(point, length);
        }

        #endregion

        #region Dispatcher

        /// <summary>
        /// Run all outstanding events queued on the provided Dispatcher
        /// </summary>
        /// <param name="dispatcher"></param>
        internal static void DoEvents(this Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            Action<DispatcherFrame> action = _ => { frame.Continue = false; };
            dispatcher.BeginInvoke(
                DispatcherPriority.SystemIdle,
                action,
                frame);

            var count = 3;
            do
            {
                try
                {
                    Dispatcher.PushFrame(frame);
                    break;
                }
                catch
                {
                    // The core editor can surface exceptions when we run events in
                    // this manner.  It would be nice if they could be distinguished from
                    // exceptions thrown from EditorUtils but can't find a way.  Reluctantly
                    // swallow exceptions.
                }

                count--;
            } while (count > 0);
        }

        #endregion

        #region IClassificationTypeRegistryService

        internal static IClassificationType GetOrCreateClassificationType(this IClassificationTypeRegistryService classificationTypeRegistryService, string type)
        {
            var classificationType = classificationTypeRegistryService.GetClassificationType(type);
            if (classificationType == null)
            {
                classificationType = classificationTypeRegistryService.CreateClassificationType(type, new IClassificationType[] { });
            }

            return classificationType;
        }

        #endregion

        #region AsyncTagger

        internal static void WaitForBackgroundToComplete<TData, TTag>(this AsyncTagger<TData, TTag> asyncTagger, TestableSynchronizationContext synchronizationContext)
            where TTag : ITag
        {
            while (asyncTagger.AsyncBackgroundRequestData.HasValue)
            {
                synchronizationContext.RunAll();
                Thread.Yield();
            }
        }

        #endregion 
    }
}
