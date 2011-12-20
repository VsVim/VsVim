using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils.UnitTest
{
    public static class EditorExtensions
    {
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

        public static SnapshotPoint GetPointInLine(this ITextView textView, int line, int column)
        {
            return textView.TextBuffer.GetPointInLine(line, column);
        }

        public static SnapshotPoint GetEndPoint(this ITextView textView)
        {
            return textView.TextSnapshot.GetEndPoint();
        }

        public static ITextSnapshotLine GetLine(this ITextView textView, int line)
        {
            return textView.TextSnapshot.GetLineFromLineNumber(line);
        }

        public static SnapshotLineRange GetLineRange(this ITextView textView, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRange.CreateForLineNumberRange(textView.TextSnapshot, startLine, endLine).Value;
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int length)
        {
            return GetLineSpan(textView, lineNumber, 0, length);
        }

        public static SnapshotSpan GetLineSpan(this ITextView textView, int lineNumber, int column, int length)
        {
            return GetLineSpan(textView.TextBuffer, lineNumber, column, length);
        }

        public static ITextSnapshotLine GetLastLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetLastLine();
        }

        public static ITextSnapshotLine GetFirstLine(this ITextView textView)
        {
            return textView.TextSnapshot.GetFirstLine();
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
        public static void MoveCaretTo(this ITextView textView, int position, int virtualSpaces)
        {
            var point = new SnapshotPoint(textView.TextSnapshot, position);
            var virtualPoint = new VirtualSnapshotPoint(point, virtualSpaces);
            textView.Caret.MoveTo(virtualPoint);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber)
        {
            return MoveCaretTo(textView, textView.GetLine(lineNumber).Start.Position);
        }

        public static CaretPosition MoveCaretToLine(this ITextView textView, int lineNumber, int column)
        {
            var point = textView.GetLine(lineNumber).Start.Add(column);
            return MoveCaretTo(textView, point.Position);
        }

        public static ITextSnapshotLine GetCaretLine(this ITextView textView)
        {
            return textView.Caret.Position.BufferPosition.GetContainingLine();
        }

        public static void SetText(this ITextView textView, params string[] lines)
        {
            SetText(textView.TextBuffer, lines);
        }

        public static void SetText(this ITextView textView, string text, int? caret = null)
        {
            SetText(textView.TextBuffer, text);
            if (caret.HasValue)
            {
                MoveCaretTo(textView, caret.Value);
            }
        }

        #endregion

        #region IWpfTextView

        /// <summary>
        /// Make only a single line visible in the IWpfTextView.  This is really useful when testing
        /// actions like scrolling
        /// </summary>
        /// <param name="textView"></param>
        public static void MakeOneLineVisible(this IWpfTextView wpfTextView)
        {
            var oldSize = wpfTextView.VisualElement.RenderSize;
            var size = new Size(
                oldSize.Width,
                wpfTextView.TextViewLines.FirstVisibleLine.Height);
            wpfTextView.VisualElement.RenderSize = size;
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

        public static SnapshotLineRange GetLineRange(this ITextBuffer buffer, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRange.CreateForLineNumberRange(buffer.CurrentSnapshot, startLine, endLine).Value;
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

        public static SnapshotPoint GetPointInLine(this ITextBuffer textBuffer, int line, int column)
        {
            var snapshotLine = textBuffer.GetLine(line);
            return snapshotLine.Start.Add(column);
        }

        public static SnapshotPoint GetEndPoint(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetEndPoint();
        }

        public static SnapshotSpan GetExtent(this ITextBuffer buffer)
        {
            return buffer.CurrentSnapshot.GetExtent();
        }

        public static SnapshotSpan GetSpan(this ITextBuffer buffer, int start, int length)
        {
            return buffer.CurrentSnapshot.GetSpan(start, length);
        }

        public static void SetText(this ITextBuffer buffer, params string[] lines)
        {
            var text = lines.Aggregate((x, y) => x + Environment.NewLine + y);
            var edit = buffer.CreateEdit(EditOptions.DefaultMinimalChange, 0, null);
            edit.Replace(new Span(0, buffer.CurrentSnapshot.Length), text);
            edit.Apply();
        }

        #endregion

        #region ITextBufferFactoryService

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

        #region ITextSnapshot

        public static ITextSnapshotLine GetLine(this ITextSnapshot tss, int lineNumber)
        {
            return tss.GetLineFromLineNumber(lineNumber);
        }

        public static SnapshotLineRange GetLineRange(this ITextSnapshot tss, int startLine, int endLine = -1)
        {
            endLine = endLine >= 0 ? endLine : startLine;
            return SnapshotLineRange.CreateForLineNumberRange(tss, startLine, endLine).Value;
        }

        public static ITextSnapshotLine GetFirstLine(this ITextSnapshot tss)
        {
            return GetLine(tss, 0);
        }

        public static ITextSnapshotLine GetLastLine(this ITextSnapshot tss)
        {
            return GetLine(tss, tss.LineCount - 1);
        }

        public static SnapshotPoint GetPoint(this ITextSnapshot tss, int position)
        {
            return new SnapshotPoint(tss, position);
        }

        public static SnapshotPoint GetEndPoint(this ITextSnapshot tss)
        {
            return new SnapshotPoint(tss, tss.Length);
        }

        public static SnapshotSpan GetSpan(this ITextSnapshot tss, int start, int length)
        {
            return new SnapshotSpan(tss, start, length);
        }

        public static SnapshotSpan GetExtent(this ITextSnapshot snapshot)
        {
            return new SnapshotSpan(snapshot, 0, snapshot.Length);
        }

        public static NormalizedSnapshotSpanCollection GetTaggerExtent(this ITextSnapshot snapshot)
        {
            var span = GetExtent(snapshot);
            return new NormalizedSnapshotSpanCollection(span);
        }

        #endregion

        #region SnapshotPoint

        /// <summary>
        /// Get the column hat this SnapshotPoint occupies
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

    }
}
