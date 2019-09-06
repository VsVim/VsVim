using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using System.Threading;

namespace Vim.EditorHost
{
    public static class Extensions
    {
        #region ITextBufferFactoryService

        /// <summary>
        /// Create an ITextBuffer with the specified content
        /// </summary>
        public static ITextBuffer CreateTextBufferRaw(this ITextBufferFactoryService textBufferFactoryService, string content, IContentType contentType = null)
        {
            var textBuffer = contentType != null
                ? textBufferFactoryService.CreateTextBuffer(contentType)
                : textBufferFactoryService.CreateTextBuffer();

            if (!string.IsNullOrEmpty(content))
            {
                textBuffer.Replace(new Span(0, 0), content);
            }

            return textBuffer;
        }

        /// <summary>
        /// Create an ITextBuffer with the specified lines
        /// </summary>
        public static ITextBuffer CreateTextBuffer(this ITextBufferFactoryService textBufferFactoryService, params string[] lines)
        {
            return CreateTextBuffer(textBufferFactoryService, null, lines);
        }

        /// <summary>
        /// Create an ITextBuffer with the specified content type and lines
        /// </summary>
        public static ITextBuffer CreateTextBuffer(this ITextBufferFactoryService textBufferFactoryService, IContentType contentType, params string[] lines)
        {
            var textBuffer = contentType != null
                ? textBufferFactoryService.CreateTextBuffer(contentType)
                : textBufferFactoryService.CreateTextBuffer();

            if (lines.Length != 0)
            {
                var text = lines.Aggregate((x, y) => x + Environment.NewLine + y);
                textBuffer.Replace(new Span(0, 0), text);
            }

            return textBuffer;
        }

        #endregion

        #region ITagger<T>

        /// <summary>
        /// Get the ITagSpan values for the given SnapshotSpan
        /// </summary>
        public static IEnumerable<ITagSpan<T>> GetTags<T>(this ITagger<T> tagger, SnapshotSpan span)
            where T : ITag
        {
            return tagger.GetTags(new NormalizedSnapshotSpanCollection(span));
        }

        #endregion

        #region ITextView

        public static SnapshotPoint GetPoint(this ITextView textView, int position)
        {
            return new SnapshotPoint(textView.TextSnapshot, position);
        }

        public static SnapshotPoint GetEndPoint(this ITextView textView)
        {
            return textView.TextSnapshot.GetEndPoint();
        }

        public static ITextSnapshotLine GetLine(this ITextView textView, int line)
        {
            return textView.TextSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int length)
        {
            return GetLineSpan(textView, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int column, int length)
        {
            return GetLineSpan(textView.TextBuffer, lineNumber, column, length);
        }

        public static SnapshotPoint GetCaretPoint(this ITextView textView)
        {
            return textView.Caret.Position.BufferPosition;
        }

        public static VirtualSnapshotPoint GetCaretVirtualPoint(this ITextView textView)
        {
            return textView.Caret.Position.VirtualBufferPosition;
        }

        public static ITextSnapshotLine GetCaretLine(this ITextView textView)
        {
            return textView.Caret.Position.BufferPosition.GetContainingLine();
        }

         /// <summary>
        /// Move the caret to the given position in the ITextView
        /// </summary>
        public static CaretPosition MoveCaretTo(this ITextView textView, int position)
        {
            return textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, position));
        }

        /// <summary>
        /// Move the caret to the given position in the ITextView with the set amount of virtual 
        /// spaces
        /// </summary>
        public static CaretPosition MoveCaretTo(this ITextView textView, int position, int virtualSpaces)
        {
            var point = new SnapshotPoint(textView.TextSnapshot, position);
            var virtualPoint = new VirtualSnapshotPoint(point, virtualSpaces);
            return textView.Caret.MoveTo(virtualPoint);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber)
        {
            var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
            return MoveCaretTo(textView, snapshotLine.Start.Position);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber, int column)
        {
            var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
            var point = snapshotLine.Start.Add(column);
            return MoveCaretTo(textView, point.Position);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber, int column, int virtualSpaces)
        {
            var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
            var point = snapshotLine.Start.Add(column);
            var virtualPoint = new VirtualSnapshotPoint(point, virtualSpaces);
            return textView.Caret.MoveTo(virtualPoint);
        }

        public static void SetText(this ITextView textView, params string[] lines)
        {
            textView.TextBuffer.SetText(lines);
        }

        #endregion

        #region ITextBuffer

        public static ITextSnapshotLine GetLineFromLineNumber(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        public static ITextSnapshotLine GetLine(this ITextBuffer buffer, int line)
        {
            return buffer.CurrentSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int length)
        {
            return GetLineSpan(buffer, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextBuffer buffer, int lineNumber, int column, int length)
        {
            var line = buffer.GetLine(lineNumber);
            return new SnapshotSpan(line.Start.Add(column), length);
        }

        public static SnapshotPoint GetPoint(this ITextBuffer buffer, int position)
        {
            return new SnapshotPoint(buffer.CurrentSnapshot, position);
        }

        public static VirtualSnapshotPoint GetVirtualPoint(this ITextBuffer buffer, int position)
        {
            return new VirtualSnapshotPoint(buffer.CurrentSnapshot, position);
        }

        public static VirtualSnapshotPoint GetVirtualPointInLine(this ITextBuffer buffer, int line, int column)
        {
            var snapshotLine = buffer.CurrentSnapshot.GetLineFromLineNumber(line);
            return new VirtualSnapshotPoint(snapshotLine, column);
        }

        public static SnapshotPoint GetEndPoint(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetEndPoint();
        }

        public static SnapshotSpan GetSpan(this ITextBuffer buffer, int start, int length)
        {
            return buffer.CurrentSnapshot.GetSpan(start, length);
        }

        /// <summary>
        /// Any ITextBuffer instance is possibly an IProjectionBuffer (which is a text buffer composed 
        /// of parts of other ITextBuffers).  This will return all of the real ITextBuffer buffers 
        /// composing the provided ITextBuffer
        /// </summary>
        public static IEnumerable<ITextBuffer> GetSourceBuffersRecursive(this ITextBuffer textBuffer)
        {
            if (textBuffer is IProjectionBuffer projectionBuffer)
            {
                return projectionBuffer.GetSourceBuffersRecursive();
            }

            return new [] { textBuffer };
        }

        public static SnapshotSpan GetExtent(this ITextBuffer textBuffer)
        {
            return textBuffer.CurrentSnapshot.GetExtent();
        }

        public static SnapshotPoint GetPointInLine(this ITextBuffer textBuffer, int line, int column)
        {
            return textBuffer.CurrentSnapshot.GetPointInLine(line, column);
        }

        public static VirtualSnapshotPoint GetVirtualPointInLine(this ITextBuffer textBuffer, int line, int column, int virtualSpaces)
        {
            return textBuffer.CurrentSnapshot.GetVirtualPointInLine(line, column, virtualSpaces);
        }

        public static void SetText(this ITextBuffer textBuffer, params string[] lines)
        {
            var textContent = string.Join(Environment.NewLine, lines);
            SetTextContent(textBuffer, textContent);
        }

        public static void SetTextContent(this ITextBuffer textBuffer, string textContent)
        {
            var edit = textBuffer.CreateEdit(EditOptions.DefaultMinimalChange, 0, null);
            edit.Replace(new Span(0, textBuffer.CurrentSnapshot.Length), textContent);
            edit.Apply();
        }

        #endregion

        #region ITextSnapshot

        public static NormalizedSnapshotSpanCollection GetTaggerExtent(this ITextSnapshot snapshot)
        {
            var span = snapshot.GetExtent();
            return new NormalizedSnapshotSpanCollection(span);
        }

        #endregion

        #region SnapshotPoint

        /// <summary>
        /// Get the column that this SnapshotPoint occupies
        /// </summary>
        public static int GetColumn(this SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            return point.Position - line.Start.Position;
        }

        public static SnapshotSpan GetSpan(this SnapshotPoint point, int length)
        {
            return new SnapshotSpan(point, length);
        }

        #endregion

        #region Dispatcher

        /// <summary>
        /// Run all outstanding events queued on the provided Dispatcher
        /// </summary>
        /// <param name="dispatcher"></param>
        public static void DoEvents(this Dispatcher dispatcher)
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

        public static IClassificationType GetOrCreateClassificationType(this IClassificationTypeRegistryService classificationTypeRegistryService, string type)
        {
            var classificationType = classificationTypeRegistryService.GetClassificationType(type);
            if (classificationType == null)
            {
                classificationType = classificationTypeRegistryService.CreateClassificationType(type, new IClassificationType[] { });
            }

            return classificationType;
        }

        #endregion

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
            var span = left.Span.CreateOverarching(right.Span);
            return new SnapshotSpan(left.Snapshot, span);
        }

        public static ITextSnapshotLine GetStartLine(this SnapshotSpan span)
        {
            return span.Start.GetContainingLine();
        }

        /// <summary>
        /// Get the last line included in the SnapshotSpan
        /// </summary>
        public static ITextSnapshotLine GetLastLine(this SnapshotSpan span)
        {
            return span.Length > 0
                ? span.End.Subtract(1).GetContainingLine()
                : GetStartLine(span);
        }

        #endregion

        #region ITextSnapshot

        public static char GetChar(this ITextSnapshot snapshot, int position)
        {
            return GetPoint(snapshot, position).GetChar();
        }

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

        /// <summary>
        /// Get the SnapshotPoint for the given position within the ITextSnapshot
        /// </summary>
        public static SnapshotPoint GetPoint(this ITextSnapshot snapshot, int position)
        {
            return new SnapshotPoint(snapshot, position);
        }

        public static SnapshotPoint GetPointInLine(this ITextSnapshot snapshot, int line, int column)
        {
            var snapshotLine = snapshot.GetLineFromLineNumber(line);
            return snapshotLine.Start.Add(column);
        }

        public static VirtualSnapshotPoint GetVirtualPointInLine(this ITextSnapshot snapshot, int line, int column, int virtualSpaces)
        {
            var snapshotLine = snapshot.GetLineFromLineNumber(line);
            return new VirtualSnapshotPoint(snapshotLine.Start.Add(column), virtualSpaces);
        }

        public static SnapshotPoint GetStartPoint(this ITextSnapshot snapshot)
        {
            return new SnapshotPoint(snapshot, 0);
        }

        public static SnapshotPoint GetEndPoint(this ITextSnapshot snapshot)
        {
            return new SnapshotPoint(snapshot, snapshot.Length);
        }

        #endregion

        #region IProjectionBuffer

        /// <summary>
        /// IProjectionBuffer instances can compose recursively.  This will look recursively down the 
        /// source buffers to find all of the critical ones
        /// </summary>
        public static IEnumerable<ITextBuffer> GetSourceBuffersRecursive(this IProjectionBuffer projectionBuffer)
        {
            var toVisit = new Queue<IProjectionBuffer>();
            toVisit.Enqueue(projectionBuffer);

            var found = new HashSet<ITextBuffer>();
            while (toVisit.Count > 0)
            {
                var current = toVisit.Dequeue();
                if (found.Contains(current))
                {
                    continue;
                }

                found.Add(current);
                foreach (var sourceBuffer in current.SourceBuffers)
                {
                    if (sourceBuffer is IProjectionBuffer sourceProjection)
                    {
                        toVisit.Enqueue(sourceProjection);
                    }
                    else
                    {
                        found.Add(sourceBuffer);
                    }
                }
            }

            return found.Where(x => !(x is IProjectionBuffer));
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

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> enumerable)
        {
            return new HashSet<T>(enumerable);
        }

        #endregion
    }
}
