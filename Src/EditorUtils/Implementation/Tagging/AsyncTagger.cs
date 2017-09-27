using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed partial class AsyncTagger<TData, TTag> : ITagger<TTag>, IDisposable
        where TTag : ITag
    {
        #region CompleteReason

        private enum CompleteReason
        {
            Finished,
            Cancelled,
            Error
        }

        #endregion

        #region BackgroundCacheData

        /// <summary>
        /// This holds the set of data which is currently known from the background thread.  Data in 
        /// this collection should be considered final for the given Snapshot.  It will only change
        /// if the AsyncTaggerSource itself raises a Changed event (in which case we discard all 
        /// background data).  
        /// </summary>
        internal struct BackgroundCacheData
        {
            internal readonly ITextSnapshot Snapshot;

            /// <summary>
            /// Set of line ranges for which tags are known
            /// </summary>
            internal readonly NormalizedLineRangeCollection VisitedCollection;

            /// <summary>
            /// Set of known tags
            /// </summary>
            internal readonly ReadOnlyCollection<ITagSpan<TTag>> TagList;

            internal SnapshotSpan Span
            {
                get
                {
                    var range = VisitedCollection.OverarchingLineRange;
                    if (!range.HasValue)
                    {
                        return new SnapshotSpan(Snapshot, 0, 0);
                    }

                    var lineRange = new SnapshotLineRange(Snapshot, range.Value.StartLineNumber, range.Value.Count);
                    return lineRange.ExtentIncludingLineBreak;
                }
            }

            internal BackgroundCacheData(SnapshotLineRange lineRange, ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                Snapshot = lineRange.Snapshot;
                VisitedCollection = new NormalizedLineRangeCollection();
                VisitedCollection.Add(lineRange.LineRange);
                TagList = tagList;
            }

            internal BackgroundCacheData(ITextSnapshot snapshot, NormalizedLineRangeCollection visitedCollection, ReadOnlyCollection<ITagSpan<TTag>> tagList)
            {
                Snapshot = snapshot;
                VisitedCollection = visitedCollection;
                TagList = tagList;
            }

            /// <summary>
            /// Determine tag cache state we have for the given SnapshotSpan
            /// </summary>
            internal TagCacheState GetTagCacheState(SnapshotSpan span)
            {
                // If the requested span doesn't even intersect with the overarching SnapshotSpan
                // of the cached data in the background then a more exhaustive search isn't needed
                // at this time
                var cachedSpan = Span;
                if (!cachedSpan.IntersectsWith(span))
                {
                    return TagCacheState.None;
                }

                var lineRange = SnapshotLineRange.CreateForSpan(span);
                var unvisited = VisitedCollection.GetUnvisited(lineRange.LineRange);
                return unvisited.HasValue
                    ? TagCacheState.Partial
                    : TagCacheState.Complete;
            }

            /// <summary>
            /// Create a TrackingCacheData instance from this BackgroundCacheData
            /// </summary>
            internal TrackingCacheData CreateTrackingCacheData()
            {
                // Create the list.  Initiate an ITrackingSpan for every SnapshotSpan present
                var trackingList = TagList.Select(
                    tagSpan =>
                    {
                        var snapshot = tagSpan.Span.Snapshot;
                        var trackingSpan = snapshot.CreateTrackingSpan(tagSpan.Span, SpanTrackingMode.EdgeExclusive);
                        return Tuple.Create(trackingSpan, tagSpan.Tag);
                    })
                    .ToReadOnlyCollection();

                return new TrackingCacheData(
                    Snapshot.CreateTrackingSpan(Span, SpanTrackingMode.EdgeInclusive),
                    trackingList);
            }
        }

        #endregion

        #region TrackingCacheData

        internal struct TrackingCacheData
        {
            internal readonly ITrackingSpan TrackingSpan;
            internal readonly ReadOnlyCollection<Tuple<ITrackingSpan, TTag>> TrackingList;

            internal TrackingCacheData(ITrackingSpan trackingSpan, ReadOnlyCollection<Tuple<ITrackingSpan, TTag>> trackingList)
            {
                TrackingSpan = trackingSpan;
                TrackingList = trackingList;
            }

            internal TrackingCacheData Merge(ITextSnapshot snapshot, TrackingCacheData trackingCacheData)
            {
                var left = TrackingSpan.GetSpanSafe(snapshot);
                var right = trackingCacheData.TrackingSpan.GetSpanSafe(snapshot);
                SnapshotSpan span;
                if (left.HasValue && right.HasValue)
                {
                    span = left.Value.CreateOverarching(right.Value);
                }
                else if (left.HasValue)
                {
                    span = left.Value;
                }
                else if (right.HasValue)
                {
                    span = right.Value;
                }
                else
                {
                    span = new SnapshotSpan(snapshot, 0, 0);
                }
                var trackingSpan = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                var tagList = TrackingList
                    .Concat(trackingCacheData.TrackingList)
                    .Distinct(EqualityUtility.Create<Tuple<ITrackingSpan, TTag>>(
                        (x, y) => x.Item1.GetSpanSafe(snapshot) == y.Item1.GetSpanSafe(snapshot),
                        tuple => tuple.Item1.GetSpanSafe(snapshot).GetHashCode()))
                    .ToReadOnlyCollection();

                return new TrackingCacheData(trackingSpan, tagList);
            }

            /// <summary>
            /// Does this tracking information contain tags over the given span in it's 
            /// ITextSnapshot
            /// </summary>
            internal bool ContainsCachedTags(SnapshotSpan span)
            {
                var snapshot = span.Snapshot;
                var trackingSpan = TrackingSpan.GetSpanSafe(snapshot);
                return trackingSpan.HasValue;
            }

            /// <summary>
            /// Get the cached tags on the given ITextSnapshot
            ///
            /// If this SnapshotSpan is coming from a different snapshot which is ahead of 
            /// our current one we need to take special steps.  If we simply return nothing
            /// and go to the background the tags will flicker on screen.  
            ///
            /// To work around this we try to map the tags to the requested ITextSnapshot. If
            /// it succeeds then we use the mapped values and simultaneously kick off a background
            /// request for the correct ones
            /// </summary>
            internal ReadOnlyCollection<ITagSpan<TTag>> GetCachedTags(ITextSnapshot snapshot)
            {
                // Mapping gave us at least partial information.  Will work for the transition
                // period
                return
                    TrackingList
                    .Select(
                        tuple =>
                        {
                            var itemSpan = tuple.Item1.GetSpanSafe(snapshot);
                            return itemSpan.HasValue
                                ? (ITagSpan<TTag>)new TagSpan<TTag>(itemSpan.Value, tuple.Item2)
                                : null;
                        })
                    .Where(tagSpan => tagSpan != null)
                    .ToReadOnlyCollection();
            }
        }

        #endregion

        #region TagCache

        internal struct TagCache
        {
            internal BackgroundCacheData? BackgroundCacheData;
            internal TrackingCacheData? TrackingCacheData;

            internal bool IsEmpty
            {
                get { return !BackgroundCacheData.HasValue && !TrackingCacheData.HasValue; }
            }

            internal TagCache(BackgroundCacheData? backgroundCacheData, TrackingCacheData? trackingCacheData)
            {
                BackgroundCacheData = backgroundCacheData;
                TrackingCacheData = trackingCacheData;
            }

            internal static TagCache Empty
            {
                get { return new TagCache(null, null); }
            }
        }

        #endregion

        #region TagCacheState

        internal enum TagCacheState
        {
            None,
            Partial,
            Complete
        }

        #endregion

        #region AsyncBackgroundRequest

        internal struct AsyncBackgroundRequest
        {
            internal readonly ITextSnapshot Snapshot;
            internal readonly Channel Channel;
            internal readonly Task Task;
            internal readonly CancellationTokenSource CancellationTokenSource;

            internal AsyncBackgroundRequest(
                ITextSnapshot snapshot,
                Channel channel,
                Task task,
                CancellationTokenSource cancellationTokenSource)
            {
                Snapshot = snapshot;
                CancellationTokenSource = cancellationTokenSource;
                Channel = channel;
                Task = task;
            }
        }

        #endregion

        /// This number was chosen virtually at random.  In extremely large files it's legal
        /// to ask for the tags for the entire file (and sadly very often done).  When this 
        /// happens even an async tagger breaks down a bit.  It won't cause the UI to hang but
        /// it will appear the tagger is broken because it's not giving back any data.  So 
        /// we break the non-visible sections into chunks and process the chunks one at a time
        ///
        /// Note: Even though a section is not visible we must still provide tags.  Gutter 
        /// margins and such still need to see tags for non-visible portions of the buffer
        internal const int DefaultChunkCount = 500;

        /// <summary>
        /// Cached empty tag list
        /// </summary>
        private static readonly ReadOnlyCollection<ITagSpan<TTag>> EmptyTagList = new ReadOnlyCollection<ITagSpan<TTag>>(new List<ITagSpan<TTag>>());

        private readonly IAsyncTaggerSource<TData, TTag> _asyncTaggerSource;
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        /// <summary>
        /// The one and only active AsyncBackgroundRequest instance.  There can be several
        /// in flight at once.  But we will cancel the earlier ones should a new one be 
        /// requested
        /// </summary>
        private AsyncBackgroundRequest? _asyncBackgroundRequest;

        /// <summary>
        /// The current cache of tags we've provided to our consumer
        /// </summary>
        private TagCache _tagCache;

        /// <summary>
        /// The Overarching snapshot span for which we've received GetTags request.  At a glance
        /// it would appear that using a NormalizedLineRangeCollection would be more efficient
        /// here as it more accurately tracks the ranges.  That is true but we only use this 
        /// for the cases where IAsyncTaggerSource declares that it changes.  It's not incorrect
        /// to say the overarching SnapshotSpan has changed in this case.  And it's cheaper
        /// to use the overaching span in the case of odd requests like time travel ones
        /// </summary>
        private SnapshotSpan? _cachedOverarchingRequestSpan = null;

        private int _chunkCount = DefaultChunkCount;

        /// <summary>
        /// The SnapshotSpan for which we have given out tags
        /// </summary>
        internal SnapshotSpan? CachedOverarchingRequestSpan
        {
            get { return _cachedOverarchingRequestSpan; }
            set { _cachedOverarchingRequestSpan = value; }
        }

        /// <summary>
        /// The cache of ITag values
        /// </summary>
        internal TagCache TagCacheData
        {
            get { return _tagCache; }
            set { _tagCache = value; }
        }

        /// <summary>
        /// If there is a background request active this holds the information about it 
        /// </summary>
        internal AsyncBackgroundRequest? AsyncBackgroundRequestData
        {
            get { return _asyncBackgroundRequest; }
            set { _asyncBackgroundRequest = value; }
        }

        internal int ChunkCount
        {
            get { return _chunkCount; }
            set { _chunkCount = value; }
        }

        internal AsyncTagger(IAsyncTaggerSource<TData, TTag> asyncTaggerSource)
        {
            _asyncTaggerSource = asyncTaggerSource;
            _asyncTaggerSource.Changed += OnAsyncTaggerSourceChanged;

            // If there is an ITextView associated with the IAsyncTaggerSource then we want to 
            // listen to LayoutChanges.  If the layout changes while we are getting tags we want
            // to prioritize the visible lines
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                _asyncTaggerSource.TextViewOptional.LayoutChanged += OnLayoutChanged;
            }
        }

        /// <summary>
        /// Given a new tag list determine if the results differ from what we would've been 
        /// returning from our TrackingCacheData over the same SnapshotSpan.  Often times the 
        /// new data is the same as the old hence we don't need to produce any changed information
        /// to the buffer
        /// </summary>
        internal bool DidTagsChange(SnapshotSpan span, ReadOnlyCollection<ITagSpan<TTag>> tagList)
        {
            if (!_tagCache.TrackingCacheData.HasValue || !_tagCache.TrackingCacheData.Value.ContainsCachedTags(span))
            {
                // Nothing in the tracking cache so it changed if there is anything in the new
                // collection.  If the new collection has anything then it changed
                return tagList.Count > 0;
            }

            var trackingCacheData = _tagCache.TrackingCacheData.Value;
            var trackingTagList = trackingCacheData.GetCachedTags(span.Snapshot);
            if (trackingTagList.Count != tagList.Count)
            {
                return true;
            }

            var trackingSet = trackingTagList
                .Select(tagSpan => tagSpan.Span)
                .ToHashSet();

            return tagList.Any(x => !trackingSet.Contains(x.Span));
        }

        /// <summary>
        /// Get the tags for the specified NormalizedSnapshotSpanCollection.  Use the cache if 
        /// possible and possibly go to the background if necessary
        /// </summary>
        internal IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection col)
        {
            // The editor itself will never send an empty collection to GetTags.  But this is an 
            // API and other components are free to call it with whatever values they like
            if (col.Count == 0)
            {
                return EmptyTagList;
            }

            AdjustRequestSpan(col);
            AdjustToSnapshot(col[0].Snapshot);

            if (col.Count == 1)
            {
                return GetTags(col[0]);
            }

            EditorUtilsTrace.TraceInfo("AsyncTagger::GetTags Count {0}", col.Count);
            IEnumerable<ITagSpan<TTag>> all = null;
            foreach (var span in col)
            {
                var current = GetTags(span);
                all = all == null
                    ? current
                    : all.Concat(current);
            }

            return all;
        }

        private IEnumerable<ITagSpan<TTag>> GetTags(SnapshotSpan span)
        {
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            EditorUtilsTrace.TraceInfo("AsyncTagger::GetTags {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            // First try and see if the tagger can provide prompt data.  We want to avoid 
            // creating Task<T> instances if possible.  
            IEnumerable<ITagSpan<TTag>> tagList = EmptyTagList;
            if (!TryGetTagsPrompt(span, out tagList) &&
                !TryGetTagsFromBackgroundDataCache(span, out tagList))
            {
                // The request couldn't be fully satisfied from the cache or prompt data so
                // we will request the data in the background thread
                GetTagsInBackground(span);

                // Since the request couldn't be fully fulfilled by the cache we augment the returned data
                // with our tracking data
                var trackingTagList = GetTagsFromTrackingDataCache(span.Snapshot);
                tagList = tagList == null
                    ? trackingTagList
                    : tagList.Concat(trackingTagList);
            }

            // Now filter the set of returned ITagSpan values to those which are part of the 
            // requested NormalizedSnapshotSpanCollection.  The cache lookups don't dig down and 
            // instead return all available tags.  We filter down the collection here to what's 
            // necessary.
            return tagList.Where(tagSpan => tagSpan.Span.IntersectsWith(span));
        }

        private void Dispose()
        {
            RemoveHandlers();
            var disposable = _asyncTaggerSource as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private void RemoveHandlers()
        {
            _asyncTaggerSource.Changed -= OnAsyncTaggerSourceChanged;
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                _asyncTaggerSource.TextViewOptional.LayoutChanged -= OnLayoutChanged;
            }
        }

        /// <summary>
        /// Try and get the tags promptly from the IAsyncTaggerSource
        /// </summary>
        private bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tagList)
        {
            return _asyncTaggerSource.TryGetTagsPrompt(span, out tagList);
        }

        private bool TryGetTagsFromBackgroundDataCache(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tagList)
        {
            if (!_tagCache.BackgroundCacheData.HasValue || _tagCache.BackgroundCacheData.Value.Snapshot != span.Snapshot)
            {
                tagList = EmptyTagList;
                return false;
            }

            var backgroundCacheData = _tagCache.BackgroundCacheData.Value;
            switch (backgroundCacheData.GetTagCacheState(span))
            {
                case TagCacheState.Complete:
                    tagList = backgroundCacheData.TagList;
                    return true;
                case TagCacheState.Partial:
                    tagList = backgroundCacheData.TagList;
                    return false;
                case TagCacheState.None:
                default:
                    tagList = EmptyTagList;
                    return false;
            }
        }

        private ReadOnlyCollection<ITagSpan<TTag>> GetTagsFromTrackingDataCache(ITextSnapshot snapshot)
        {
            if (!_tagCache.TrackingCacheData.HasValue)
            {
                return EmptyTagList;
            }

            var trackingCacheData = _tagCache.TrackingCacheData.Value;
            return trackingCacheData.GetCachedTags(snapshot);
        }

        /// <summary>
        /// Get the tags for the specified SnapshotSpan in a background task.  If there are outstanding
        /// requests for SnapshotSpan values then this one will take priority over those 
        /// </summary>
        private void GetTagsInBackground(SnapshotSpan span)
        {
            var synchronizationContext = SynchronizationContext.Current;
            if (null == synchronizationContext)
            {
                return;
            }

            // The background processing should now be focussed on the specified ITextSnapshot 
            var snapshot = span.Snapshot;

            // In the majority case GetTags(NormalizedSnapshotCollection) drives this function and 
            // AdjustToSnapshot is already called.  There are other code paths though within AsyncTagger
            // which call this method.  We need to guard against them here.  
            AdjustToSnapshot(snapshot);

            // Our caching and partitioning of data is all done on a line range
            // basis.  Just expand the requested SnapshotSpan to the encompassing
            // SnaphotlineRange
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            span = lineRange.ExtentIncludingLineBreak;

            // If we already have a background task running for this ITextSnapshot then just enqueue this 
            // request onto that existing one.  By this point if the request exists it must be tuned to 
            // this ITextSnapshot
            if (_asyncBackgroundRequest.HasValue)
            {
                var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
                if (asyncBackgroundRequest.Snapshot == snapshot)
                {
                    Contract.Requires(asyncBackgroundRequest.Snapshot == snapshot);
                    EditorUtilsTrace.TraceInfo("AsyncTagger Background Existing {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);
                    asyncBackgroundRequest.Channel.WriteNormal(lineRange);
                    return;
                }

                CancelAsyncBackgroundRequest();
            }

            Contract.Assert(!_asyncBackgroundRequest.HasValue);
            EditorUtilsTrace.TraceInfo("AsyncTagger Background New {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            // Create the data which is needed by the background request
            var data = _asyncTaggerSource.GetDataForSnapshot(snapshot);
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var channel = new Channel();
            channel.WriteNormal(lineRange);

            // If there is an ITextView then make sure it is requested as well.  If the source provides an 
            // ITextView then it is always prioritized on requests for a new snapshot
            if (_asyncTaggerSource.TextViewOptional != null)
            {
                var visibleLineRange = _asyncTaggerSource.TextViewOptional.GetVisibleSnapshotLineRange();
                if (visibleLineRange.HasValue)
                {
                    channel.WriteVisibleLines(visibleLineRange.Value);
                }
            }

            // The background thread needs to know the set of values we have already queried 
            // for.  Send a copy since this data will be mutated from a background thread
            NormalizedLineRangeCollection localVisited;
            if (_tagCache.BackgroundCacheData.HasValue)
            {
                var backgroundCacheData = _tagCache.BackgroundCacheData.Value;
                Contract.Requires(backgroundCacheData.Snapshot == snapshot);
                localVisited = backgroundCacheData.VisitedCollection.Copy();
            }
            else
            {
                localVisited = new NormalizedLineRangeCollection();
            }

            // Function which finally gets the tags.  This is run on a background thread and can
            // throw as the implementor is encouraged to use CancellationToken::ThrowIfCancelled
            var localAsyncTaggerSource = _asyncTaggerSource;
            var localChunkCount = _chunkCount;
            Action getTags = () => GetTagsInBackgroundCore(
                localAsyncTaggerSource,
                data,
                localChunkCount,
                channel,
                localVisited,
                cancellationToken,
                (completeReason) => synchronizationContext.Post(_ => OnGetTagsInBackgroundComplete(completeReason, channel, cancellationTokenSource), null),
                (processedLineRange, tagList) => synchronizationContext.Post(_ => OnGetTagsInBackgroundProgress(cancellationTokenSource, processedLineRange, tagList), null));

            // Create the Task which will handle the actual gathering of data.  If there is a delay
            // specified use it
            var localDelay = _asyncTaggerSource.Delay;
            Action taskAction =
                () =>
                {
                    if (localDelay.HasValue)
                    {
                        Thread.Sleep(localDelay.Value);
                    }

                    getTags();
                };

            var task = new Task(taskAction, cancellationToken);
            _asyncBackgroundRequest = new AsyncBackgroundRequest(
                span.Snapshot,
                channel,
                task,
                cancellationTokenSource);

            task.Start();
        }

        [UsedInBackgroundThread]
        private static void GetTagsInBackgroundCore(
            IAsyncTaggerSource<TData, TTag> asyncTaggerSource,
            TData data,
            int chunkCount,
            Channel channel,
            NormalizedLineRangeCollection visited,
            CancellationToken cancellationToken,
            Action<CompleteReason> onComplete,
            Action<SnapshotLineRange, ReadOnlyCollection<ITagSpan<TTag>>> onProgress)
        {
            CompleteReason completeReason;
            try
            {
                // Keep track of the LineRange values which we've already provided tags for.  Don't 
                // duplicate the work
                var toProcess = new Queue<SnapshotLineRange>();

                // *** This value can be wrong *** 
                // This is the version number we expect the Channel to have.  It's used
                // as a hueristic to determine if we should prioritize a value off of the stack or our
                // local stack.  If it's wrong it means we prioritize the wrong value.  Not a bug it
                // just changes the order in which values will appear
                var versionNumber = channel.CurrentVersion;

                // Take one value off of the threadedLineRangeStack value.  If the value is bigger than
                // our chunking increment then we will add the value in chunks to the toProcess queue
                Action popOne =
                    () =>
                    {
                        var value = channel.Read();
                        if (!value.HasValue)
                        {
                            return;
                        }

                        var lineRange = value.Value;
                        if (lineRange.Count <= chunkCount)
                        {
                            toProcess.Enqueue(lineRange);
                            return;
                        }

                        var snapshot = lineRange.Snapshot;
                        var startLineNumber = lineRange.StartLineNumber;
                        while (startLineNumber <= lineRange.LastLineNumber)
                        {
                            var startLine = snapshot.GetLineFromLineNumber(startLineNumber);
                            var localRange = SnapshotLineRange.CreateForLineAndMaxCount(startLine, chunkCount);
                            toProcess.Enqueue(localRange);
                            startLineNumber += chunkCount;
                        }
                    };

                // Get the tags for the specified SnapshotLineRange and return the results.  No chunking is done here,
                // the data is just directly processed
                Action<SnapshotLineRange> getTags =
                    tagLineRange =>
                    {
                        var unvisited = visited.GetUnvisited(tagLineRange.LineRange);
                        if (unvisited.HasValue)
                        {
                            var tagList = EmptyTagList;
                            try
                            {
                                tagLineRange = SnapshotLineRange.CreateForLineNumberRange(tagLineRange.Snapshot, unvisited.Value.StartLineNumber, unvisited.Value.LastLineNumber).Value;
                                tagList = asyncTaggerSource.GetTagsInBackground(data, tagLineRange.ExtentIncludingLineBreak, cancellationToken);
                            }
                            catch (Exception e)
                            {
                                // Ignore exceptions that are thrown by IAsyncTaggerSource.  If the tagger threw then we consider
                                // the tags to be nothing for this span.  
                                //
                                // It's important that we register some value here.  If we register nothing then the foreground will
                                // never see this slot as fulfilled and later requests for this span will eventually queue up another
                                // background request
                                EditorUtilsTrace.TraceInfo("AsyncTagger source exception in background processing {0}", e);
                            }
                            visited.Add(tagLineRange.LineRange);
                            onProgress(tagLineRange, tagList);
                        }
                    };

                do
                {
                    versionNumber = channel.CurrentVersion;
                    popOne();

                    // We've drained both of the sources of input hence we are done
                    if (0 == toProcess.Count)
                    {
                        break;
                    }

                    while (0 != toProcess.Count)
                    {
                        // If at any point the threadLineRangeStack value changes we consider the new values to have 
                        // priority over the old ones
                        if (versionNumber != channel.CurrentVersion)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var lineRange = toProcess.Dequeue();
                        getTags(lineRange);
                    }

                } while (!cancellationToken.IsCancellationRequested);

                completeReason = cancellationToken.IsCancellationRequested
                    ? CompleteReason.Cancelled
                    : CompleteReason.Finished;
            }
            catch (OperationCanceledException)
            {
                // Don't report cancellation exceptions.  These are thrown during cancellation for fast
                // break from the operation.  It's really a control flow mechanism
                completeReason = CompleteReason.Cancelled;
            }
            catch (Exception e)
            {
                // Handle cancellation exceptions and everything else.  Don't want an errant 
                // exception thrown by the IAsyncTaggerSource to crash the process
                EditorUtilsTrace.TraceInfo("AsyncTagger Exception in background processing {0}", e);
                completeReason = CompleteReason.Error;
            }

            onComplete(completeReason);
        }

        /// <summary>
        /// Cancel the pending AsyncBackgoundRequest if one is currently running
        /// </summary>
        private void CancelAsyncBackgroundRequest()
        {
            if (_asyncBackgroundRequest.HasValue)
            {
                // Use a try / with to protect the Cancel from throwing and taking down the process
                try
                {
                    var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
                    if (!asyncBackgroundRequest.CancellationTokenSource.IsCancellationRequested)
                    {
                        asyncBackgroundRequest.CancellationTokenSource.Cancel();
                    }
                }
                catch (Exception)
                {

                }

                _asyncBackgroundRequest = null;
            }
        }

        private void AdjustRequestSpan(NormalizedSnapshotSpanCollection col)
        {
            if (col.Count > 0)
            {
                // Note that we only use the overarching span to track what data we are responsible 
                // for in a
                var requestSpan = col.GetOverarchingSpan();
                _cachedOverarchingRequestSpan = TaggerUtil.AdjustRequestedSpan(_cachedOverarchingRequestSpan, requestSpan);
            }
        }

        /// <summary>
        /// The background processing is now focussed on the given ITextSnapshot.  If anything is 
        /// focused on the old ITextSnapshot move it to the specified one.
        /// </summary>
        private void AdjustToSnapshot(ITextSnapshot snapshot)
        {
            // First check and see if we need to move the existing background data to tracking data
            if (_tagCache.BackgroundCacheData.HasValue && _tagCache.BackgroundCacheData.Value.Snapshot != snapshot)
            {
                var backgroundCacheData = _tagCache.BackgroundCacheData.Value;
                var trackingCacheData = backgroundCacheData.CreateTrackingCacheData();
                if (_tagCache.TrackingCacheData.HasValue)
                {
                    trackingCacheData = trackingCacheData.Merge(snapshot, _tagCache.TrackingCacheData.Value);
                }

                _tagCache = new TagCache(null, trackingCacheData);
            }

            // Next cancel any existing background request if it's not focused on this ITextSnapshot
            if (_asyncBackgroundRequest.HasValue && _asyncBackgroundRequest.Value.Snapshot != snapshot)
            {
                CancelAsyncBackgroundRequest();
            }
        }

        private void RaiseTagsChanged(SnapshotSpan span)
        {
            var lineRange = SnapshotLineRange.CreateForSpan(span);
            EditorUtilsTrace.TraceInfo("AsyncTagger::RaiseTagsChanged {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber);

            if (_tagsChanged != null)
            {
                _tagsChanged(this, new SnapshotSpanEventArgs(span));
            }
        }

        /// <summary>
        /// Called when the IAsyncTaggerSource raises a Changed event.  Clear out the 
        /// cache, pass on the event to the ITagger and wait for the next request
        /// </summary>
        private void OnAsyncTaggerSourceChanged(object sender, EventArgs e)
        {
            // Clear out the cache.  It's no longer valid.
            _tagCache = TagCache.Empty;
            CancelAsyncBackgroundRequest();

            // Now if we've previously had a SnapshotSpan requested via GetTags go ahead
            // and tell the consumers that it's changed.  Use the entire cached request
            // span here.  We're pessimistic when we have a Changed call because we have
            // no information on what could've changed
            if (_cachedOverarchingRequestSpan.HasValue)
            {
                RaiseTagsChanged(_cachedOverarchingRequestSpan.Value);
            }
        }

        /// <summary>
        /// If the Layout changes while we are in the middle of getting tags we want to 
        /// prioritize the new set of visible lines.
        /// </summary>
        private void OnLayoutChanged(object sender, EventArgs e)
        {
            if (!_asyncBackgroundRequest.HasValue || _asyncTaggerSource.TextViewOptional == null)
            {
                return;
            }

            var visibleLineRange = _asyncTaggerSource.TextViewOptional.GetVisibleSnapshotLineRange();
            var asyncBackgroundRequest = _asyncBackgroundRequest.Value;
            if (visibleLineRange.HasValue && visibleLineRange.Value.Snapshot == asyncBackgroundRequest.Snapshot)
            {
                asyncBackgroundRequest.Channel.WriteVisibleLines(visibleLineRange.Value);
            }
        }

        /// <summary>
        /// Is the async operation with the specified CancellationTokenSource the active 
        /// background request
        /// </summary>
        private bool IsActiveBackgroundRequest(CancellationTokenSource cancellationTokenSource)
        {
            return _asyncBackgroundRequest.HasValue && _asyncBackgroundRequest.Value.CancellationTokenSource == cancellationTokenSource;
        }

        /// <summary>
        /// Called on the main thread when the request for tags has processed at least a small 
        /// section of the file.  This funtion may be called many times for a single background 
        /// request
        ///
        /// Called on the main thread
        /// </summary>
        private void OnGetTagsInBackgroundProgress(CancellationTokenSource cancellationTokenSource, SnapshotLineRange lineRange, ReadOnlyCollection<ITagSpan<TTag>> tagList)
        {
            if (!IsActiveBackgroundRequest(cancellationTokenSource))
            {
                return;
            }

            // Merge the existing background data if it's present and on the same ITextSnapshot
            BackgroundCacheData newData;
            if (_tagCache.BackgroundCacheData.HasValue && _tagCache.BackgroundCacheData.Value.Snapshot == lineRange.Snapshot)
            {
                var oldData = _tagCache.BackgroundCacheData.Value;
                var tags = oldData.TagList.Concat(tagList).ToReadOnlyCollection();
                oldData.VisitedCollection.Add(lineRange.LineRange);
                newData = new BackgroundCacheData(lineRange.Snapshot, oldData.VisitedCollection, tags);
            }
            else
            {
                newData = new BackgroundCacheData(lineRange, tagList);
            }

            _tagCache = new TagCache(newData, _tagCache.TrackingCacheData);

            // Determine if the tags changed on the given Span.  In an edit it's very possible and likely
            // that the ITagSpan we returned by simply mapping the SnapshotSpan forward was correct.  If 
            // so then for a given SnapshotSpan we've already returned a result which was correct.  Raising
            // TagsChanged again for that SnapshotSpan will cause needless work to ocur (and potentially
            // more layouts
            var span = lineRange.ExtentIncludingLineBreak;
            if (DidTagsChange(span, tagList))
            {
                RaiseTagsChanged(span);
            }
        }

        /// <summary>
        /// Called when the background request is completed
        ///
        /// Called on the main thread
        /// </summary>
        private void OnGetTagsInBackgroundComplete(CompleteReason reason, Channel channel, CancellationTokenSource cancellationTokenSource)
        {
            if (!IsActiveBackgroundRequest(cancellationTokenSource))
            {
                return;
            }

            // The request is complete.  Reset the active request information
            CancelAsyncBackgroundRequest();

            // Update the tag cache to indicate we are no longer doing any tracking edits
            _tagCache = new TagCache(_tagCache.BackgroundCacheData, null);

            // There is one race condition we must deal with here.  It is possible to get requests in the following
            // order 
            //
            //  - F GetTags span1
            //  - B Process span1 
            //  - B Complete span1
            //  - F GetTags span2 (adds to existing queue)
            //  - F Get notified that background complete
            //
            // The good news is any data that is missed will still be in threadedLineRangeStack.  So we just need to
            // drain this value and re-request the data 
            //
            // We own the stack at this point so just access it directly
            var stack = channel.CurrentStack;
            if (!stack.IsEmpty && reason == CompleteReason.Finished)
            {
                var list = new List<SnapshotSpan>();
                while (!stack.IsEmpty)
                {
                    GetTagsInBackground(stack.Value.ExtentIncludingLineBreak);
                    stack = stack.Pop();
                }
            }
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return GetTags(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagsChanged += value; }
            remove { _tagsChanged -= value; }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion
    }
}
