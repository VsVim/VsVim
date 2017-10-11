#light

namespace Vim
open Microsoft.VisualStudio.Text
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
    let AdjustRequestedSpan (cachedRequestSpan : Nullable<SnapshotSpan>) (requestSpan : SnapshotSpan) =
        match cachedRequestSpan with
        | NullableUtil.Null -> requestSpan
        | NullableUtil.HasValue cachedRequestSpan ->
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
