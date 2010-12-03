using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.ExternalEdit
{
    internal sealed class ExternalEditManager
    {
        private readonly IVimBuffer _buffer;
        private readonly IVsTextLines _vsTextLines;
        private bool m_inExternalEdit;
        private SnapshotSpan? m_ignoreSnippetMarkerSpan;

        internal ExternalEditManager(IVimBuffer buffer, IVsTextLines vsTextLines)
        {
            _vsTextLines = vsTextLines;
            _buffer = buffer;
            _buffer.TextView.LayoutChanged += OnLayoutChanged;
            _buffer.SwitchedMode += OnSwitchedMode;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            CheckForExternalEdit();
        }

        private void OnSwitchedMode(object sender, IMode newMode)
        {
            if (m_inExternalEdit)
            {
                // If we're in the middle of an external edit and the mode switches then we 
                // need to record the current edit markers so we can ignore them going 
                // forward.  Further updates which cause these markers to be rendered shouldn't
                // cause us to re-enter external edit mode
                SaveCurrentEditorMarkersForIgnore();
            }
        }

        /// <summary>
        /// Save all of the current Edit markers for ignoring.  Have to consider the entire
        /// buffer here because large external edits (big snippets, refactors, etc ...) could 
        /// have off screen markers.  Don't want a future scroll which includes those markers
        /// to make the user re-enter external edit mode
        /// </summary>
        private void SaveCurrentEditorMarkersForIgnore()
        {
            var span = SnapshotUtil.GetFullSpan(_buffer.TextSnapshot);
            var markers = GetExternalEditMarkers(span);
            SaveCurrentSnippetMarkersForIgnore(markers);
        }

        /// <summary>
        /// For snippets just track the span for the snippet
        /// </summary>
        /// <param name="markers"></param>
        private void SaveCurrentSnippetMarkersForIgnore(IEnumerable<ExternalEditMarker> markers)
        {
            SnapshotPoint? start = null;
            SnapshotPoint? end = null;
            foreach (var cur in markers.Where(x => x.ExternalEditKind == ExternalEditKind.Snippet))
            {
                if (start == null)
                {
                    start = cur.Span.Start;
                    end = cur.Span.End;
                }
                else
                {
                    if (cur.Span.Start < start.Value)
                    {
                        start = cur.Span.Start;
                    }

                    if (cur.Span.End > end.Value)
                    {
                        end = cur.Span.End;
                    }
                }
            }

            if (!start.HasValue || !end.HasValue)
            {
                m_ignoreSnippetMarkerSpan = null;
            }
            else
            {
                m_ignoreSnippetMarkerSpan = new SnapshotSpan(start.Value, end.Value);
            }
        }

        private void CheckForExternalEdit()
        {
            if (_buffer.ModeKind == ModeKind.ExternalEdit)
            {
                return;
            }

            var range = _buffer.TextView.GetVisibleLineRange();
            if (range.IsError)
            {
                return;
            }

            var markers = GetExternalEditMarkers(range.Value.ExtentIncludingLineBreak);

            MoveIgnoredMarkersToCurrentSnapshot();
            if (markers.All(x => ShouldIgnore(x)))
            {
                if (markers.Count == 0)
                {
                    ClearIgnoreMarkers();
                }
                return;
            }

            // Not in an external edit and there are edit markers we need to consider.  Time to enter
            // external edit mode
            if (GetExternalEditMarkers(range.Value.ExtentIncludingLineBreak).Any())
            {
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                m_inExternalEdit = true;
            }
        }

        private void ClearIgnoreMarkers()
        {
            m_ignoreSnippetMarkerSpan = null;
        }

        /// <summary>
        /// Returns all active ExternalEditMarker instances for the given range
        /// </summary>
        private List<ExternalEditMarker> GetExternalEditMarkers(SnapshotSpan span)
        {
            var markers = _vsTextLines.GetLineMarkers(span.ToTextSpan());
            var list = new List<ExternalEditMarker>();
            foreach (var marker in markers)
            {
                ExternalEditMarker editMarker;
                if (TryCreateExternalEditMarker(marker, out editMarker))
                {
                    list.Add(editMarker);
                }
            }

            return list;
        }

        private bool ShouldIgnore(ExternalEditMarker marker)
        {
            switch (marker.ExternalEditKind)
            {
                case ExternalEditKind.Snippet:
                    return m_ignoreSnippetMarkerSpan.HasValue 
                        && marker.Span.OverlapsWith(m_ignoreSnippetMarkerSpan.Value);
                default:
                case ExternalEditKind.None:
                    Debug.Fail("Invalid enum value");
                    return true;
            }
        }

        private void MoveIgnoredMarkersToCurrentSnapshot()
        {
            var snapshot = _buffer.TextSnapshot;
            if (m_ignoreSnippetMarkerSpan.HasValue)
            {
                var mapped = m_ignoreSnippetMarkerSpan.Value.SafeTranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
                if (mapped.IsValue)
                {
                    m_ignoreSnippetMarkerSpan = mapped.Value;
                }
                else
                {
                    m_ignoreSnippetMarkerSpan = null;
                }
            }
        }

        private bool TryCreateExternalEditMarker(IVsTextLineMarker marker, out ExternalEditMarker editMarker)
        {
            editMarker = new ExternalEditMarker();
            var result = marker.GetMarkerType();
            if (result.IsError)
            {
                return false;
            }

            // Predefined markers aren't a concern
            var value = (int)result.Value;
            if (value <= (int)MARKERTYPE.DEF_MARKER_COUNT)
            {
                return false;
            }

            // Get the SnapshotSpan for the marker
            var span = marker.GetCurrentSpan(_buffer.TextSnapshot);
            if (span.IsError)
            {
                return false;
            }

            switch ((int)result.Value)
            {
                case 15:
                case 16:
                case 26:
                    // Details
                    //  15: Snippet marker for inactive span
                    //  16: Snippet marker for active span
                    //  26: Tracks comment insertion for a snippet
                    editMarker = new ExternalEditMarker(ExternalEditKind.Snippet, span.Value);
                    return true;
                case 25:
                    // Kind currently unknown.  
                    return false;
                default:
                    // TODO: Should remove this after development completes
                    return false;
            }
        }

        internal static void Monitor(IVimBuffer buffer, IVsTextLines vsTextLines)
        {
            new ExternalEditManager(buffer, vsTextLines);
        }
    }
}
