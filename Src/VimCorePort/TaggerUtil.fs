#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Tagging;
open System.Collections.ObjectModel;
open Microsoft.VisualStudio.Utilities
open System
open System.Diagnostics
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Threading
open Vim.ToDelete
open System.Net

module TaggerUtil = 

    /// The simple taggers when changed need to provide an initial SnapshotSpan 
    /// for the TagsChanged event.  It's important that this SnapshotSpan be kept as
    /// small as possible.  If it's incorrectly large it can have a negative performance
    /// impact on the editor.  In particular
    ///
    /// 1. The value is directly provided in ITagAggregator::TagsChanged.  This value
    ///    is acted on directly by many editor components.  Providing a large range 
    ///    unnecessarily increases their work load.
    /// 2. It can cause a ripple effect in Visual Studio 2010 RTM.  The SnapshotSpan 
    ///    returned will be immediately be the vale passed to GetTags for every other
    ///    ITagger in the system (TextMarkerVisualManager issue). 
    ///
    /// In order to provide the minimum possible valid SnapshotSpan the simple taggers
    /// cache the overarching SnapshotSpan for the latest ITextSnapshot of all requests
    /// to which they are given.
    let AdjustRequestedSpan (cachedRequestSpan : SnapshotSpan option) (requestSpan : SnapshotSpan) =
        match cachedRequestSpan with
        | None -> requestSpan
        | Some cachedRequestSpan ->
            let cachedSnapshot = cachedRequestSpan.Snapshot
            let requestSnapshot = requestSpan.Snapshot
            if cachedSnapshot = requestSnapshot then
                // Same snapshot so we just need the overarching SnapshotSpan
                EditorUtil.CreateOverarching cachedRequestSpan requestSpan
            elif cachedSnapshot.Version.VersionNumber < requestSnapshot.Version.VersionNumber then
                // Request for a span on a new ITextSnapshot.  Translate the old SnapshotSpan
                // to the new ITextSnapshot and get the overarching value 
                let trackingSpan = cachedSnapshot.CreateTrackingSpan(cachedRequestSpan.Span, SpanTrackingMode.EdgeInclusive)
                match TrackingSpanUtil.GetSpan requestSnapshot trackingSpan with
                | Some s -> EditorUtil.CreateOverarching s requestSpan
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
type internal CountedValue<'T> 
    (
        _value : 'T,
        _key : obj,
        _propertyCollection : PropertyCollection
    ) = 

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

    static member GetOrCreate propertyCollection key (createFunc : unit -> 'T) = 
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
type internal CountedTagger<'TTag when 'TTag :> ITag>  =

    val private _countedValue : CountedValue<ITagger<'TTag>>

    member x.Tagger = x._countedValue.Value

    member x.Dispose() = x._countedValue.Release()

    new (propertyCollection : PropertyCollection, key : obj, createFunc : (unit -> ITagger<'TTag>)) =
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

    val private _countedValue : CountedValue<IClassifier>

    member x.Classifier = x._countedValue.Value

    member x.Dispose() = x._countedValue.Release()

    new (propertyCollection : PropertyCollection, key : obj, createFunc : (unit -> IClassifier)) =
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
    {
        TrackingSpan : ITrackingSpan;
        Tag : OutliningRegionTag;
        Cookie : int
    }

/// Implementation of the IAdhocOutliner service.  This class is only used for testing
/// so it's focused on creating the simplest implementation vs. the most efficient
type internal AdhocOutliner 
    (
        _textBuffer : ITextBuffer
    ) =

    let _map = new Dictionary<int, OutliningData>()
    let mutable _counter = 0 
    let _changed = StandardEvent()

    static member OutlinerKey = new obj();
    static member OutlinerTaggerKey = new obj();
    static member EmptyCollection = new ReadOnlyCollection<OutliningRegion>([| |])

    /// The outlining implementation is worthless unless it is also registered as an ITagger 
    /// component.  If this hasn't happened by the time the APIs are being queried then it is
    /// a bug and we need to notify the developer
    member x.EnsureTagger() = 
        if not (_textBuffer.Properties.ContainsProperty(AdhocOutliner.OutlinerTaggerKey)) then
            let msg = "In order to use IAdhocOutliner you must also export an ITagger implementation for the buffer which return CreateOutliningTagger";
            raise (new Exception(msg))

    /// Get all of the values which map to the given ITextSnapshot
    member private x.GetOutliningRegions (span : SnapshotSpan) =
        // Avoid allocating a map or new collection if we are simply empty
        if _map.Count = 0 then
            AdhocOutliner.EmptyCollection
        else 
            let snapshot = span.Snapshot
            let list = new List<OutliningRegion>()
            for cur in _map.Values do
                match TrackingSpanUtil.GetSpan snapshot cur.TrackingSpan with
                | None -> ()
                | Some currentSpan -> list.Add({ Tag = cur.Tag; Span = currentSpan; Cookie = cur.Cookie });

            ReadOnlyCollection<OutliningRegion>(list)

    member private x.CreateOutliningRegion (span : SnapshotSpan) spanTrackingMode text hint = 
        let trackingSpan = span.Snapshot.CreateTrackingSpan(span.Span, spanTrackingMode) 
        let tag = OutliningRegionTag(text, hint) 
        let data = { TrackingSpan = trackingSpan; Tag = tag; Cookie = _counter }
        _map.[_counter] <- data
        _counter <- _counter + 1
        _changed.Trigger x
        { Tag = tag; Span = span; Cookie = data.Cookie }

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

type internal Classifier
    (
        _tagger : ITagger<IClassificationTag>
    ) as this =

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
        | _  -> ()

    interface IClassifier with 
        member x.GetClassificationSpans span = 
            let col = NormalizedSnapshotSpanCollection(span)
            let tags =
                _tagger.GetTags(col)
                |> Seq.map (fun x -> ClassificationSpan(x.Span, x.Tag.ClassificationType))
            List<ClassificationSpan>(tags) :> IList<ClassificationSpan>
        [<CLIEvent>]
        member x.ClassificationChanged = _changed.Publish

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal BasicTagger<'TTag when 'TTag :> ITag>
    (
        _basicTaggerSource : IBasicTaggerSource<'TTag>
    ) as this = 

    let _changed = StandardEvent<SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()
    let mutable _cachedRequestSpan : SnapshotSpan option = None

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
        with get() = _cachedRequestSpan
        and set value = _cachedRequestSpan <- value

    member private x.Dispose() =
        _eventHandlers.DisposeAll()
        match _basicTaggerSource with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()

    member x.GetTags (col : NormalizedSnapshotSpanCollection) = 
        if col.Count > 0 then
            let span = NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan col
            _cachedRequestSpan <- Some (TaggerUtil.AdjustRequestedSpan _cachedRequestSpan span)

        match col.Count with
        | 0 -> Seq.empty
        | 1 -> _basicTaggerSource.GetTags (col.[0]) :> ITagSpan<'TTag> seq
        | _ -> col |> Seq.collect (fun x -> _basicTaggerSource.GetTags x)

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _changed.Publish
    
    interface IDisposable with
        member x.Dispose() = x.Dispose()