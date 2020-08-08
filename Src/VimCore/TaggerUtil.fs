namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Tagging
open System.Collections.ObjectModel
open Microsoft.VisualStudio.Utilities
open System
open System.Diagnostics
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Net
open System.Threading.Tasks

module TaggerUtilCore =

    /// The simple taggers when changed need to provide an initial SnapshotSpan
    /// for the TagsChanged event.  It's important that this SnapshotSpan be kept as
    /// small as possible.  If it's incorrectly large it can have a negative performance
    /// impact on the editor.  In particular
    ///
    /// 1. The value is directly provided in ITagAggregator::TagsChanged.  This value
    ///    is acted on directly by many editor components.  Providing a large range
    ///    unnecessarily increases their work load.
    /// 2. It can cause a ripple effect in Visual Studio 2010 RTM.  The SnapshotSpan
    ///    returned will be immediately be the value passed to GetTags for every other
    ///    ITagger in the system (TextMarkerVisualManager issue).
    ///
    /// In order to provide the minimum possible valid SnapshotSpan the simple taggers
    /// cache the overarching SnapshotSpan for the latest ITextSnapshot of all requests
    /// to which they are given.
    let AdjustRequestedSpan (cachedRequestSpan: SnapshotSpan option) (requestSpan: SnapshotSpan) =
        match cachedRequestSpan with
        | None -> requestSpan
        | Some cachedRequestSpan ->
            let cachedSnapshot = cachedRequestSpan.Snapshot
            let requestSnapshot = requestSpan.Snapshot
            if cachedSnapshot = requestSnapshot then
                // Same snapshot so we just need the overarching SnapshotSpan
                SnapshotSpanUtil.CreateOverarching cachedRequestSpan requestSpan
            elif cachedSnapshot.Version.VersionNumber < requestSnapshot.Version.VersionNumber then
                // Request for a span on a new ITextSnapshot.  Translate the old SnapshotSpan
                // to the new ITextSnapshot and get the overarching value
                let trackingSpan =
                    cachedSnapshot.CreateTrackingSpan(cachedRequestSpan.Span, SpanTrackingMode.EdgeInclusive)
                match TrackingSpanUtil.GetSpan requestSnapshot trackingSpan with
                | Some s -> SnapshotSpanUtil.CreateOverarching s requestSpan
                | None ->
                    // If we can't translate the previous SnapshotSpan forward then simply use the
                    // entire ITextSnapshot.  This is a correct value, it just has the potential for
                    // some inefficiencies
                    SnapshotUtil.GetExtent requestSnapshot
            else
                // It's a request for a value in the past.  This is a very rare scenario that is almost
                // always followed by a request for a value on the current snapshot.  Just return the
                // entire ITextSnapshot.  This is a correct value, it just has the potential for
                // some inefficiencies
                SnapshotUtil.GetExtent requestSpan.Snapshot

/// This solves the same problem as CountedTagger but for IClassifier
type internal CountedValue<'T>(_value: 'T, _key: obj, _propertyCollection: PropertyCollection) =

    let mutable _count = 1

    member x.Value = _value

    member private x.Increment() = _count <- _count + 1

    member x.Release() =
        _count <- _count - 1
        if _count = 0 then
            match _value :> obj with
            | :? IDisposable as d -> d.Dispose()
            | _ -> ()
            _propertyCollection.RemoveProperty(_key) |> ignore

    static member GetOrCreate propertyCollection key (createFunc: unit -> 'T) =
        match PropertyCollectionUtil.GetValue<CountedValue<'T>> key propertyCollection with
        | Some countedValue ->
            countedValue.Increment()
            countedValue
        | None ->
            let countedValue = new CountedValue<'T>(createFunc(), key, propertyCollection)
            propertyCollection.[key] <- countedValue
            countedValue

/// It's possible, and very likely, for an ITagger<T> to be requested multiple times for the
/// same scenario via the ITaggerProvider.  This happens when extensions spin up custom
/// ITagAggregator instances or simple manually query for a new ITagger.  Having multiple taggers
/// for the same data is often very unnecessary.  Produces a lot of duplicate work.  For example
/// consider having multiple :hlsearch taggers for the same ITextView.
///
/// CountedTagger helps solve this by using a ref counted solution over the raw ITagger.  It allows
/// for only one ITagger to be created for the same scenario
type internal CountedTagger<'TTag when 'TTag :> ITag> =

    val private _countedValue: CountedValue<ITagger<'TTag>>

    member x.Tagger = x._countedValue.Value

    member x.Dispose() = x._countedValue.Release()

    new(propertyCollection: PropertyCollection, key: obj, createFunc: unit -> ITagger<'TTag>) =
        let value = CountedValue<ITagger<'TTag>>.GetOrCreate propertyCollection key createFunc
        { _countedValue = value }

    interface ITagger<'TTag> with
        member x.GetTags col = x.Tagger.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = x.Tagger.TagsChanged

    interface IDisposable with
        member x.Dispose() = x.Dispose()


/// This solves the same problem as CountedTagger but for IClassifier
type internal CountedClassifier =

    val private _countedValue: CountedValue<IClassifier>

    member x.Classifier = x._countedValue.Value

    member x.Dispose() = x._countedValue.Release()

    new(propertyCollection: PropertyCollection, key: obj, createFunc: unit -> IClassifier) =
        let value = CountedValue<IClassifier>.GetOrCreate propertyCollection key createFunc
        { _countedValue = value }

    interface IClassifier with
        member x.GetClassificationSpans span = x.Classifier.GetClassificationSpans span
        [<CLIEvent>]
        member x.ClassificationChanged = x.Classifier.ClassificationChanged

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
type internal OutliningData =
    { TrackingSpan: ITrackingSpan
      Tag: OutliningRegionTag
      Cookie: int }

/// Implementation of the IAdhocOutliner service.  This class is only used for testing
/// so it's focused on creating the simplest implementation vs. the most efficient
type internal AdhocOutliner(_textBuffer: ITextBuffer) =

    let _map = new Dictionary<int, OutliningData>()
    let mutable _counter = 0
    let _changed = StandardEvent()

    static let s_outlinerKey = new obj()
    static let s_outlinerTaggerKey = new obj()
    static let s_emptyCollection = new ReadOnlyCollection<OutliningRegion>([||])

    static member OutlinerTaggerKey = s_outlinerTaggerKey

    /// The outlining implementation is worthless unless it is also registered as an ITagger
    /// component.  If this hasn't happened by the time the APIs are being queried then it is
    /// a bug and we need to notify the developer
    member x.EnsureTagger() =
        if not (PropertyCollectionUtil.ContainsKey s_outlinerTaggerKey _textBuffer.Properties) then
            let msg =
                "In order to use IAdhocOutliner you must also export an ITagger implementation for the buffer which return CreateOutliningTagger"
            raise (new Exception(msg))

    /// Get all of the values which map to the given ITextSnapshot
    member private x.GetOutliningRegions(span: SnapshotSpan) =
        // Avoid allocating a map or new collection if we are simply empty
        if _map.Count = 0 then
            s_emptyCollection
        else
            let snapshot = span.Snapshot
            let list = new List<OutliningRegion>()
            for cur in _map.Values do
                match TrackingSpanUtil.GetSpan snapshot cur.TrackingSpan with
                | None -> ()
                | Some currentSpan ->
                    list.Add
                        ({ Tag = cur.Tag
                           Span = currentSpan
                           Cookie = cur.Cookie })

            ReadOnlyCollection<OutliningRegion>(list)

    member private x.CreateOutliningRegion (span: SnapshotSpan) spanTrackingMode text hint =
        let trackingSpan = span.Snapshot.CreateTrackingSpan(span.Span, spanTrackingMode)
        let tag = OutliningRegionTag(text, hint)

        let data =
            { TrackingSpan = trackingSpan
              Tag = tag
              Cookie = _counter }
        _map.[_counter] <- data
        _counter <- _counter + 1
        _changed.Trigger x
        { Tag = tag
          Span = span
          Cookie = data.Cookie }

    static member GetOrCreate(textBuffer: ITextBuffer) =
        let propertyCollection = textBuffer.Properties
        propertyCollection.GetOrCreateSingletonProperty(s_outlinerKey, (fun _ -> AdhocOutliner(textBuffer)))

    interface IAdhocOutliner with
        member x.TextBuffer = _textBuffer

        member x.GetOutliningRegions span =
            x.EnsureTagger()
            x.GetOutliningRegions span

        member x.CreateOutliningRegion span spanTrackingMode text hint =
            x.EnsureTagger()
            x.CreateOutliningRegion span spanTrackingMode text hint

        member x.DeleteOutliningRegion cookie =
            x.EnsureTagger()
            if _map.Remove cookie then
                _changed.Trigger x
                true
            else
                false

        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface IBasicTaggerSource<OutliningRegionTag> with

        member x.GetTags span =
            x.GetOutliningRegions span
            |> Seq.map (fun x -> new TagSpan<OutliningRegionTag>(x.Span, x.Tag) :> ITagSpan<OutliningRegionTag>)
            |> ReadOnlyCollectionUtil.OfSeq

        [<CLIEvent>]
        member x.Changed = _changed.Publish

type internal Classifier(_tagger: ITagger<IClassificationTag>) as this =

    let _changed = StandardEvent<ClassificationChangedEventArgs>()
    let _eventHandlers = DisposableBag()

    do
        _tagger.TagsChanged
        |> Observable.subscribe (fun args ->
            let args = ClassificationChangedEventArgs(args.Span)
            _changed.Trigger this args)
        |> _eventHandlers.Add

    member private x.Dispose() =
        _eventHandlers.DisposeAll()
        match _tagger with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    interface IClassifier with

        member x.GetClassificationSpans span =
            let col = NormalizedSnapshotSpanCollection(span)
            let tags = _tagger.GetTags(col) |> Seq.map (fun x -> ClassificationSpan(x.Span, x.Tag.ClassificationType))
            List<ClassificationSpan>(tags) :> IList<ClassificationSpan>

        [<CLIEvent>]
        member x.ClassificationChanged = _changed.Publish

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal BasicTagger<'TTag when 'TTag :> ITag>(_basicTaggerSource: IBasicTaggerSource<'TTag>) as this =

    let _changed = StandardEvent<SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()
    let mutable _cachedRequestSpan: SnapshotSpan option = None

    do
        _basicTaggerSource.Changed
        |> Observable.subscribe (fun x ->
            match _cachedRequestSpan with
            | Some s ->
                let args = SnapshotSpanEventArgs(s)
                _changed.Trigger this args
            | None -> ())
        |> _eventHandlers.Add

    member x.CachedRequestSpan
        with get () = _cachedRequestSpan
        and set value = _cachedRequestSpan <- value

    member private x.Dispose() =
        _eventHandlers.DisposeAll()
        match _basicTaggerSource with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    member x.GetTags(col: NormalizedSnapshotSpanCollection) =
        if col.Count > 0 then
            let span = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
            _cachedRequestSpan <- Some(TaggerUtilCore.AdjustRequestedSpan _cachedRequestSpan span)

        match col.Count with
        | 0 -> Seq.empty
        | 1 -> _basicTaggerSource.GetTags(col.[0]) :> ITagSpan<'TTag> seq
        | _ -> col |> Seq.collect (fun x -> _basicTaggerSource.GetTags x)

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _changed.Publish

    interface IDisposable with
        member x.Dispose() = x.Dispose()

/// Need another type here because SnapshotLineRange is a struct and we need atomic assignment
/// guarantees to use Interlocked.Exchange
type TextViewLineRange =
    { LineRange: SnapshotLineRange }

/// This class is used to support the one way transfer of SnapshotLineRange values between
/// the foreground thread of the tagger and the background processing thread.  It understands
/// the priority placed on the visible UI lines and will transfer those lines at a higher
/// priority than normal requests
[<UsedInBackgroundThread>]
type internal Channel() =

    /// This is the normal request stack from the main thread.  More recently requested items
    /// are given higher priority than older items
    let mutable _stack: SnapshotLineRange list = list.Empty

    /// When set this is represents the visible line range of the text view.  It has the highest
    /// priority for the background thread
    let mutable _textViewLineRange: TextViewLineRange option = None

    /// Version number tracks the number of writes to the channel
    let mutable _version = 0

    member x.CurrentStack = _stack

    /// This number is incremented after every write to the channel.  It is a hueristic only and
    /// not an absolute indicator.  It is not set atomically with every write but instead occurs
    /// some time after the write.
    member x.CurrentVersion = _version

    member x.WriteVisibleLines lineRange =
        let lineRange = { LineRange = lineRange } |> Some
        Interlocked.Exchange(&_textViewLineRange, lineRange) |> ignore
        Interlocked.Increment(&_version) |> ignore

    member x.WriteNormal lineRange =
        let mutable success = false
        while not success do
            let oldStack = _stack
            let newStack = lineRange :: oldStack
            if oldStack = Interlocked.CompareExchange(&_stack, newStack, oldStack) then success <- true
        Interlocked.Increment(&_version) |> ignore

    member private x.ReadNormal() =
        let mutable value: SnapshotLineRange option = None
        let mutable isDone = false
        while not isDone do
            let oldStack = _stack
            match oldStack with
            | [] -> isDone <- true
            | head :: tail ->
                if oldStack = Interlocked.CompareExchange(&_stack, tail, oldStack) then
                    value <- Some head
                    isDone <- true
        value

    member private x.ReadVisibleLines() =
        let mutable value: SnapshotLineRange option = None
        let mutable isDone = false
        while not isDone do
            let oldTextViewLineRange = _textViewLineRange
            match oldTextViewLineRange with
            | None -> isDone <- true
            | Some lineRange ->
                if oldTextViewLineRange = Interlocked.CompareExchange(&_textViewLineRange, None, oldTextViewLineRange) then
                    value <- Some lineRange.LineRange
                    isDone <- true
        value

    member x.Read() =
        match x.ReadVisibleLines() with
        | Some lines -> Some lines
        | None -> x.ReadNormal()

[<RequireQualifiedAccess>]
[<NoComparison>]
type internal CompleteReason =
    | Finished
    | Cancelled
    | Error

/// The goal of this collection is to efficiently track the set of LineRange values that have
/// been visited for a given larger LineRange.  The order in which, or original granualarity
/// of visits is less important than the overall range which is visited.
///
/// For example if both ranges 1-3 and 2-5 are visited then the collection will only record
/// that 1-5 is visited.
type internal NormalizedLineRangeCollection() =

    let _list = List<LineRange>()

    member x.OverarchingLineRange =
        match _list.Count with
        | 0 -> None
        | 1 -> _list.[0] |> Some
        | _ ->
            let startLine = _list.[0].StartLineNumber
            let lastLine = _list.[_list.Count - 1].LastLineNumber
            LineRange.CreateFromBounds startLine lastLine |> Some

    member x.Count = _list.Count
    member x.Item
        with get (index) = _list.[index]

    member x.Add(lineRange: LineRange) =
        match x.FindInsertionPoint lineRange.StartLineNumber with
        | None ->
            // Just insert at the end and let the collapse code do the work in this case
            _list.Add(lineRange)
            x.CollapseIntersecting(_list.Count - 1)
        | Some index ->
            // Quick optimization check to avoid copying the contents of the List
            // structure down on insert
            let item = _list.[index]
            if item.StartLineNumber = lineRange.StartLineNumber || lineRange.ContainsLineNumber(item.StartLineNumber)
            then _list.[index] <- LineRange.CreateOverarching item lineRange
            else _list.Insert(index, lineRange)
            x.CollapseIntersecting(index)

    member x.Clear() = _list.Clear()

    member x.Contains lineRange = _list |> Seq.exists (fun x -> x.Contains lineRange)

    member x.Copy() = NormalizedLineRangeCollection.Create _list

    /// Get the unvisited lines in the specified range
    member x.GetUnvisited lineRange =
        let mutable value = Some lineRange
        let mutable index = 0
        while index < _list.Count do
            let current = _list.[index]
            if current.Intersects lineRange then

                if current.Contains(lineRange) then
                    // Already have a LineRange which completely contains the provided
                    // value.  No unvisited values
                    value <- None
                    index <- _list.Count
                elif current.StartLineNumber <= lineRange.StartLineNumber then
                    // The found range starts before and intersects.  The unvisited section
                    // is the range below
                    value <-
                        LineRange.CreateFromBounds (current.LastLineNumber + 1) (lineRange.LastLineNumber) |> Some
                    index <- _list.Count
                elif current.StartLineNumber > lineRange.StartLineNumber then
                    // The found range starts below and intersects.  The unvisited section
                    // is the line range above
                    value <-
                        LineRange.CreateFromBounds lineRange.StartLineNumber (current.StartLineNumber - 1) |> Some
                    index <- _list.Count

            else
                index <- index + 1
        value

    /// This is the helper method for Add which will now collapse elements that intersect.   We only have
    /// to look at the item before the insert and all items after and not the entire collection.
    member private x.CollapseIntersecting index =
        // It's possible this new LineRange actually intersects with the LineRange before
        // the insertion point.  LineRange values are ordered by start line.  Hence the LineRange
        // before could have an extent which intersects the new LineRange but not the previous
        // LineRange at this index.  Do a quick check for this and if it's true just start the
        // collapse one index backwards
        let lineRange = _list.[index]
        if index > 0 && _list.[index - 1].Intersects(lineRange) then
            x.CollapseIntersecting(index - 1)
        else
            let mutable removeCount = 0
            let mutable current = index + 1
            let mutable lineRange = lineRange
            while current < _list.Count do
                let currentLineRange = _list.[current]
                if not (lineRange.Intersects(currentLineRange)) then
                    current <- _list.Count
                else
                    lineRange <- LineRange.CreateOverarching lineRange currentLineRange
                    _list.[index] <- lineRange
                    removeCount <- removeCount + 1
                    current <- current + 1

            if removeCount > 0 then _list.RemoveRange(index + 1, removeCount)

    member private x.FindInsertionPoint(startLineNumber: int) =
        let mutable value: int option = None
        let mutable index = 0
        while index < _list.Count do
            if startLineNumber <= _list.[index].StartLineNumber then
                value <- Some index
                index <- _list.Count
            else
                index <- index + 1
        value

    static member Create(collection: LineRange seq) =
        let range = NormalizedLineRangeCollection()
        for r in collection do
            range.Add r
        range

    interface IEnumerable<LineRange> with
        member x.GetEnumerator() = _list.GetEnumerator() :> IEnumerator<LineRange>

    interface IEnumerable with
        member x.GetEnumerator() = (_list :> IEnumerable).GetEnumerator()

[<RequireQualifiedAccess>]
[<NoComparison>]
type internal TagCacheState =
    | None
    | Partial
    | Complete

[<Struct>]
type internal TrackingCacheData<'TTag when 'TTag :> ITag>(_trackingSpan: ITrackingSpan, _trackingList: ReadOnlyCollection<ITrackingSpan * 'TTag>) =

    member x.TrackingSpan = _trackingSpan

    member x.TrackingList = _trackingList

    member x.Merge (snapshot: ITextSnapshot) (trackingCacheData: TrackingCacheData<'TTag>) =
        let left = TrackingSpanUtil.GetSpan snapshot (x.TrackingSpan)
        let right = TrackingSpanUtil.GetSpan snapshot trackingCacheData.TrackingSpan

        let span =
            match (left, right) with
            | Some left, Some right -> SnapshotSpanUtil.CreateOverarching left right
            | Some left, None -> left
            | None, Some right -> right
            | None, None -> SnapshotSpan(snapshot, 0, 0)

        let trackingSpan = snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)

        let tagList =
            x.TrackingList
            |> Seq.append trackingCacheData.TrackingList
            |> Seq.distinctBy (fun (x, _) -> TrackingSpanUtil.GetSpan snapshot x)
            |> ReadOnlyCollectionUtil.OfSeq

        TrackingCacheData(trackingSpan, tagList)

    /// Does this tracking information contain tags over the given span in it's
    /// ITextSnapshot
    member x.ContainsCachedTags(span: SnapshotSpan) =
        let snapshot = span.Snapshot
        TrackingSpanUtil.GetSpan snapshot x.TrackingSpan |> Option.isSome

    /// Get the cached tags on the given ITextSnapshot
    ///
    /// If this SnapshotSpan is coming from a different snapshot which is ahead of
    /// our current one we need to take special steps.  If we simply return nothing
    /// and go to the background the tags will flicker on screen.
    ///
    /// To work around this we try to map the tags to the requested ITextSnapshot. If
    /// it succeeds then we use the mapped values and simultaneously kick off a background
    /// request for the correct ones
    member x.GetCachedTags snapshot =
        // Mapping gave us at least partial information.  Will work for the transition
        // period
        x.TrackingList
        |> Seq.map (fun (span, tag) ->
            match TrackingSpanUtil.GetSpan snapshot span with
            | None -> None
            | Some span -> TagSpan<'TTag>(span, tag) :> ITagSpan<'TTag> |> Some)
        |> SeqUtil.filterToSome
        |> ReadOnlyCollectionUtil.OfSeq

/// This holds the set of data which is currently known from the background thread.  Data in
/// this collection should be considered final for the given Snapshot.  It will only change
/// if the AsyncTaggerSource itself raises a Changed event (in which case we discard all
/// background data).
[<Struct>]
type BackgroundCacheData<'TTag when 'TTag :> ITag> =

    val private _snapshot: ITextSnapshot
    val private _visitedCollection: NormalizedLineRangeCollection
    val private _tagList: ReadOnlyCollection<ITagSpan<'TTag>>

    member x.Snapshot = x._snapshot

    /// Set of line ranges for which tags are known
    member x.VisitedCollection = x._visitedCollection

    /// Set of known tags
    member x.TagList = x._tagList

    member x.Span =
        match x._visitedCollection.OverarchingLineRange with
        | None -> SnapshotSpan(x.Snapshot, 0, 0)
        | Some range ->
            let lineRange = SnapshotLineRange(x.Snapshot, range.StartLineNumber, range.Count)
            lineRange.ExtentIncludingLineBreak

    new(snapshot, visitedCollection, tagList) =
        { _snapshot = snapshot
          _visitedCollection = visitedCollection
          _tagList = tagList }

    new(lineRange: SnapshotLineRange, tagList) =
        let col = NormalizedLineRangeCollection()
        col.Add(lineRange.LineRange)
        { _snapshot = lineRange.Snapshot
          _visitedCollection = col
          _tagList = tagList }

    /// Determine tag cache state we have for the given SnapshotSpan
    member x.GetTagCacheState(span: SnapshotSpan) =
        // If the requested span doesn't even intersect with the overarching SnapshotSpan
        // of the cached data in the background then a more exhaustive search isn't needed
        // at this time
        let cachedSpan = x.Span
        if not (cachedSpan.IntersectsWith(span)) then
            TagCacheState.None
        else
            let lineRange = SnapshotLineRange.CreateForSpan(span)
            match x.VisitedCollection.GetUnvisited lineRange.LineRange with
            | Some _ -> TagCacheState.Partial
            | None -> TagCacheState.Complete

    /// Create a TrackingCacheData instance from this BackgroundCacheData
    member x.CreateTrackingCacheData() =
        // Create the list.  Initiate an ITrackingSpan for every SnapshotSpan present
        let trackingList =
            x.TagList
            |> Seq.map (fun tagSpan ->
                let snapshot = tagSpan.Span.Snapshot
                let trackingSpan = snapshot.CreateTrackingSpan(tagSpan.Span.Span, SpanTrackingMode.EdgeExclusive)
                (trackingSpan, tagSpan.Tag))
            |> ReadOnlyCollectionUtil.OfSeq

        TrackingCacheData(x.Snapshot.CreateTrackingSpan(x.Span.Span, SpanTrackingMode.EdgeInclusive), trackingList)

[<Struct>]
type internal TagCache<'TTag when 'TTag :> ITag> =
    val mutable private _backgroundCacheData: BackgroundCacheData<'TTag> option
    val mutable private _trackingCacheData: TrackingCacheData<'TTag> option

    member x.BackgroundCacheData
        with get () = x._backgroundCacheData
        and set value = x._backgroundCacheData <- value

    member x.TrackingCacheData
        with get () = x._trackingCacheData
        and set value = x._trackingCacheData <- value

    member x.IsEmpty = Option.isNone x.BackgroundCacheData && Option.isNone x.TrackingCacheData

    new(backgroundCacheData, trackingCacheData) =
        { _backgroundCacheData = backgroundCacheData
          _trackingCacheData = trackingCacheData }

    static member Empty = TagCache<'TTag>(None, None)

[<Struct>]
type internal AsyncBackgroundRequest =
    { Snapshot: ITextSnapshot
      Channel: Channel
      Task: Task
      CancellationTokenSource: CancellationTokenSource }

type internal AsyncTagger<'TData, 'TTag when 'TTag :> ITag>(_asyncTaggerSource: IAsyncTaggerSource<'TData, 'TTag>) as this =

    /// This number was chosen virtually at random.  In extremely large files it's legal
    /// to ask for the tags for the entire file (and sadly very often done).  When this
    /// happens even an async tagger breaks down a bit.  It won't cause the UI to hang but
    /// it will appear the tagger is broken because it's not giving back any data.  So
    /// we break the non-visible sections into chunks and process the chunks one at a time
    ///
    /// Note: Even though a section is not visible we must still provide tags.  Gutter
    /// margins and such still need to see tags for non-visible portions of the buffer
    static let DefaultChunkCount = 500

    static let EmptyTagList = ReadOnlyCollection<ITagSpan<'TTag>>([||])

    let _eventHandlers = DisposableBag()
    let _tagsChanged = StandardEvent<SnapshotSpanEventArgs>()

    /// The one and only active AsyncBackgroundRequest instance.  There can be several
    /// in flight at once.  But we will cancel the earlier ones should a new one be
    /// requested
    let mutable _asyncBackgroundRequest: AsyncBackgroundRequest option = None

    /// The current cache of tags we've provided to our consumer
    let mutable _tagCache = TagCache<'TTag>.Empty

    /// The Overarching snapshot span for which we've received GetTags request.  At a glance
    /// it would appear that using a NormalizedLineRangeCollection would be more efficient
    /// here as it more accurately tracks the ranges.  That is true but we only use this
    /// for the cases where IAsyncTaggerSource declares that it changes.  It's not incorrect
    /// to say the overarching SnapshotSpan has changed in this case.  And it's cheaper
    /// to use the overaching span in the case of odd requests like time travel ones
    let mutable _cachedOverarchingRequestSpan: SnapshotSpan option = None

    let mutable _chunkCount = DefaultChunkCount

    do
        _asyncTaggerSource.Changed
        |> Observable.subscribe (fun _ -> this.OnAsyncTaggerSourceChanged())
        |> _eventHandlers.Add

        // If there is an ITextView associated with the IAsyncTaggerSource then we want to
        // listen to LayoutChanges.  If the layout changes while we are getting tags we want
        // to prioritize the visible lines
        match _asyncTaggerSource.TextView with
        | None -> ()
        | Some textView ->
            textView.LayoutChanged
            |> Observable.subscribe (fun _ -> this.OnLayoutChanged())
            |> _eventHandlers.Add

    member x.CachedOverarchingRequestSpan
        with get () = _cachedOverarchingRequestSpan
        and set value = _cachedOverarchingRequestSpan <- value

    member x.TagCacheData
        with get () = _tagCache
        and set value = _tagCache <- value

    member x.AsyncBackgroundRequestData
        with get () = _asyncBackgroundRequest
        and set value = _asyncBackgroundRequest <- value

    member x.ChunkCount
        with get () = _chunkCount
        and set value = _chunkCount <- value

    /// Given a new tag list determine if the results differ from what we would've been
    /// returning from our TrackingCacheData over the same SnapshotSpan.  Often times the
    /// new data is the same as the old hence we don't need to produce any changed information
    /// to the buffer
    member x.DidTagsChange (span: SnapshotSpan) (tagList: ReadOnlyCollection<ITagSpan<'TTag>>) =
        match _tagCache.TrackingCacheData with
        | None ->
            // Nothing in the tracking cache so it changed if there is anything in the new
            // collection.  If the new collection has anything then it changed
            tagList.Count > 0
        | Some trackingCacheData ->
            if not (trackingCacheData.ContainsCachedTags span) then
                // Nothing in the tracking cache so it changed if there is anything in the new
                // collection.  If the new collection has anything then it changed
                tagList.Count > 0
            else
                let trackingTagList = trackingCacheData.GetCachedTags(span.Snapshot)
                if trackingTagList.Count <> tagList.Count then
                    true
                else
                    let trackingSet =
                        trackingTagList
                        |> Seq.map (fun x -> x.Span)
                        |> HashSetUtil.OfSeq

                    tagList |> Seq.exists (fun x -> not (trackingSet.Contains x.Span))

    /// Get the tags for the specified NormalizedSnapshotSpanCollection.  Use the cache if
    /// possible and possibly go to the background if necessary
    member x.GetTags(col: NormalizedSnapshotSpanCollection) =
        // The editor itself will never send an empty collection to GetTags.  But this is an
        // API and other components are free to call it with whatever values they like
        if col.Count = 0 then
            EmptyTagList :> ITagSpan<'TTag> seq
        else
            x.AdjustRequestSpan col
            x.AdjustToSnapshot(col.[0].Snapshot)

            if col.Count = 1 then
                x.GetTagsForSpan col.[0]
            else
                VimTrace.TraceDebug("AsyncTagger::GetTags Count {0}", col.Count)
                let mutable all: ITagSpan<'TTag> seq = Seq.empty
                for span in col do
                    let current = x.GetTagsForSpan span
                    all <- Seq.append all current
                all

    member x.GetTagsForSpan span =
        let lineRange = SnapshotLineRange.CreateForSpan span
        VimTrace.TraceDebug("AsyncTagger::GetTags {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber)

        // First try and see if the tagger can provide prompt data.  We want to avoid
        // creating Task<T> instances if possible.
        let isComplete, syncTagList =
            match x.TryGetTagsPrompt span with
            | Some tagList -> (true, tagList)
            | None -> x.GetTagsFromBackgroundDataCache span

        let tagList =
            if isComplete then
                syncTagList
            else
                // The request couldn't be fully satisfied from the cache or prompt data so
                // we will request the data in the background thread
                x.GetTagsInBackground span

                // Since the request couldn't be fully fulfilled by the cache we augment the returned data
                // with our tracking data
                let trackingTagList = x.GetTagsFromTrackingDataCache span.Snapshot
                Seq.append syncTagList trackingTagList

        // Now filter the set of returned ITagSpan values to those which are part of the
        // requested NormalizedSnapshotSpanCollection.  The cache lookups don't dig down and
        // instead return all available tags.  We filter down the collection here to what's
        // necessary.
        tagList |> Seq.filter (fun tagSpan -> tagSpan.Span.IntersectsWith(span))

    member private x.Dispose() =
        _eventHandlers.DisposeAll()
        match _asyncTaggerSource with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    /// Try and get the tags promptly from the IAsyncTaggerSource
    member private x.TryGetTagsPrompt span = _asyncTaggerSource.TryGetTagsPrompt span

    member private x.GetTagsFromBackgroundDataCache span =
        match _tagCache.BackgroundCacheData with
        | None -> (false, Seq.empty)
        | Some backgroundCacheData ->
            if backgroundCacheData.Snapshot <> span.Snapshot then
                (false, Seq.empty)
            else
                match backgroundCacheData.GetTagCacheState span with
                | TagCacheState.Complete -> (true, backgroundCacheData.TagList :> ITagSpan<'TTag> seq)
                | TagCacheState.Partial -> (false, backgroundCacheData.TagList :> ITagSpan<'TTag> seq)
                | TagCacheState.None -> (false, Seq.empty)

    member private x.GetTagsFromTrackingDataCache snapshot =
        match _tagCache.TrackingCacheData with
        | None -> EmptyTagList
        | Some trackingCacheData -> trackingCacheData.GetCachedTags snapshot

    /// Get the tags for the specified SnapshotSpan in a background task.  If there are outstanding
    /// requests for SnapshotSpan values then this one will take priority over those
    member x.GetTagsInBackground(span: SnapshotSpan) =
        let synchronizationContext = SynchronizationContext.Current
        if synchronizationContext <> null then
            // The background processing should now be focussed on the specified ITextSnapshot
            let snapshot = span.Snapshot

            // In the majority case GetTags(NormalizedSnapshotCollection) drives this function and
            // AdjustToSnapshot is already called.  There are other code paths though within AsyncTagger
            // which call this method.  We need to guard against them here.
            x.AdjustToSnapshot snapshot

            // Our caching and partitioning of data is all done on a line range
            // basis.  Just expand the requested SnapshotSpan to the encompassing
            // SnaphotlineRange
            let lineRange = SnapshotLineRange.CreateForSpan span
            let span = lineRange.ExtentIncludingLineBreak

            let createNewRequest() =
                Contract.Assert(Option.isNone _asyncBackgroundRequest)
                VimTrace.TraceDebug
                    ("AsyncTagger Background New {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber)

                // Create the data which is needed by the background request
                let data = _asyncTaggerSource.GetDataForSnapshot(snapshot)
                let cancellationTokenSource = new CancellationTokenSource()
                let cancellationToken = cancellationTokenSource.Token
                let channel = new Channel()
                channel.WriteNormal lineRange

                // If there is an ITextView then make sure it is requested as well.  If the source provides an
                // ITextView then it is always prioritized on requests for a new snapshot
                match _asyncTaggerSource.TextView with
                | None -> ()
                | Some textView ->
                    match TextViewUtil.GetVisibleSnapshotLineRange textView with
                    | Some r -> channel.WriteVisibleLines r
                    | None -> ()

                // The background thread needs to know the set of values we have already queried
                // for.  Send a copy since this data will be mutated from a background thread
                let localVisited =
                    match _tagCache.BackgroundCacheData with
                    | Some backgroundCacheData ->
                        Contract.Requires(backgroundCacheData.Snapshot = snapshot)
                        backgroundCacheData.VisitedCollection.Copy()
                    | None -> NormalizedLineRangeCollection()

                // Function which finally gets the tags.  This is run on a background thread and can
                // throw as the implementor is encouraged to use CancellationToken::ThrowIfCancelled
                let localAsyncTaggerSource = _asyncTaggerSource
                let localChunkCount = _chunkCount
                let getTags() =
                    AsyncTagger<'TData, 'TTag>
                        .GetTagsInBackgroundCore
                            (localAsyncTaggerSource, data, localChunkCount, channel, localVisited, cancellationToken,
                             (fun completeReason ->
                                 synchronizationContext.Post
                                     ((fun _ ->
                                         x.OnGetTagsInBackgroundComplete completeReason channel cancellationTokenSource),
                                      null)),
                             (fun processedLineRange tagList ->
                                 synchronizationContext.Post
                                     ((fun _ ->
                                         x.OnGetTagsInBackgroundProgress cancellationTokenSource processedLineRange
                                             tagList), null)))

                // Create the Task which will handle the actual gathering of data.  If there is a delay
                // specified use it
                let localDelay = _asyncTaggerSource.Delay

                let taskAction() =
                    match localDelay with
                    | Some delay -> Thread.Sleep delay
                    | None -> ()

                    getTags()

                let task = new Task((fun _ -> taskAction()), cancellationToken)
                _asyncBackgroundRequest <-
                    { Snapshot = span.Snapshot
                      Channel = channel
                      Task = task
                      CancellationTokenSource = cancellationTokenSource }
                    |> Some

                task.Start(TaskScheduler.Default)

            // If we already have a background task running for this ITextSnapshot then just enqueue this
            // request onto that existing one.  By this point if the request exists it must be tuned to
            // this ITextSnapshot
            match _asyncBackgroundRequest with
            | None -> createNewRequest()
            | Some asyncBackgroundRequest ->
                if asyncBackgroundRequest.Snapshot = snapshot then
                    Contract.Requires(asyncBackgroundRequest.Snapshot = snapshot)
                    VimTrace.TraceDebug
                        ("AsyncTagger Background Existing {0} - {1}", lineRange.StartLineNumber,
                         lineRange.LastLineNumber)
                    asyncBackgroundRequest.Channel.WriteNormal lineRange
                else
                    x.CancelAsyncBackgroundRequest()
                    createNewRequest()

    [<UsedInBackgroundThread>]
    static member GetTagsInBackgroundCore(asyncTaggerSource: IAsyncTaggerSource<'TData, 'TTag>, data: 'TData,
                                          chunkCount: int, channel: Channel, visited: NormalizedLineRangeCollection,
                                          cancellationToken: CancellationToken, onComplete: CompleteReason -> unit,
                                          onProgress: SnapshotLineRange -> ReadOnlyCollection<ITagSpan<'TTag>> -> unit) =
        let completeReason =
            try
                // Keep track of the LineRange values which we've already provided tags for.  Don't
                // duplicate the work
                let toProcess = Queue<SnapshotLineRange>()

                // *** This value can be wrong ***
                // This is the version number we expect the Channel to have.  It's used
                // as a hueristic to determine if we should prioritize a value off of the stack or our
                // local stack.  If it's wrong it means we prioritize the wrong value.  Not a bug it
                // just changes the order in which values will appear
                let versionNumber = channel.CurrentVersion

                // Take one value off of the threadedLineRangeStack value.  If the value is bigger than
                // our chunking increment then we will add the value in chunks to the toProcess queue
                let popOne() =
                    match channel.Read() with
                    | None -> ()
                    | Some lineRange ->
                        if lineRange.Count <= chunkCount then
                            toProcess.Enqueue(lineRange)
                        else
                            let snapshot = lineRange.Snapshot
                            let mutable startLineNumber = lineRange.StartLineNumber
                            while startLineNumber <= lineRange.LastLineNumber do
                                let startLine = snapshot.GetLineFromLineNumber startLineNumber
                                let localRange = SnapshotLineRange.CreateForLineAndMaxCount startLine chunkCount
                                toProcess.Enqueue localRange
                                startLineNumber <- startLineNumber + chunkCount

                // Get the tags for the specified SnapshotLineRange and return the results.  No chunking is done here,
                // the data is just directly processed
                let getTags (tagLineRange: SnapshotLineRange) =
                    match visited.GetUnvisited tagLineRange.LineRange with
                    | None -> ()
                    | Some unvisited ->
                        let tagLineRange =
                            match SnapshotLineRange.CreateForLineNumberRange tagLineRange.Snapshot
                                      unvisited.StartLineNumber unvisited.LastLineNumber with
                            | NullableUtil.HasValue v -> v
                            | NullableUtil.Null -> tagLineRange

                        let tagList =
                            try
                                asyncTaggerSource.GetTagsInBackground data tagLineRange.ExtentIncludingLineBreak
                                    cancellationToken
                            with e ->
                                // Ignore exceptions that are thrown by IAsyncTaggerSource.  If the tagger threw then we consider
                                // the tags to be nothing for this span.
                                //
                                // It's important that we register some value here.  If we register nothing then the foreground will
                                // never see this slot as fulfilled and later requests for this span will eventually queue up another
                                // background request
                                VimTrace.TraceDebug("AsyncTagger source exception in background processing {0}", e)
                                EmptyTagList

                        visited.Add tagLineRange.LineRange
                        onProgress tagLineRange tagList

                let mutable isDone = false
                while not isDone && not (cancellationToken.IsCancellationRequested) do
                    let versionNumber = channel.CurrentVersion
                    popOne()

                    if 0 = toProcess.Count then
                        // We've drained both of the sources of input hence we are done
                        isDone <- true
                    else
                        // If at any point the threadLineRangeStack value changes we consider the new values to have
                        // priority over the old ones
                        while toProcess.Count > 0 && versionNumber = channel.CurrentVersion do

                            cancellationToken.ThrowIfCancellationRequested()
                            let lineRange = toProcess.Dequeue()
                            getTags lineRange

                if cancellationToken.IsCancellationRequested
                then CompleteReason.Cancelled
                else CompleteReason.Finished
            with
            | :? OperationCanceledException as e ->
                // Don't report cancellation exceptions.  These are thrown during cancellation for fast
                // break from the operation.  It's really a control flow mechanism
                CompleteReason.Cancelled
            | e ->
                // Handle cancellation exceptions and everything else.  Don't want an errant
                // exception thrown by the IAsyncTaggerSource to crash the process
                VimTrace.TraceDebug("AsyncTagger Exception in background processing {0}", e)
                CompleteReason.Error

        onComplete completeReason

    /// Cancel the pending AsyncBackgoundRequest if one is currently running
    member private x.CancelAsyncBackgroundRequest() =
        match _asyncBackgroundRequest with
        | None -> ()
        | Some asyncBackgroundRequest ->
            // Use a try / with to protect the Cancel from throwing and taking down the process
            try
                if not asyncBackgroundRequest.CancellationTokenSource.IsCancellationRequested then
                    asyncBackgroundRequest.CancellationTokenSource.Cancel()
            with _ -> ()

            _asyncBackgroundRequest <- None

    member private x.AdjustRequestSpan(col: NormalizedSnapshotSpanCollection) =
        if col.Count > 0 then
            // Note that we only use the overarching span to track what data we are responsible
            // for in a
            let requestSpan = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
            _cachedOverarchingRequestSpan <-
                TaggerUtilCore.AdjustRequestedSpan _cachedOverarchingRequestSpan requestSpan |> Some

    /// The background processing is now focussed on the given ITextSnapshot.  If anything is
    /// focused on the old ITextSnapshot move it to the specified one.
    member private x.AdjustToSnapshot snapshot =
        // First check and see if we need to move the existing background data to tracking data
        match _tagCache.BackgroundCacheData with
        | None -> ()
        | Some backgroundCacheData ->
            if backgroundCacheData.Snapshot <> snapshot then
                let trackingCacheData = backgroundCacheData.CreateTrackingCacheData()

                let trackingCacheData =
                    match _tagCache.TrackingCacheData with
                    | None -> trackingCacheData
                    | Some t -> trackingCacheData.Merge snapshot t

                _tagCache <- TagCache(None, Some trackingCacheData)

        // Next cancel any existing background request if it's not focused on this ITextSnapshot
        match _asyncBackgroundRequest with
        | None -> ()
        | Some asyncBackgroundRequest ->
            if asyncBackgroundRequest.Snapshot <> snapshot then x.CancelAsyncBackgroundRequest()

    member private x.RaiseTagsChanged span =
        let lineRange = SnapshotLineRange.CreateForSpan span
        VimTrace.TraceDebug
            ("AsyncTagger::RaiseTagsChanged {0} - {1}", lineRange.StartLineNumber, lineRange.LastLineNumber)

        _tagsChanged.Trigger x (SnapshotSpanEventArgs(span))

    /// Called when the IAsyncTaggerSource raises a Changed event.  Clear out the
    /// cache, pass on the event to the ITagger and wait for the next request
    member private x.OnAsyncTaggerSourceChanged() =
        // Clear out the cache.  It's no longer valid.
        _tagCache <- TagCache.Empty
        x.CancelAsyncBackgroundRequest()

        // Now if we've previously had a SnapshotSpan requested via GetTags go ahead
        // and tell the consumers that it's changed.  Use the entire cached request
        // span here.  We're pessimistic when we have a Changed call because we have
        // no information on what could've changed
        match _cachedOverarchingRequestSpan with
        | Some s -> x.RaiseTagsChanged s
        | None -> ()

    /// If the Layout changes while we are in the middle of getting tags we want to
    /// prioritize the new set of visible lines.
    member private x.OnLayoutChanged() =
        match _asyncTaggerSource.TextView, _asyncBackgroundRequest with
        | Some textView, Some asyncBackgroundRequest ->
            match TextViewUtil.GetVisibleSnapshotLineRange textView with
            | None -> ()
            | Some visibleLineRange ->
                if visibleLineRange.Snapshot = asyncBackgroundRequest.Snapshot then
                    asyncBackgroundRequest.Channel.WriteVisibleLines visibleLineRange
        | _ -> ()

    /// Is the async operation with the specified CancellationTokenSource the active
    /// background request
    member private x.IsActiveBackgroundRequest cancellationTokenSource =
        match _asyncBackgroundRequest with
        | None -> false
        | Some r -> r.CancellationTokenSource = cancellationTokenSource

    /// <summary>
    /// Called on the main thread when the request for tags has processed at least a small
    /// section of the file.  This funtion may be called many times for a single background
    /// request
    ///
    /// Called on the main thread
    /// </summary>
    member private x.OnGetTagsInBackgroundProgress cancellationTokenSource (lineRange: SnapshotLineRange) tagList =
        if x.IsActiveBackgroundRequest cancellationTokenSource then
            // Merge the existing background data if it's present and on the same ITextSnapshot
            let newData =
                match _tagCache.BackgroundCacheData with
                | Some oldData ->
                    if oldData.Snapshot = lineRange.Snapshot then
                        let tags = Seq.append oldData.TagList tagList |> ReadOnlyCollectionUtil.OfSeq
                        oldData.VisitedCollection.Add(lineRange.LineRange)
                        BackgroundCacheData(lineRange.Snapshot, oldData.VisitedCollection, tags)
                    else
                        BackgroundCacheData(lineRange, tagList)
                | None -> BackgroundCacheData(lineRange, tagList)

            _tagCache <- TagCache(Some newData, _tagCache.TrackingCacheData)

            // Determine if the tags changed on the given Span.  In an edit it's very possible and likely
            // that the ITagSpan we returned by simply mapping the SnapshotSpan forward was correct.  If
            // so then for a given SnapshotSpan we've already returned a result which was correct.  Raising
            // TagsChanged again for that SnapshotSpan will cause needless work to ocur (and potentially
            // more layouts
            let span = lineRange.ExtentIncludingLineBreak
            if x.DidTagsChange span tagList then x.RaiseTagsChanged span

    /// Called when the background request is completed
    ///
    /// Called on the main thread
    member private x.OnGetTagsInBackgroundComplete reason (channel: Channel) cancellationTokenSource =
        if x.IsActiveBackgroundRequest cancellationTokenSource then
            // The request is complete.  Reset the active request information
            x.CancelAsyncBackgroundRequest()

            // Update the tag cache to indicate we are no longer doing any tracking edits
            _tagCache <- TagCache(_tagCache.BackgroundCacheData, None)

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
            let stack = channel.CurrentStack
            if not (List.isEmpty stack) && reason = CompleteReason.Finished then
                stack |> List.iter (fun s -> x.GetTagsInBackground s.ExtentIncludingLineBreak)

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module TaggerUtil =

    let CreateAsyncTaggerRaw(asyncTaggerSource: IAsyncTaggerSource<'TData, 'TTag>) =
        let tagger = new AsyncTagger<'TData, 'TTag>(asyncTaggerSource)
        tagger :> ITagger<'TTag>

    let CreateAsyncTagger propertyCollection (key: obj) (createFunc: unit -> IAsyncTaggerSource<'TData, 'TTag>) =
        let createTagger() =
            let source = createFunc()
            CreateAsyncTaggerRaw source

        let countedTagger = new CountedTagger<'TTag>(propertyCollection, key, createTagger)
        countedTagger :> ITagger<'TTag>

    let CreateAsyncClassifierRaw(asyncTaggerSource: IAsyncTaggerSource<'TData, IClassificationTag>) =
        let tagger = CreateAsyncTaggerRaw asyncTaggerSource
        let classifier = new Classifier(tagger)
        classifier :> IClassifier

    let CreateAsyncClassifier
        propertyCollection
        (key: obj)
        (createFunc: unit -> IAsyncTaggerSource<'TData, IClassificationTag>)
        =
        let createClassifier() =
            let source = createFunc()
            CreateAsyncClassifierRaw source

        let countedClassifier = new CountedClassifier(propertyCollection, key, createClassifier)
        countedClassifier :> IClassifier

    let CreateBasicTaggerRaw(basicTaggerSource: IBasicTaggerSource<'TTag>) =
        let tagger = new BasicTagger<'TTag>(basicTaggerSource)
        tagger :> ITagger<'TTag>

    let CreateBasicTagger propertyCollection (key: obj) (createFunc: unit -> IBasicTaggerSource<'TTag>) =
        let createTagger() =
            let source = createFunc()
            CreateBasicTaggerRaw source

        let countedTagger = new CountedTagger<'TTag>(propertyCollection, key, createTagger)
        countedTagger :> ITagger<'TTag>

    let CreateBasicClassifierRaw(basicTaggerSource: IBasicTaggerSource<IClassificationTag>) =
        let tagger = CreateBasicTaggerRaw basicTaggerSource
        let classifier = new Classifier(tagger)
        classifier :> IClassifier

    let CreateBasicClassifier propertyCollection (key: obj) (createFunc: unit -> IBasicTaggerSource<IClassificationTag>) =
        let createClassifier() =
            let source = createFunc()
            CreateBasicClassifierRaw source

        let countedClassifier = new CountedClassifier(propertyCollection, key, createClassifier)
        countedClassifier :> IClassifier

    let GetOrCreateOutliner(textBuffer: ITextBuffer) =
        let outliner = AdhocOutliner.GetOrCreate textBuffer
        outliner :> IAdhocOutliner

    let CreateOutlinerTagger textBuffer =
        let createSource() =
            let source = AdhocOutliner.GetOrCreate textBuffer
            source :> IBasicTaggerSource<OutliningRegionTag>

        let key = AdhocOutliner.GetOrCreate
        CreateBasicTagger textBuffer.Properties AdhocOutliner.OutlinerTaggerKey createSource
