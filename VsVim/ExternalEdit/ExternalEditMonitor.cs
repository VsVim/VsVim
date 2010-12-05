using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.ExternalEdit
{
    internal sealed class ExternalEditMonitor
    {
        private readonly IVimBuffer _buffer;
        private readonly Result<IVsTextLines> _vsTextLines;
        private readonly ITagAggregator<ITag> _tagAggregator;
        private readonly ReadOnlyCollection<IExternalEditorAdapter> _externalEditorAdapters;
        private readonly List<ExternalEditMarker> _ignoredMarkers = new List<ExternalEditMarker>();

        internal bool InExternalEdit { get; set; }

        internal IEnumerable<ExternalEditMarker> IgnoredMarkers
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
            ReadOnlyCollection<IExternalEditorAdapter> externalEditorAdapters,
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

        internal List<ExternalEditMarker> GetExternalEditMarkers(SnapshotSpan span)
        {
            var list = new List<ExternalEditMarker>();
            GetExternalEditMarkers(span, list);
            return list;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            CheckForExternalEdit();
        }

        private void OnSwitchedMode(object sender, IMode newMode)
        {
            if (InExternalEdit)
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
            _ignoredMarkers.Clear();
            GetExternalEditMarkers(span, _ignoredMarkers);
        }

        private void CheckForExternalEdit()
        {
            if (InExternalEdit)
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
            if (markers.All(ShouldIgnore))
            {
                ClearIgnoreMarkers();
                return;
            }

            // Not in an external edit and there are edit markers we need to consider.  Time to enter
            // external edit mode
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            InExternalEdit = true;
        }

        private void ClearIgnoreMarkers()
        {
            _ignoredMarkers.Clear();
        }

        private void GetExternalEditMarkers(SnapshotSpan span, List<ExternalEditMarker> list)
        {
            GetExternalEditMarkersFromShims(span, list);
            GetExternalEditMarkersFromTags(span, list);
        }

        private void GetExternalEditMarkersFromTags(SnapshotSpan span, List<ExternalEditMarker> list)
        {
            var tags = _tagAggregator.GetTags(span);
            foreach (var cur in tags)
            {
                foreach (var tagSpan in cur.Span.GetSpans(_buffer.TextSnapshot))
                {
                    foreach (var adapter in _externalEditorAdapters)
                    {
                        var marker = adapter.TryCreateExternalEditMarker(cur.Tag, tagSpan);
                        if (marker.HasValue)
                        {
                            list.Add(marker.Value);
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
        private void GetExternalEditMarkersFromShims(SnapshotSpan span, List<ExternalEditMarker> list)
        {
            if (_vsTextLines.IsValue)
            {
                var markers = _vsTextLines.Value.GetLineMarkers(span.ToTextSpan());
                foreach (var marker in markers)
                {
                    foreach (var adapter in _externalEditorAdapters)
                    {
                        var editMarker = adapter.TryCreateExternalEditMarker(marker, _buffer.TextSnapshot);
                        if (editMarker.HasValue)
                        {
                            list.Add(editMarker.Value);
                        }
                    }
                }
            }
        }

        private bool ShouldIgnore(ExternalEditMarker marker)
        {
            foreach (var ignore in _ignoredMarkers)
            {
                if (ignore.Span.OverlapsWith(marker.Span))
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
                if (_ignoredMarkers[i].Span.Snapshot == snapshot)
                {
                    i++;
                    continue;
                }

                var mapped = _ignoredMarkers[i].Span.SafeTranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
                if (mapped.IsValue)
                {
                    _ignoredMarkers[i] = new ExternalEditMarker(_ignoredMarkers[i].ExternalEditKind, mapped.Value);
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
