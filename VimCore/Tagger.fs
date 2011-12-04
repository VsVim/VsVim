namespace Vim

open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Threading
open System.Threading.Tasks

/// Structure for passing a single value between threads in a queue like
/// fashion.  Later values will replace earlier ones.
[<UsedInBackgroundThread>]
type SingleItemQueue<'T>() = 
    let _value : ('T option) ref = ref None

    member x.Enqueue value = 
        Interlocked.Exchange(_value, Some value) |> ignore

    member x.Dequeue() = 
        Interlocked.Exchange(_value, None) 

module TaggerUtil = 

    /// The simple taggers when changed need to provide an initial SnapshotSpan 
    /// for the TagsChanged event.  It's important that this SnapshotSpan be kept as
    /// small as possible.  If it's incorrectly large it can have a negative performance
    /// impact on the editor.  In particular
    ///
    /// 1. The value is directly provided in ITagAggregator<T>::TagsChanged.  This value
    ///    is acted on directly by many editor components.  Providing a large range 
    ///    unnecessarily increases their work load.
    /// 2. It can cause a ripple effect in Visual Studio 2010 RTM.  The SnapshotSpan 
    ///    returned will be immediately be the vale passed to GetTags for every other
    ///    ITagger<T> in the system (TextMarkerVisualManager issue). 
    ///
    /// In order to provide the minimum possible valid SnapshotSpan the simple taggers
    /// cache the overarching SnapshotSpan for the latest ITextSnapshot of all requests
    /// to which they are given.
    let AdjustRequestSpan (cachedRequestSpan : SnapshotSpan option) (requestSpan : SnapshotSpan) =
        match cachedRequestSpan with 
        | None -> Some requestSpan
        | Some cachedRequestSpan -> 
            if cachedRequestSpan.Snapshot = requestSpan.Snapshot then
                SnapshotSpanUtil.CreateOverarching cachedRequestSpan requestSpan |> Some
            else
                Some requestSpan

/// A tagger source for asynchronous taggers.  Methods on this interface can and will
/// be called on any thread
type IAsyncTaggerSource<'TData, 'TTag when 'TTag :> ITag> = 

    inherit System.IDisposable

    /// Delay in milliseconds which should occur between the call to GetTags and the kicking off
    /// of a background task
    abstract Delay : int option

    /// The current Snapshot.  
    ///
    /// Called from the main thread only
    abstract TextSnapshot : ITextSnapshot

    /// The current ITextView if this tagger is attached to a ITextView.
    ///
    /// Called from the main thread only
    abstract TextView : ITextView option

    /// To prevent needless spawning of Task<T> values the async tagger has the option
    /// of providing prompt data.  This method should only be used when determination
    /// of the tokens requires no calculation.  It will be called on the main thread
    ///
    /// Called from the main thread only
    abstract GetTagsPrompt : span : SnapshotSpan -> ITagSpan<'TTag> list option

    /// This method is called to gather data on the UI thread which will then be passed
    /// down to the background thread for processing
    ///
    /// Called from the main thread only
    abstract GetDataForSpan : span : SnapshotSpan -> 'TData

    /// Return the applicable tags for the given SnapshotSpan instance.  This will be
    /// called on a background thread and should respect the provided CancellationToken
    [<UsedInBackgroundThread>]
    abstract GetTagsInBackground : data : 'TData -> span : SnapshotSpan -> cancellationToken : CancellationToken -> ITagSpan<'TTag> list

    /// Raised by the source when the underlying source has changed.  All previously
    /// provided data should be considered incorrect after this event
    [<CLIEvent>]
    abstract Changed : IEvent<unit>

type BackgroundCacheData<'TTag when 'TTag :> ITag> = { 

    Span : SnapshotSpan

    TagList : ITagSpan<'TTag> list
}

    with

    member x.Snapshot = x.Span.Snapshot

type TrackingCacheData<'TTag when 'TTag :> ITag> = {

    TrackingSpan : ITrackingSpan

    TrackingList : (ITrackingSpan * 'TTag) list
}

    with

    member x.Merge snapshot (trackingCacheData : TrackingCacheData<'TTag>) =
        let trackingSpan = 
            let left = TrackingSpanUtil.GetSpan snapshot x.TrackingSpan
            let right = TrackingSpanUtil.GetSpan snapshot trackingCacheData.TrackingSpan
            let span = 
                match left, right with
                | Some left, Some right -> SnapshotSpanUtil.CreateOverarching left right
                | Some left, None -> left
                | None, Some right -> right
                | _ -> SnapshotSpan(snapshot, 0, 0)
            TrackingSpanUtil.Create span SpanTrackingMode.EdgeInclusive

        let tagList = 
            x.TrackingList
            |> Seq.append trackingCacheData.TrackingList
            |> Seq.distinctBy (fun (trackingSpan, tag) -> TrackingSpanUtil.GetSpan snapshot trackingSpan)
            |> List.ofSeq

        { TrackingSpan = trackingSpan; TrackingList = tagList }

/// Caches information about the tag values we've given out
[<RequireQualifiedAccess>]
type TagCache<'TTag when 'TTag :> ITag> =

    /// There is currently no cache
    | None

    /// Cache of a GetTagsInBackground call.  Maintains the requested SnapshotSpan and the
    /// set of returned ITagSpan<'TTag> values
    | BackgroundCache of BackgroundCacheData<'TTag>

    /// In the time between kicking off a background request due to an edit and the edit
    /// completing we track the previous BackgroundCache via an ITrackingSpan.  This provides
    /// a manner of visibility until the request completes
    | TrackingCache of TrackingCacheData<'TTag>

    /// During an extentded request post edit we may have both tracking and final data.  This
    /// holds that stage
    | TrackingAndBackgroundCache of TrackingCacheData<'TTag> * BackgroundCacheData<'TTag>

/// Data about a background request for tags
type AsyncBackgroundRequest = {

    LineRange : SnapshotLineRange

    CancellationTokenSource : CancellationTokenSource

    PriorityLineRangeQueue : SingleItemQueue<SnapshotLineRange>

    Task : Task
}

    with

    member x.Span = x.LineRange.ExtentIncludingLineBreak

[<RequireQualifiedAccess>]
type TagResult<'TTag when 'TTag :> ITag> =
    | None
    | Partial of ITagSpan<'TTag> list
    | Complete of ITagSpan<'TTag> list

    with 

    member x.TagList =
        match x with
        | None -> List.empty
        | Partial tagList -> tagList
        | Complete tagList -> tagList

type AsyncTagger<'TData, 'TTag when 'TTag :> ITag>
    (
        _asyncTaggerSource : IAsyncTaggerSource<'TData, 'TTag>
    ) as this =

    /// Number of lines on either side of a request for which we will cache background
    /// data
    [<Literal>]
    static let LineAdjustment = 40

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()

    /// The one and only active AsyncBackgroundRequest instance.  There can be several
    /// in flight at once.  But we will cancel the earlier ones should a new one be 
    /// requested
    let mutable _asyncBackgroundRequest : AsyncBackgroundRequest option = None

    /// Cache of ITag values for a given SnapshotSpan
    let mutable _tagCache = TagCache<'TTag>.None

    /// The SnapshotSpan for which we've provided information via GetTags
    let mutable _cachedRequestSpan : SnapshotSpan option = None

    do 
        _asyncTaggerSource.Changed 
        |> Observable.subscribe this.OnAsyncSourceChanged
        |> _eventHandlers.Add

        // If there is an ITextView associated with the IAsyncTaggerSource then we want to 
        // listen to LayoutChanges.  If the layout changes while we are getting tags we want
        // to prioritize the visible lines
        match _asyncTaggerSource.TextView with
        | None -> ()
        | Some textView ->
            textView.LayoutChanged
            |> Observable.subscribe this.OnLayoutChanged
            |> _eventHandlers.Add 

    /// The SnapshotSpan for which we have given out tags
    member x.CachedRequestSpan 
        with get() = _cachedRequestSpan
        and set value = _cachedRequestSpan <- value

    /// The cache of ITag<'TTag> values
    member x.TagCache 
        with get() = _tagCache
        and set value = _tagCache <- value

    /// If there is a background request active this holds the information about it 
    member x.AsyncBackgroundRequest 
        with get() = _asyncBackgroundRequest
        and set value = _asyncBackgroundRequest <- value

    /// Get the tags for the specified NormalizedSnapshotSpanCollection.  Use the cache if 
    /// possible and possibly go to the background if necessary
    member x.GetTags (col : NormalizedSnapshotSpanCollection) = 
        let span = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
        x.AdjustRequestSpan span

        let tagList = 

            // First try and see if the tagger can provide prompt data.  We want to avoid 
            // creating Task<T> instances if possible.  
            match x.GetTagsPrompt span with
            | Some tagList -> tagList
            | None ->

                // Next step is to hit the cache
                match x.GetTagsFromCache span with
                | TagResult.None -> 

                    // Nothing was in the cache.  Kick off a background request to get it
                    // and return and empty list for now
                    x.GetTagsInBackground span
                    List.empty
                | TagResult.Partial tagList ->

                    // Tag list was partially avaliable.  Kick off a request to get the
                    // complete data and return what we have for now
                    x.GetTagsInBackground span
                    tagList
                | TagResult.Complete tagList ->
                    tagList

        // Now filter the set of returned ITagSpan values to those which are part of the 
        // requested NormalizedSnapshotSpanCollection.  The cache lookups don't dig down and 
        // instead return all available tags.  We filter down the collection here to what's 
        // necessary.
        tagList
        |> Seq.filter (fun tagSpan -> tagSpan.Span.IntersectsWith(span))

    /// Get the tags from the IAsyncTaggerSource
    member x.GetTagsFromSource span = 
        TagResult.None

    /// Get the tags promptly from the IAsyncTaggerSource
    member x.GetTagsPrompt span = 
        _asyncTaggerSource.GetTagsPrompt span 

    /// Get the tags from the cache.  
    member x.GetTagsFromCache (span : SnapshotSpan) = 

        // Update the cache to the given ITextSnapshot
        x.MaybeUpdateTagCacheToSnapshot span.Snapshot

        let getFromBackgroundCacheData (backgroundCacheData : BackgroundCacheData<'TTag>) =
            let cachedSpan = backgroundCacheData.Span
            if cachedSpan.Contains(span) then
                TagResult.Complete backgroundCacheData.TagList
            elif cachedSpan.IntersectsWith(span) then

                // The requested span is at least partially within the cached region.  Return 
                // the data that is available and schedule a background request to get the 
                // rest
                TagResult.Partial backgroundCacheData.TagList
            else
                TagResult.None

        match _tagCache with 
        | TagCache.None ->
            TagResult.None
        | TagCache.BackgroundCache backgroundCacheData ->
            getFromBackgroundCacheData backgroundCacheData
        | TagCache.TrackingCache trackingCacheData ->
            x.GetTagsFromTrackingCacheData span trackingCacheData
        | TagCache.TrackingAndBackgroundCache (trackingCacheData, backgroundCacheData) ->

            // We are in the middle of processing a background request.  We have both the
            // tracking data and the partial background data.  During the transition we should
            // pull first from background and then from tracking 
            let tagResult = getFromBackgroundCacheData backgroundCacheData
            match tagResult with
            | TagResult.Complete _ -> tagResult
            | TagResult.Partial backgroundTagList -> 
                // Merge tracking and background daat. 
                let trackingResult = x.GetTagsFromTrackingCacheData span trackingCacheData
                List.append backgroundTagList trackingResult.TagList |> TagResult.Partial
            | TagResult.None -> x.GetTagsFromTrackingCacheData span trackingCacheData

    /// Get the tags from the edit tracking data which intersect with the requested SnapshotSpan
    member x.GetTagsFromTrackingCacheData span (trackingCacheData : TrackingCacheData<'TTag>) =

        // If this SnapshotSpan is coming from a different snapshot which is ahead of 
        // our current one we need to take special steps.  If we simply return nothing
        // and go to the background the tags will flicker on screen.  
        //
        // To work around this we try to map the tags to the requested ITextSnapshot. If
        // it succeeds then we use the mapped values and simultaneously kick off a background
        // request for the correct ones
        let snapshot = SnapshotSpanUtil.GetSnapshot span
        match TrackingSpanUtil.GetSpan snapshot trackingCacheData.TrackingSpan with
        | None -> TagResult.None
        | Some mappedSpan ->
            if mappedSpan.IntersectsWith(span) then
                // Mapping gave us at least partial information.  Will work for the transition
                // period
                trackingCacheData.TrackingList
                |> Seq.map (fun (trackingSpan, tag) ->
                    match TrackingSpanUtil.GetSpan span.Snapshot trackingSpan with
                    | None -> None
                    | Some tagSpan -> TagSpan<'TTag>(tagSpan, tag) :> ITagSpan<'TTag> |> Some)
                |> SeqUtil.filterToSome
                |> List.ofSeq
                |> TagResult.Partial
            else
                TagResult.None

    /// Get the tags for the specified SnapshotSpan in a background task
    member x.GetTagsInBackground (span : SnapshotSpan) = 

        let startRequest (synchronizationContext : SynchronizationContext) = 

            /// Our caching and partitioning of data is all done on a line range
            /// basis.  Just expand the requested SnapshotSpan to the encompassing
            /// SnaphotlineRange
            let lineRange = SnapshotLineRangeUtil.CreateForSpan span
            let span = lineRange.ExtentIncludingLineBreak

            // Create the data which is needed by the background request
            let data = _asyncTaggerSource.GetDataForSpan span
            let cancellationTokenSource = new CancellationTokenSource()
            let cancellationToken = cancellationTokenSource.Token

            // Create the priority queue for visible lines
            let priorityLineRangeQueue = SingleItemQueue<SnapshotLineRange>()
            match OptionUtil.map2 TextViewUtil.GetVisibleSnapshotLineRange _asyncTaggerSource.TextView with
            | None -> ()
            | Some visibleLineRange -> priorityLineRangeQueue.Enqueue visibleLineRange

            // Function which finally gets the tags.  This is run on a background thread and can
            // throw as the implementor is encouraged to use CancellationToken::ThrowIfCancelled
            let getTags () = 
                let onComplete () = synchronizationContext.Post((fun _ -> x.OnGetTagsInBackgroundComplete cancellationTokenSource), null)
                let onProgress lineRange tagList = synchronizationContext.Post((fun _ -> x.OnGetTagsInBackgroundProgress cancellationTokenSource lineRange tagList), null)
                x.GetTagsInBackgroundCore data lineRange priorityLineRangeQueue cancellationToken onComplete onProgress

            // Create the Task which will handle the actual gathering of data.  If there is a delay
            // specified use it
            let task = 
                match _asyncTaggerSource.Delay with
                | None -> new Task(getTags, cancellationToken)
                | Some delay ->
                    let parent = new Task((fun () -> Thread.Sleep(delay)), cancellationToken)
                    parent.ContinueWith(System.Action<Task>(fun _ -> getTags()), cancellationToken) |> ignore
                    parent

            let asyncBackgroundRequest = {
                LineRange = lineRange
                CancellationTokenSource = cancellationTokenSource
                PriorityLineRangeQueue = priorityLineRangeQueue
                Task = task }
            _asyncBackgroundRequest <- Some asyncBackgroundRequest

            // Now kick off the request
            task.Start()

        let synchronizationContext = SynchronizationContext.Current
        if synchronizationContext <> null then
            match _asyncBackgroundRequest with
            | None -> startRequest synchronizationContext
            | Some asyncBackgroundRequest ->

                // Don't spawn a new task if the existing one is a superset of the current request
                if span.Start.Position < asyncBackgroundRequest.Span.Start.Position ||
                   span.End.Position > asyncBackgroundRequest.Span.End.Position ||
                   span.Snapshot <> asyncBackgroundRequest.Span.Snapshot then

                   x.CancelAsyncBackgroundRequest()
                   startRequest synchronizationContext

    member x.GetTagsInBackgroundCore data (lineRange : SnapshotLineRange) (priorityQueue : SingleItemQueue<SnapshotLineRange>) (cancellationToken : CancellationToken) onComplete onProgress =

        try
            // This number was chosen virtually at random.  In extremely large files it's legal
            // to ask for the tags for the inter file (and sadly very often done).  When this 
            // happens even an async tagger breaks down a bit.  It won't cause the UI to hang but
            // it will appear the tagger is broken because it's not giving back any data.  So 
            // we break the non-visible sections into chunks and process the chunks one at a time
            //
            // Note: Even though a section is not visible we must still provide tags.  Gutter 
            // margins and such still need to see tags for non-visible portions of the buffer
            let chunkCount = 1000 

            // Keep track of the LineRange values which we've already provided tags for.  Don't 
            // duplicate the work
            let visited = LineRangeVisited()

            // Get the tags for the specified SnapshotSpan
            let getTags (lineRange : SnapshotLineRange) = 
                match visited.GetUnvisited lineRange.LineRange with
                | None -> ()
                | Some unvisited ->
                    let lineRange = SnapshotLineRangeUtil.CreateForLineNumberRange lineRange.Snapshot unvisited.StartLineNumber unvisited.LastLineNumber
                    let span = lineRange.ExtentIncludingLineBreak
                    let tagList = 
                        try
                            _asyncTaggerSource.GetTagsInBackground data span cancellationToken
                        with 
                            _ -> List.empty

                    visited.Add lineRange.LineRange
                    onProgress lineRange tagList

            // Get the tags in the specified range in chunks
            let getTagsByChunk (lineRange : SnapshotLineRange) =
                let snapshot = lineRange.Snapshot
                let mutable i = lineRange.StartLineNumber
                while i < lineRange.LastLineNumber do
                    cancellationToken.ThrowIfCancellationRequested()

                    match priorityQueue.Dequeue() with
                    | Some priorityLineRange -> getTags priorityLineRange
                    | None ->
                        let endLineNumber = min lineRange.LastLineNumber (i + chunkCount)
                        let chunkLineRange = SnapshotLineRangeUtil.CreateForLineNumberRange snapshot i endLineNumber
                        getTags chunkLineRange
    
                        i <- i + chunkCount

            // It's common in Visual Studio RTM for the entire SnapshotSpan of the ITextSnapshot
            // to be requested due to a bug.  In very large files this can cause a significant delay
            // in showing tags.  So we prioritize the visible lines and then move on to ones which
            // are not visible
            if lineRange.Count > (chunkCount * 2) then
                getTagsByChunk lineRange
            else 
                getTags lineRange
        with
            // Handle cancellation exceptions and everything else.  Don't want an errant 
            // exception thrown by the IAsyncTaggerSource to crash the process
            _ -> ()

        onComplete ()

    /// Cancel the pending AsyncBackgoundRequest if one is currently running
    member x.CancelAsyncBackgroundRequest() =
        match _asyncBackgroundRequest with 
        | None -> ()
        | Some asyncBackgroundRequest ->

            // Use a try / with to protect the Cancel from throwing and taking down the process
            try
                if not asyncBackgroundRequest.CancellationTokenSource.IsCancellationRequested then
                    asyncBackgroundRequest.CancellationTokenSource.Cancel()
            with 
                _ -> ()
            _asyncBackgroundRequest <- None

    member x.AdjustRequestSpan (requestSpan : SnapshotSpan) =
        _cachedRequestSpan <- TaggerUtil.AdjustRequestSpan _cachedRequestSpan requestSpan

    /// Potentially update the TagCache to the given ITextSnapshot
    member x.MaybeUpdateTagCacheToSnapshot (snapshot : ITextSnapshot) = 

        let createTrackingCacheData (backgroundCacheData : BackgroundCacheData<'TTag>) =
            let cacheSpan = backgroundCacheData.Span

            // Create the list.  Initiate an ITrackingSpan for every SnapshotSpan present
            let trackingList = 
                backgroundCacheData.TagList
                |> List.map (fun tagSpan ->
                    let trackingSpan = TrackingSpanUtil.Create tagSpan.Span SpanTrackingMode.EdgeExclusive
                    (trackingSpan, tagSpan.Tag))

            let trackingSpan = TrackingSpanUtil.Create cacheSpan SpanTrackingMode.EdgeInclusive
            let trackingCacheData = {
                TrackingSpan = trackingSpan
                TrackingList = trackingList 
            }

            trackingCacheData

        match _tagCache with
        | TagCache.BackgroundCache backgroundCacheData ->

            // Change the background cache to a tracking one if an edit occurred
            if backgroundCacheData.Snapshot.Version.VersionNumber < snapshot.Version.VersionNumber then
                let trackingCacheData = createTrackingCacheData backgroundCacheData
                _tagCache <- TagCache.TrackingCache trackingCacheData

        | TagCache.TrackingAndBackgroundCache (trackingCacheData, backgroundCacheData) ->

            // Merge the background cache into the tracking cache
            if backgroundCacheData.Snapshot.Version.VersionNumber < snapshot.Version.VersionNumber then
                let newTrackingCacheData = createTrackingCacheData backgroundCacheData
                let mergedTrackingCacheData = trackingCacheData.Merge snapshot newTrackingCacheData
                _tagCache <- TagCache.TrackingCache mergedTrackingCacheData

        | TagCache.TrackingCache _ ->
            // Already tracking, no more work to do
            ()
        | TagCache.None ->
            ()

    member x.Dispose() =
        _asyncTaggerSource.Dispose()
        _eventHandlers.DisposeAll()

    member x.RaiseTagsChanged span =
        _tagsChanged.Trigger(x, SnapshotSpanEventArgs(span))

    /// Called when the IAsyncTaggerSource raises a Changed event.  Clear out the 
    /// cache, pass on the event to the ITagger and wait for the next request
    member x.OnAsyncSourceChanged() =   

        // Clear out the cache.  It's no longer valid.
        _tagCache <- TagCache.None
        x.CancelAsyncBackgroundRequest()

        // Now if we've previously had a SnapshotSpan requested via GetTags go ahead
        // and tell the consumers that it's changed.  Use the entire cached request
        // span here.  We're pessimistic when we have a Changed call because we have
        // no information on what could've changed
        match _cachedRequestSpan with
        | None -> ()
        | Some cachedRequestSpan -> x.RaiseTagsChanged cachedRequestSpan

    /// If the Layout changes while we are in the middle of getting tags we want to 
    /// prioritize the new set of visible lines.
    member x.OnLayoutChanged _ =
        match _asyncBackgroundRequest with
        | None -> ()
        | Some asyncBackgroundRequest ->
            match OptionUtil.map2 TextViewUtil.GetVisibleSnapshotLineRange _asyncTaggerSource.TextView with
            | None -> ()
            | Some visibleLineRange ->
                let left = asyncBackgroundRequest.LineRange.ExtentIncludingLineBreak
                let right = visibleLineRange.ExtentIncludingLineBreak
                if left.Snapshot = right.Snapshot && left.IntersectsWith right then
                    asyncBackgroundRequest.PriorityLineRangeQueue.Enqueue visibleLineRange

    /// Is the async operation with the specified CancellationTokenSource the active 
    /// background request
    member x.IsActiveBackgroundRequest cancellationTokenSource =
        match _asyncBackgroundRequest with
        | None -> false
        | Some asyncBackgroundRequest -> asyncBackgroundRequest.CancellationTokenSource = cancellationTokenSource

    /// Given a new tag list determine if the results differ from what we would've been 
    /// returning from our TrackingCacheData over the same SnapshotSpan
    member x.DidTagsChange span (tagList : ITagSpan<'TTag> list) =
        let trackingCacheData = 
            match _tagCache with
            | TagCache.None -> None
            | TagCache.BackgroundCache _ -> None
            | TagCache.TrackingCache trackingCacheData -> Some trackingCacheData
            | TagCache.TrackingAndBackgroundCache (trackingCacheData, _) -> Some trackingCacheData

        match trackingCacheData with
        | None -> 
            // Nothing in the TrackingDataCache.  It's changed if there are any
            // tags in the tagList
            not (List.isEmpty tagList)
        | Some trackingCacheData ->
            let trackingTagList = 
                let tagResult = x.GetTagsFromTrackingCacheData span trackingCacheData
                tagResult.TagList
            if trackingTagList.Length <> tagList.Length then
                true
            else
                let trackingSet = 
                    trackingTagList
                    |> Seq.map (fun tagSpan -> (tagSpan.Span.Start.Position, tagSpan.Span.Length))
                    |> Set.ofSeq

                tagList
                |> Seq.map (fun tagSpan -> (tagSpan.Span.Start.Position, tagSpan.Span.Length))
                |> Seq.exists (fun tuple -> not (Set.contains tuple trackingSet))

    /// Called on the main thread when the request for tags has processed at least a small 
    /// section of the file.  This funtion may be called many times for a single background 
    /// request
    ///
    /// Called on the main thread
    member x.OnGetTagsInBackgroundProgress cancellationTokenSource (lineRange : SnapshotLineRange) tagList = 

        if x.IsActiveBackgroundRequest cancellationTokenSource then

            let span = lineRange.ExtentIncludingLineBreak
            let newData = { Span = span; TagList = tagList }

            // Merge the existing background tracking data with the new data
            let merge (backgroundCacheData : BackgroundCacheData<'TTag>) =
                if backgroundCacheData.Span.Snapshot = span.Snapshot then
                    let span = SnapshotSpanUtil.CreateOverarching span backgroundCacheData.Span
                    {   Span = span
                        TagList = List.append tagList backgroundCacheData.TagList }
                else
                    newData

            // Update the TagCache based on the progress dat
            _tagCache <- 
                match _tagCache with
                | TagCache.None -> TagCache.BackgroundCache newData
                | TagCache.TrackingCache trackingCacheData ->
                    // Currently tracking.  Merge in the new background data with 
                    // the active tracking data.  It will be cleared out on completion
                    TagCache.TrackingAndBackgroundCache (trackingCacheData, newData)
                | TagCache.TrackingAndBackgroundCache (trackingCacheData, backgroundCacheData) ->
                    // Need to merge the backgorund caching data.  
                    let backgroundCacheData = merge backgroundCacheData
                    TagCache.TrackingAndBackgroundCache (trackingCacheData, backgroundCacheData)
                | TagCache.BackgroundCache backgroundCacheData ->
                    TagCache.BackgroundCache (merge backgroundCacheData)

            // Determine if the tags changed on the given Span.  In an edit it's very possible and likely
            // that the ITagSpan we returned by simply mapping the SnapshotSpan forward was correct.  If 
            // so then for a given SnapshotSpan we've already returned a result which was correct.  Raising
            // TagsChanged again for that SnapshotSpan will cause needless work to ocur (and potentially
            // more layouts
            if x.DidTagsChange span tagList then
                x.RaiseTagsChanged span

    /// Called when the background request is completed
    ///
    /// Called on the main thread
    member x.OnGetTagsInBackgroundComplete cancellationTokenSource =
        if x.IsActiveBackgroundRequest cancellationTokenSource then

            // The request is complete.  Reset the active request information
            x.CancelAsyncBackgroundRequest()

            // Update the tag cache to indicate we are no longer doing any tracking edits
            _tagCache <- 
                match _tagCache with
                | TagCache.None -> _tagCache
                | TagCache.BackgroundCache _ -> _tagCache
                | TagCache.TrackingAndBackgroundCache (_, backgroundCacheData) -> TagCache.BackgroundCache backgroundCacheData
                | TagCache.TrackingCache _ -> TagCache.None

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

type IBasicTaggerSource<'TTag when 'TTag :> ITag> = 

    /// The current Snapshot.  
    abstract TextSnapshot : ITextSnapshot

    /// Get the ITagSpan<'TTag> values in the specified SnapshotSpan
    abstract GetTags : span : SnapshotSpan  -> ITagSpan<'TTag> list

    /// Raised by the source when the underlying source has changed.  All previously
    /// provided data should be considered incorrect after this event
    [<CLIEvent>]
    abstract Changed : IEvent<unit>

type BasicTagger<'TTag when 'TTag :> ITag>
    (
        _basicTaggerSource : IBasicTaggerSource<'TTag>
    ) as this = 

    let mutable _cachedRequestSpan : SnapshotSpan option = None

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()

    do 
        _basicTaggerSource.Changed
        |> Observable.subscribe this.OnBasicTaggerSourceChanged
        |> _eventHandlers.Add

    member x.CachedRequestSpan 
        with get() = _cachedRequestSpan
        and set value = _cachedRequestSpan <- value

    member x.Dispose() =
        _eventHandlers.DisposeAll()

        match _basicTaggerSource with
        | :? System.IDisposable as disposable -> disposable.Dispose()
        | _ -> ()

    member x.AdjustRequestSpan (requestSpan : SnapshotSpan) =
        _cachedRequestSpan <- TaggerUtil.AdjustRequestSpan _cachedRequestSpan requestSpan

    member x.GetTags (col : NormalizedSnapshotSpanCollection) =

        // Adjust the requested SnapshotSpan to be the overarching SnapshotSpan of the 
        // request.  
        let span = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
        x.AdjustRequestSpan span

        // Even though it's easier don't do a GetTags request for the overarching SnapshotSpan
        // of the request.  It's possible for the overarching SnapshotSpan to have an order
        // magnitudes more lines than the items in the collection.  This is very possible when
        // large folded regions or on screen.  Instead just request the individual ones
        if col.Count = 1 then
            _basicTaggerSource.GetTags col.[0] |> Seq.ofList
        else
            col
            |> Seq.map _basicTaggerSource.GetTags
            |> Seq.concat

    member x.OnBasicTaggerSourceChanged() =
        match _cachedRequestSpan with
        | None -> ()
        | Some cachedRequestSpan ->

            let args = SnapshotSpanEventArgs(cachedRequestSpan)
            _tagsChanged.Trigger(this, args)

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

/// It's possible, and very likely, for an ITagger<T> to be requested multiple times for the 
/// same scenario via the ITaggerProvider.  This happens when extensions spin up custom 
/// ITagAggregator instances or simple manually query for a new ITagger.  Having multiple taggers
/// for the same data is often very unnecessary.  Produces a lot of duplicate work.  For example
/// consider having multiple :hlsearch taggers for the same ITextView.  
///
/// CountedTagger helps solve this by using a ref counted solution over the raw ITagger.  It allows
/// for only one ITagger to be created for the same scenario
type CountedTagger<'TTag when 'TTag :> ITag>
    (
        _tagger : ITagger<'TTag>,
        _key : obj,
        _propertyCollection : PropertyCollection
    ) = 

    member x.Tagger = _tagger

    member x.Dispose() = 
        match PropertyCollectionUtil.GetValue<int* ITagger<'TTag>> _key _propertyCollection with 
        | None -> 
            // Should never happen.  Indicates major programming problem as we now 
            // aren't properly ref tracking the underlying ITagger.
            Contract.Assert false
        | Some (count, tagger) ->
            if count = 1 then
                match _tagger with
                | :? System.IDisposable as disposable -> disposable.Dispose()
                | _ -> ()

                _propertyCollection.RemoveProperty _key |> ignore
            else
                _propertyCollection.[_key] <- (count - 1, tagger)

    interface ITagger<'TTag> with
        member x.GetTags col = _tagger.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagger.TagsChanged

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

    static member Create (key : obj) propertyCollection (createTagger : unit -> ITagger<'TTag>) = 
        let rawTagger = 
            match PropertyCollectionUtil.GetValue<int * ITagger<'TTag>> key propertyCollection with
            | None ->
                // Initial creation
                let rawTagger = createTagger()
                propertyCollection.[key] <- (1, rawTagger)
                rawTagger
            | Some (count, rawTagger) ->
                // Re-use the existing ITagger
                let count = count + 1
                propertyCollection.[key] <- (count, rawTagger)
                rawTagger

        new CountedTagger<'TTag>(rawTagger, key, propertyCollection) :> ITagger<'TTag>

/// Tagger for incremental searches
type IncrementalSearchTaggerSource (_vimBuffer : IVimBuffer) =

    let _search = _vimBuffer.IncrementalSearch
    let _textBuffer = _vimBuffer.TextBuffer
    let _globalSettings = _vimBuffer.GlobalSettings
    let _eventHandlers = DisposableBag()
    let _changed = new Event<unit>()
    let mutable _searchSpan : ITrackingSpan option = None

    do 
        let raiseChanged () = _changed.Trigger()

        let updateCurrentWithResult result = 
            _searchSpan <-
                match result with
                | SearchResult.Found (_, span, _) -> span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive) |> Some
                | SearchResult.NotFound _ -> None

        // When the search is updated we need to update the result.  Make sure to do so before raising 
        // the event.  The editor can and will call back into us synchronously and access a stale value
        // if we don't
        _search.CurrentSearchUpdated 
        |> Observable.subscribe (fun result ->

            updateCurrentWithResult result
            raiseChanged())
        |> _eventHandlers.Add

        // When the search is completed there is nothing left for us to tag.
        _search.CurrentSearchCompleted 
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseChanged())
        |> _eventHandlers.Add

        // When the search is cancelled there is nothing left for us to tag.
        _search.CurrentSearchCancelled
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseChanged())
        |> _eventHandlers.Add

        // We need to pay attention to the current IVimBuffer mode.  If it's any visual mode then we don't want
        // to highlight any spans.
        _vimBuffer.SwitchedMode
        |> Observable.subscribe (fun _ -> raiseChanged())
        |> _eventHandlers.Add

        // When the 'incsearch' setting is changed it impacts our tag display
        //
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.IncrementalSearchName)
        |> Observable.subscribe (fun _ -> raiseChanged())
        |> _eventHandlers.Add

    member x.GetTags span =

        if VisualKind.IsAnyVisual _vimBuffer.ModeKind || not _globalSettings.IncrementalSearch then 
            // If any of these are true then we shouldn't be displaying any tags
            List.empty
        else
            let snapshot = SnapshotSpanUtil.GetSnapshot span
            match _searchSpan |> Option.map (TrackingSpanUtil.GetSpan snapshot) |> OptionUtil.collapse with
            | None -> 
                // No search span or the search span doesn't map into the current ITextSnapshot so there
                // is nothing to return
                List.empty
            | Some span -> 
                // We have a span so return the tag
                let tag = TextMarkerTag(Constants.IncrementalSearchTagName)
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                [ tagSpan ]

    interface IBasicTaggerSource<TextMarkerTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal IncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textView : ITextView, textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None ->
                null
            | true, Some vimBuffer ->
                let taggerSource = new IncrementalSearchTaggerSource(vimBuffer)
                let tagger = new BasicTagger<TextMarkerTag>(taggerSource)
                tagger :> obj :?> ITagger<'T>

/// Tagger for completed incremental searches
type HighlightSearchTaggerSource
    ( 
        _textView : ITextView,
        _globalSettings : IVimGlobalSettings,
        _wordNav : ITextStructureNavigator,
        _search : ISearchService,
        _vimData : IVimData,
        _vimHost : IVimHost
    ) =

    let _textBuffer = _textView.TextBuffer
    let _changed = new Event<unit>()
    let _eventHandlers = DisposableBag()

    /// Users can temporarily disable highlight search with the ':noh' command.  This is true while 
    /// we are in that mode
    let mutable _oneTimeDisabled = false

    /// Whether or not the ITextView is visible.  There is one setting which controls the tags for
    /// all ITextView's in the system.  It's very wasteful to raise tags changed when we are not 
    /// visible.  Many components call immediately back into GetTags even if the ITextView is not
    /// visible.  Even for an async tagger this can kill perf by the sheer work done in the 
    /// background.
    let mutable _isVisible = true

    do 
        let raiseChanged () = 
            _changed.Trigger()

        let resetDisabledAndRaiseAllChanged () =
            _oneTimeDisabled <- false
            raiseChanged()

        // When the 'LastSearchData' property changes we want to reset the 'oneTimeDisabled' flag and 
        // begin highlighting again
        _vimData.LastPatternDataChanged
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged() )
        |> _eventHandlers.Add

        // Make sure we respond to the HighlightSearchOneTimeDisabled event
        _vimData.HighlightSearchOneTimeDisabled
        |> Observable.subscribe (fun _ ->
            _oneTimeDisabled <- true
            raiseChanged())
        |> _eventHandlers.Add

        // When the setting is changed it also resets the one time disabled flag
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged())
        |> _eventHandlers.Add

        _vimHost.IsVisibleChanged
        |> Observable.filter (fun textView -> textView = _textView)
        |> Observable.subscribe (fun _ -> 
            let isVisible = _vimHost.IsVisible _textView
            if _isVisible <> isVisible then
                _isVisible <- isVisible
                raiseChanged())
        |> _eventHandlers.Add

    /// Get the search information for a background 
    member x.GetDataForSpan() =
        // Build up the search information.  Don't need to wrap here as we just want
        // to consider the SnapshotSpan going forward
        let searchData = SearchData.OfPatternData _vimData.LastPatternData false
        { searchData with Kind = SearchKind.Forward }

    member x.GetTagsPrompt() = 
        let searchData = x.GetDataForSpan()
        if not _isVisible then
            Some List.empty
        elif StringUtil.isNullOrEmpty searchData.Pattern then
            // Nothing to give if there is no pattern
            Some List.empty
        elif not _globalSettings.HighlightSearch || _oneTimeDisabled then
            // Nothing to give if we are disabled
            Some List.empty
        else
            // There is a search pattern and we can't provide the data promptly
            None

    [<UsedInBackgroundThread>]
    static member GetTagsInBackground (searchService : ISearchService) wordNavigator searchData span (cancellationToken : CancellationToken) = 

        let withSpan (span : SnapshotSpan) =  
            span.Start
            |> Seq.unfold (fun point -> 

                cancellationToken.ThrowIfCancellationRequested()

                if SnapshotPointUtil.IsEndPoint point then
                    // If this is the end point of the ITextBuffer then we are done
                    None
                else
                    match searchService.FindNext searchData point wordNavigator with
                    | SearchResult.NotFound _ -> 
                        None
                    | SearchResult.Found (_, foundSpan, _) ->

                        // It's possible for the SnapshotSpan here to be a 0 length span since Regex expressions
                        // can match 0 width text.  For example "\|i\>" from issue #480.  Vim handles this by 
                        // treating the 0 width match as a 1 width match. 
                        let foundSpan = 
                            if foundSpan.Length = 0 then
                                SnapshotSpan(foundSpan.Start, 1)
                            else
                                foundSpan

                        // Don't continue searching once we pass the end of the SnapshotSpan we are searching
                        if foundSpan.Start.Position <= span.End.Position then 
                            Some(foundSpan, foundSpan.End)
                        else 
                            None)

        let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
        withSpan span
        |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )
        |> List.ofSeq

    interface IAsyncTaggerSource<SearchData, TextMarkerTag> with
        member x.Delay = Some 100
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.TextView = Some _textView
        member x.GetDataForSpan _ = x.GetDataForSpan()
        member x.GetTagsPrompt _ = x.GetTagsPrompt()
        member x.GetTagsInBackground searchData span cancellationToken = HighlightSearchTaggerSource.GetTagsInBackground _search _wordNav searchData span cancellationToken
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    let _key = obj()

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None -> 
                null
            | true, Some vimTextBuffer ->
                CountedTagger.Create _key textView.Properties (fun () ->
                    let wordNavigator = vimTextBuffer.WordNavigator
                    let asyncTaggerSource = new HighlightSearchTaggerSource(textView, vimTextBuffer.GlobalSettings, wordNavigator, _vim.SearchService, _vim.VimData, _vim.VimHost)
                    let asyncTagger = new AsyncTagger<SearchData, TextMarkerTag>(asyncTaggerSource)
                    asyncTagger :> obj :?> ITagger<'T>)

/// Tagger for matches as they appear during a confirm substitute
type SubstituteConfirmTaggerSource
    ( 
        _textBuffer : ITextBuffer,
        _mode : ISubstituteConfirmMode
    ) =

    let _changed = new Event<unit>()
    let _eventHandlers = DisposableBag()
    let mutable _currentMatch : SnapshotSpan option = None

    do 
        let raiseChanged () = _changed.Trigger()

        _mode.CurrentMatchChanged
        |> Observable.subscribe (fun data -> 
            _currentMatch <- data
            raiseChanged())
        |> _eventHandlers.Add

    member x.GetTags span = 
        match _currentMatch with
        | Some currentMatch -> 
            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            let tagSpan = TagSpan(currentMatch, tag) :> ITagSpan<TextMarkerTag>
            [ tagSpan ]
        | None -> List.empty

    interface IBasicTaggerSource<TextMarkerTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type SubstituteConfirmTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None -> 
                null
            | true, Some buffer ->
                let taggerSource = new SubstituteConfirmTaggerSource(textBuffer, buffer.SubstituteConfirmMode)
                let tagger = new BasicTagger<TextMarkerTag>(taggerSource)
                tagger :> obj :?> ITagger<'T>

/// Fold tagger for the IOutliningRegion tags created by folds.  Note that folds work
/// on an ITextBuffer level and not an ITextView level.  Hence this works directly with
/// IFoldManagerData instead of IFoldManager
type internal FoldTaggerSource(_foldData : IFoldData) =

    let _textBuffer = _foldData.TextBuffer
    let _changed = new Event<unit>()

    do 
        _foldData.FoldsUpdated 
        |> Event.add (fun _ -> _changed.Trigger())

    member x.GetTags span =

        // Get the description for the given SnapshotSpan.  This is the text displayed for
        // the folded lines.
        let getDescription span = 
            let startLine, lastLine = SnapshotSpanUtil.GetStartAndLastLine span
            sprintf "%d lines ---" ((lastLine.LineNumber - startLine.LineNumber) + 1)

        let snapshot = SnapshotSpanUtil.GetSnapshot span
        _foldData.Folds
        |> Seq.filter ( fun span -> span.Snapshot = snapshot )
        |> Seq.map (fun span ->
            let description = getDescription span
            let hint = span.GetText()
            let tag = OutliningRegionTag(true, true, description, hint)
            TagSpan<OutliningRegionTag>(span, tag) :> ITagSpan<OutliningRegionTag> )
        |> List.ofSeq

    interface IBasicTaggerSource<OutliningRegionTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<OutliningRegionTag>)>]
type FoldTaggerProvider
    [<ImportingConstructor>]
    (_factory : IFoldManagerFactory) = 

    let _key = obj()

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> textBuffer =
            CountedTagger.Create _key textBuffer.Properties (fun () ->
                let foldData = _factory.GetFoldData textBuffer
                let taggerSource = FoldTaggerSource(foldData)
                let tagger = new BasicTagger<OutliningRegionTag>(taggerSource)
                tagger :> obj :?> ITagger<'T>)


