using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using EditorUtils;

namespace VsVim.Implementation.ExternalEdit
{
    internal sealed class ExternalEditMonitor
    {
        /// <summary>
        /// What kind of check needs to be performed
        /// </summary>
        internal enum CheckKind
        {
            None = 0,
            Tags = 0x1,
            Markers = 0x2,
            All = Tags | Markers
        }

        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IVimBuffer _buffer;
        private readonly ITextView _textView;
        private readonly IProtectedOperations _protectedOperations;
        private readonly Result<IVsTextLines> _vsTextLines;
        private readonly ReadOnlyCollection<ITagger<ITag>> _taggerCollection;
        private readonly ReadOnlyCollection<IExternalEditAdapter> _externalEditorAdapters;
        private readonly List<ITrackingSpan> _ignoredExternalEditSpans = new List<ITrackingSpan>();
        private CheckKind? _queuedCheckKind;
        private bool _leavingExternalEdit;

        /// <summary>
        /// This is the set of spans in the ITextBuffer which we ignore for detecting new
        /// external edits.  These come about when the user manually ends an external edit
        /// session but there are still active edit spans in the ITextBuffer
        /// </summary>
        internal IEnumerable<ITrackingSpan> IgnoredExternalEditSpans
        {
            get
            {
                return _ignoredExternalEditSpans;
            }
            set
            {
                _ignoredExternalEditSpans.Clear();
                _ignoredExternalEditSpans.AddRange(value);
            }
        }

        internal ExternalEditMonitor(
            IVimApplicationSettings vimApplicationSettings,
            IVimBuffer buffer,
            IProtectedOperations protectedOperations,
            Result<IVsTextLines> vsTextLines,
            ReadOnlyCollection<ITagger<ITag>> taggerCollection,
            ReadOnlyCollection<IExternalEditAdapter> externalEditorAdapters)
        {
            _vimApplicationSettings = vimApplicationSettings;
            _vsTextLines = vsTextLines;
            _protectedOperations = protectedOperations;
            _externalEditorAdapters = externalEditorAdapters;
            _taggerCollection = taggerCollection;
            _buffer = buffer;
            _buffer.TextView.LayoutChanged += OnLayoutChanged;
            _buffer.SwitchedMode += OnSwitchedMode;
            _textView = _buffer.TextView;

            foreach (var tagger in _taggerCollection)
            {
                tagger.TagsChanged += OnTagsChanged;
            }
        }

        internal void Close()
        {
            _buffer.TextView.LayoutChanged -= OnLayoutChanged;
            foreach (var tagger in _taggerCollection)
            {
                tagger.TagsChanged -= OnTagsChanged;
            }
        }

        /// <summary>
        /// A layout change will occur when tags are changed in the ITextBuffer.  We use it as a clue that 
        /// we need to look for external edit tags on markers
        /// </summary>
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            QueueCheck(CheckKind.Markers);
        }

        /// <summary>
        /// When we get a TagsChnaged event we need to queue up an external edit check to examine the
        /// changes
        /// </summary>
        private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
        {
            QueueCheck(CheckKind.Tags);
        }

        /// <summary>
        /// Handles the switch mode event of the ITextBuffer.  We need to monitor this in case 
        /// the user exits external edit mode while edit tags are still around.  These need to 
        /// be saved
        /// </summary>
        private void OnSwitchedMode(object sender, SwitchModeEventArgs args)
        {
            // If we're forcing the leave of external edit then that code is responsible for 
            // saving the ignore markers
            if (_leavingExternalEdit)
            {
                return;
            }

            if (args.PreviousMode.ModeKind == ModeKind.ExternalEdit)
            {
                // If we're in the middle of an external edit and the mode switches then we 
                // need to record the current edit markers so we can ignore them going 
                // forward.  Further updates which cause these markers to be rendered shouldn't
                // cause us to re-enter external edit mode
                SetIgnoredExternalEditSpans(GetExternalEditSpans(CheckKind.All));
            }
        }

        private void SetIgnoredExternalEditSpans(IEnumerable<SnapshotSpan> spans)
        {
            _ignoredExternalEditSpans.Clear();
            _ignoredExternalEditSpans.AddRange(spans.Select(x => x.Snapshot.CreateTrackingSpan(x.Span, SpanTrackingMode.EdgeInclusive)));
        }

        /// <summary>
        /// Perform the specified check against the ITextBuffer
        /// </summary>
        internal void PerformCheck(CheckKind kind)
        {
            if (!_vimApplicationSettings.EnableExternalEditMonitoring)
            {
                return;
            }

            if (kind == CheckKind.None)
            {
                return;
            }

            // If we're in the middle of a layout then there is no sense in checking now as the values
            // will all be invalidated when the layout ends.  Queue one up for later
            if (_buffer.TextView.InLayout)
            {
                QueueCheck(kind);
                return;
            }

            if (_buffer.ModeKind == ModeKind.ExternalEdit)
            {
                CheckForExternalEditEnd();
            }
            else
            {
                CheckForExternalEditStart(kind);
            }
        }

        /// <summary>
        /// Check and see if we should start an external edit operation
        /// </summary>
        private void CheckForExternalEditStart(CheckKind kind)
        {
            Contract.Assert(_buffer.ModeKind != ModeKind.ExternalEdit);

            var externalEditSpans = GetExternalEditSpans(kind);

            // If at some point all of the external edit spans dissapear then we 
            // don't need to track them anymore.  Very important to clear the cache 
            // here as the user could fire up an external edit at the exact same location
            // and we want that to register as an external edit
            if (externalEditSpans.Count == 0)
            {
                _ignoredExternalEditSpans.Clear();
                return;
            }

            // If we should ignore all of the spans then we've not entered an external 
            // edit
            if (externalEditSpans.All(ShouldIgnore))
            {
                return;
            }

            // Clear out the ignored markers.  Everything is fair game again when we restart
            // the external edit
            _ignoredExternalEditSpans.Clear();

            // Not in an external edit and there are edit markers we need to consider.  Time to enter
            // external edit mode
            _buffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
        }

        /// <summary>
        /// Check and see if we should end an external edit operation.
        /// We are already in an external edit then we need to check all tags to determine
        /// if we should be leaving the external edit or not.  Else we only check tags when
        /// we're in an external edit for markers, there are no tags and we bail out 
        /// incorrectly
        /// </summary>
        private void CheckForExternalEditEnd()
        {
            Contract.Assert(_buffer.ModeKind == ModeKind.ExternalEdit);
            Contract.Assert(_ignoredExternalEditSpans.Count == 0);

            var externalEditSpans = GetExternalEditSpans(CheckKind.All);
            if (externalEditSpans.Count == 0)
            {
                // If we're in an external edit and all of the markers leave then transition back to
                // insert mode.  Make sure to mark we are doing this so that we avoid double
                // caching certain values
                _leavingExternalEdit = true;
                try
                {
                    _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                }
                finally
                {
                    _leavingExternalEdit = false;
                }
            }
        }

        /// <summary>
        /// Queue up a check for the specified type here.  If there is already check queued 
        /// this wont' have any effect other than to ensure the specified check is included
        /// in the existing queue
        /// </summary>
        private void QueueCheck(CheckKind kind)
        {
            if (!_vimApplicationSettings.EnableExternalEditMonitoring)
            {
                return;
            }

            if (_queuedCheckKind.HasValue)
            {
                _queuedCheckKind |= kind;
                return;
            }

            Action doCheck =
                () =>
                {
                    var saved = _queuedCheckKind ?? kind;
                    _queuedCheckKind = null;

                    // The ITextView can close in between the time of dispatch and the actual 
                    // execution of the call.  
                    //
                    // In addition to being the right thing to do by bailing out early, there are parts 
                    // of the SHIM layer which can't handle being called after the ITextView is 
                    // called.  EnumMarkers for example will throw a NullReferenceException.
                    if (_textView.IsClosed)
                    {
                        return;
                    }
                    PerformCheck(saved);
                };

            _protectedOperations.BeginInvoke(doCheck, DispatcherPriority.Loaded);
        }

        internal List<SnapshotSpan> GetExternalEditSpans(CheckKind kind)
        {
            var list = new List<SnapshotSpan>();
            GetExternalEditSpans(list, kind);
            return list;
        }

        private void GetExternalEditSpans(List<SnapshotSpan> list, CheckKind kind)
        {
            var collection = _buffer.TextView.GetLikelyVisibleSnapshotSpans();
            foreach (var span in collection)
            {
                if (0 != (kind & CheckKind.Markers))
                {
                    GetExternalEditSpansFromMarkers(span, list);
                }

                if (0 != (kind & CheckKind.Tags))
                {
                    GetExternalEditSpansFromTags(span, list);
                }
            }
        }

        /// <summary>
        /// Get the external edit spans which come from ITag values
        /// </summary>
        private void GetExternalEditSpansFromTags(SnapshotSpan span, List<SnapshotSpan> list)
        {
            if (_taggerCollection.Count == 0)
            {
                return;
            }

            var collection = new NormalizedSnapshotSpanCollection(span);
            foreach (var tagger in _taggerCollection)
            {
                var tags = tagger.GetTags(collection);
                foreach (var cur in tags)
                {
                    foreach (var adapter in _externalEditorAdapters)
                    {
                        if (adapter.IsExternalEditTag(cur.Tag))
                        {
                            list.Add(cur.Span);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns all active ExternalEditMarker instances for the given range for the old style
        /// Visual Studio markers.  It's possible this is a pure 2010 ITextBuffer though hence won't
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

        /// <summary>
        /// Should we ignore this SnapshotSpan when considering it for an external edit?
        /// </summary>
        private bool ShouldIgnore(SnapshotSpan externalEditSpan)
        {
            foreach (var ignoreTrackingSpan in _ignoredExternalEditSpans)
            {
                var ignoreSpan = TrackingSpanUtil.GetSpan(externalEditSpan.Snapshot, ignoreTrackingSpan);
                if (ignoreSpan.IsSome() && ignoreSpan.Value.OverlapsWith(externalEditSpan))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
