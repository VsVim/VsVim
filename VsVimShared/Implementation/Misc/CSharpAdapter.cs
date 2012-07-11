using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// Implements C# specific adaptations of VsVim
    /// </summary>
    [Export(typeof(IVisualModeSelectionOverride))]
    internal sealed class CSharpAdapter : IVisualModeSelectionOverride
    {
        /// <summary>
        /// This regex is intended to match the C# generated event handler pattern
        /// </summary>
        private static readonly Regex s_eventSyntaxRegex = new Regex(@"\+=\s*new\s+[a-z.]+\s*\([a-z_]*", RegexOptions.IgnoreCase);

        internal bool IsInsertModePreferred(ITextView textView)
        {
            return
                textView.TextBuffer.ContentType.IsCSharp() &&
                IsEventAddSelection(textView);
        }

        /// <summary>
        /// Is the current selection that of the C# event add pattern?
        /// </summary>
        internal bool IsEventAddSelection(ITextView textView)
        {
            var textSelection = textView.Selection;
            if (textSelection.IsEmpty || textSelection.Mode != TextSelectionMode.Stream)
            {
                return false;
            }

            var span = textView.Selection.StreamSelectionSpan.SnapshotSpan;
            var lineRange = SnapshotLineRangeUtil.CreateForSpan(span);
            if (lineRange.Count != 1)
            {
                return false;
            }

            var beforeSpan = new SnapshotSpan(lineRange.Start, span.End);
            return IsPreceededByEventAddSyntax(beforeSpan);
        }

        /// <summary>
        /// Is the provided SnapshotPoint preceeded by the '+= SomeEventType(Some_HandlerName' line
        /// </summary>
        private bool IsPreceededByEventAddSyntax(SnapshotSpan span)
        {
            // First find the + character
            var snapshot = span.Snapshot;
            SnapshotPoint? plusPoint = null;
            for (int i = span.Length - 1; i >= 0; i--)
            {
                var position = span.Start.Position + i;
                var point = new SnapshotPoint(snapshot, position);
                if (point.GetChar() == '+')
                {
                    plusPoint = point;
                    break;
                }
            }

            if (plusPoint == null)
            {
                return false;
            }

            var eventSpan = new SnapshotSpan(plusPoint.Value, span.End);
            return s_eventSyntaxRegex.IsMatch(eventSpan.GetText());
        }

        #region IVisualModeSelectionOverride

        bool IVisualModeSelectionOverride.IsInsertModePreferred(ITextView textView)
        {
            return IsInsertModePreferred(textView);
        }

        #endregion
    }
}
