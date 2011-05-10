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
        /// Save all of the current Edit markers in the visual span for ignoring.  Ideally we would 
        /// consider the entire ITextBuffer but that could involve formatting many many thousands
        /// of lines and be very expensive.  Instead we just consider the edit markers in the
        /// visual ITextBuffer
        /// </summary>
        private void SaveCurrentEditorMarkersForIgnore()
        {
            _ignoredMarkers.Clear();
            GetExternalEditSpans(_ignoredMarkers);
        }

        private void CheckForExternalEdit()
        {
            var markers = GetExternalEditSpans();
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

        internal List<SnapshotSpan> GetExternalEditSpans()
        {
            var list = new List<SnapshotSpan>();
            GetExternalEditSpans(list);
            return list;
        }

        private void GetExternalEditSpans(List<SnapshotSpan> list)
        {
            var collection = _buffer.TextView.GetLikelyVisibleSnapshotSpans();
            foreach (var span in collection)
            {
                GetExternalEditSpansFromMarkers(span, list);
                GetExternalEditSpansFromTags(span, list);
            }
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
