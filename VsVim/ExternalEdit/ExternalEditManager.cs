using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.ExternalEdit
{
    internal sealed class ExternalEditManager
    {
        private readonly IVimBuffer _buffer;
        private readonly IVsTextLines _vsTextLines;
        private readonly ITagAggregator<ITag> _tagAggregator;
        private readonly List<ExternalEditMarker> _ignoreMarkers = new List<ExternalEditMarker>();

        // TODO: Share this among all ExternalEditManager instances
        private readonly List<IExternalEditorAdapter> _externalEditorAdapters = new List<IExternalEditorAdapter>();
        private bool _inExternalEdit;

        internal ExternalEditManager(IVimBuffer buffer, IVsTextLines vsTextLines, IViewTagAggregatorFactoryService tagAggregatorFactoryService)
        {
            _vsTextLines = vsTextLines;
            _tagAggregator = tagAggregatorFactoryService.CreateTagAggregator<ITag>(buffer.TextView);
            _buffer = buffer;
            _buffer.TextView.LayoutChanged += OnLayoutChanged;
            _buffer.SwitchedMode += OnSwitchedMode;
            _externalEditorAdapters.Add(new SnippetExternalEditorAdapter());
            _externalEditorAdapters.Add(new ResharperExternalEditorAdapter());
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            CheckForExternalEdit();
        }

        private void OnSwitchedMode(object sender, IMode newMode)
        {
            if (_inExternalEdit)
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
            _ignoreMarkers.Clear();
            GetExternalEditMarkers(span, _ignoreMarkers);
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
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
            _inExternalEdit = true;
        }

        private void ClearIgnoreMarkers()
        {
            _ignoreMarkers.Clear();
        }

        private List<ExternalEditMarker> GetExternalEditMarkers(SnapshotSpan span)
        {
            var list = new List<ExternalEditMarker>();
            GetExternalEditMarkers(span, list);
            return list;
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
                        ExternalEditMarker marker;

                        if (adapter.TryCreateExternalEditMarker(cur.Tag, tagSpan, out marker))
                        {
                            list.Add(marker);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns all active ExternalEditMarker instances for the given range
        /// </summary>
        private void GetExternalEditMarkersFromShims(SnapshotSpan span, List<ExternalEditMarker> list)
        {
            var markers = _vsTextLines.GetLineMarkers(span.ToTextSpan());
            foreach (var marker in markers)
            {
                foreach (var adapter in _externalEditorAdapters)
                {
                    ExternalEditMarker editMarker;
                    if (adapter.TryCreateExternalEditMarker(marker, _buffer.TextSnapshot, out editMarker))
                    {
                        list.Add(editMarker);
                    }
                }
            }
        }

        private bool ShouldIgnore(ExternalEditMarker marker)
        {
            foreach ( var ignore in _ignoreMarkers)
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
            while (i < _ignoreMarkers.Count)
            {
                if (_ignoreMarkers[i].Span.Snapshot == snapshot)
                {
                    i++;
                    continue;
                }

                var mapped = _ignoreMarkers[i].Span.SafeTranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
                if (mapped.IsValue)
                {
                    _ignoreMarkers[i] = new ExternalEditMarker(_ignoreMarkers[i].ExternalEditKind, mapped.Value);
                }
                else
                {
                    _ignoreMarkers.RemoveAt(i);
                }
                i++;
            }
        }

        internal static void Monitor(IVimBuffer buffer, IVsTextLines vsTextLines, IViewTagAggregatorFactoryService tagAggregatorFactoryService)
        {
            new ExternalEditManager(buffer, vsTextLines, tagAggregatorFactoryService);
        }
    }
}
