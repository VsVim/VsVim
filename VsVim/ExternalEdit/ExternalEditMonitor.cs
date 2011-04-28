using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;

namespace VsVim.ExternalEdit
{
    internal sealed class ExternalEditMonitor
    {
        private readonly IVimBuffer _buffer;
        private readonly Result<IVsTextLines> _vsTextLines;
        private readonly ITagAggregator<ITag> _tagAggregator;
        private readonly ReadOnlyCollection<IExternalEditAdapter> _externalEditorAdapters;
        private readonly List<SnapshotSpan> _ignoredMarkers = new List<SnapshotSpan>();

        internal IEnumerable<SnapshotSpan> IgnoredMarkers
        {
            get { return _ignoredMarkers; }
            set
            {
                _ignoredMarkers.Clear();
                _ignoredMarkers.AddRange(value);
            }
        }

        internal ExternalEditMonitor(
            IVimBuffer buffer,
            Result<IVsTextLines> vsTextLines,
            ReadOnlyCollection<IExternalEditAdapter> externalEditorAdapters,
            ITagAggregator<ITag> tagAggregator)
        {
            _vsTextLines = vsTextLines;
            _externalEditorAdapters = externalEditorAdapters;
            _tagAggregator = tagAggregator;
            _buffer = buffer;
            _buffer.TextView.LayoutChanged += OnLayoutChanged;
            _buffer.SwitchedMode += OnSwitchedMode;
        }

        internal void Close()
        {
            _buffer.TextView.LayoutChanged -= OnLayoutChanged;
        }

        internal List<SnapshotSpan> GetExternalEditSpans(SnapshotSpan span)
        {
            var list = new List<SnapshotSpan>();
            GetExternalEditSpans(span, list);
            return list;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            CheckForExternalEdit();
        }

        private void OnSwitchedMode(object sender, SwitchModeEventArgs args)
        {
            if ( args.PreviousMode.IsSome() && args.PreviousMode.Value.ModeKind == ModeKind.ExternalEdit)
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
            var span = SnapshotUtil.GetExtent(_buffer.TextSnapshot);
            _ignoredMarkers.Clear();
            GetExternalEditSpans(span, _ignoredMarkers);
        }

        private void CheckForExternalEdit()
        {
            // Only check for an external edit if there are visible lines.  In the middle of a nested layout
            // the set of visible lines will temporarily be unavailable
            var range = _buffer.TextView.GetVisibleLineRange();
            if (range.IsError)
            {
                return;
            }

            var markers = GetExternalEditSpans(range.Value.ExtentIncludingLineBreak);
            MoveIgnoredMarkersToCurrentSnapshot();
            if (markers.All(ShouldIgnore))
            {
                if (markers.Count == 0)
                {
                    ClearIgnoreMarkers();

                    // If we're in an external edit and all of the markers leave then transition back to
                    // insert mode
                    if (_buffer.ModeKind == ModeKind.ExternalEdit)
                    {
                        _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                    }
                }
            } 
            else if (_buffer.ModeKind != ModeKind.ExternalEdit)
            {
                // Not in an external edit and there are edit markers we need to consider.  Time to enter
                // external edit mode
                _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            }
        }

        private void ClearIgnoreMarkers()
        {
            _ignoredMarkers.Clear();
        }

        private void GetExternalEditSpans(SnapshotSpan span, List<SnapshotSpan> list)
        {
            GetExternalEditSpansFromMarkers(span, list);
            GetExternalEditSpansFromTags(span, list);
        }

        private void GetExternalEditSpansFromTags(SnapshotSpan span, List<SnapshotSpan> list)
        {
            var tags = _tagAggregator.GetTags(span);
            foreach (var cur in tags)
            {
                foreach (var adapter in _externalEditorAdapters)
                {
                    if (adapter.IsExternalEditTag(cur.Tag))
                    {
                        foreach (var tagSpan in cur.Span.GetSpans(_buffer.TextSnapshot))
                        {
                            list.Add(tagSpan);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns all active ExternalEditMarker instances for the given range for the old style
        /// Visual Studio markers.  It's possible this is a pure Dev10 ITextBuffer though hence won't
        /// have any old style markers
        /// </summary>
        private void GetExternalEditSpansFromMarkers(SnapshotSpan span, List<SnapshotSpan> list)
        {
            if (_vsTextLines.IsSuccess)
            {
                var markers = _vsTextLines.Value.GetLineMarkers(span.ToTextSpan());
                foreach (var marker in markers)
                {
                    foreach (var adapter in _externalEditorAdapters)
                    {
                        if (adapter.IsExternalEditMarker(marker))
                        {
                            var markerSpan = marker.GetCurrentSpan(_buffer.TextSnapshot);
                            if (markerSpan.IsSuccess)
                            {
                                list.Add(markerSpan.Value);
                            }
                        }
                    }
                }
            }
        }

        private bool ShouldIgnore(SnapshotSpan marker)
        {
            foreach (var ignore in _ignoredMarkers)
            {
                if (ignore.Span.OverlapsWith(marker))
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveIgnoredMarkersToCurrentSnapshot()
        {
            var snapshot = _buffer.TextSnapshot;
            var i = 0;
            while (i < _ignoredMarkers.Count)
            {
                if (_ignoredMarkers[i].Snapshot == snapshot)
                {
                    i++;
                    continue;
                }

                var mapped = _ignoredMarkers[i].SafeTranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
                if (mapped.IsSuccess)
                {
                    _ignoredMarkers[i] = mapped.Value;
                }
                else
                {
                    _ignoredMarkers.RemoveAt(i);
                }
                i++;
            }
        }
    }
}
