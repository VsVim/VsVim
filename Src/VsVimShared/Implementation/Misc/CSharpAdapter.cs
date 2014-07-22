using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace Vim.VisualStudio.Implementation.Misc
{
    /// <summary>
    /// Implements C# specific adaptations of VsVim
    /// </summary>
    [Export(typeof(IVisualModeSelectionOverride))]
    internal sealed class CSharpAdapter : IVisualModeSelectionOverride
    {
        /// <summary>
        /// This regex is intended to match the C# generated event handler pattern.  This is the pattern which is 
        /// used in Visual Studio 2010
        /// </summary>
        private static readonly Regex s_fullEventSyntaxRegex = new Regex(@"\+=\s*new\s+[a-z0-9.]+\s*\([a-z0-9_]*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// This regex matches the shorter event pattern.  This is the default in Visual Studio 2012
        /// </summary>
        private static readonly Regex s_shortEventSyntaxRegex = new Regex(@"\+=\s*[a-z0-9_.]+\s*;", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            // Include the character after the selection.  Needed to disambiguate a couple
            // of cases
            var endPoint = span.End;
            if (endPoint.Position < lineRange.End.Position)
            {
                endPoint = endPoint.Add(1);
            }

            var beforeSpan = new SnapshotSpan(lineRange.Start, endPoint);
            return IsPreceededByEventAddSyntax(beforeSpan);
        }

        /// <summary>
        /// Is the provided SnapshotPoint preceded by the '+= SomeEventType(Some_HandlerName' line
        /// </summary>
        private bool IsPreceededByEventAddSyntax(SnapshotSpan span)
        {
            // First find the last + character
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
            var eventText = eventSpan.GetText();
            return 
                s_fullEventSyntaxRegex.IsMatch(eventText) ||
                s_shortEventSyntaxRegex.IsMatch(eventText);
        }

        #region IVisualModeSelectionOverride

        bool IVisualModeSelectionOverride.IsInsertModePreferred(ITextView textView)
        {
            return IsInsertModePreferred(textView);
        }

        #endregion
    }
}
