#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.Generic

/// Contains the raw data used by the IFoldManager.  The fold manager needs to have 
/// ITextView based information like the outlining manager but IOutliningRegion tags
/// need to be at the ITextBuffer level.  The FoldManagerData works at the ITextBuffer
/// level providing tags while letting the IFoldManager work at a view level giving
/// ITextView level commands
[<Sealed>]
type internal FoldData
    (
        _textBuffer : ITextBuffer
    ) =

    let _updated = Event<System.EventArgs>()
    let mutable _folds : ITrackingSpan list = List.empty

    /// Get the SnapshotSpan values which represent folded regions in the ITextView which
    /// were created by vim
    member x.Folds = 
        _folds 
        |> Seq.ofList 
        |> Seq.map (TrackingSpanUtil.GetSpan _textBuffer.CurrentSnapshot) 
        |> SeqUtil.filterToSome
        |> Seq.sortBy (fun span -> span.Start.Position)

    /// Create a fold over the given line range
    member x.CreateFold (range : SnapshotLineRange) = 
        if range.Count > 1 then

            // Note we only use the Extent of the range and do not include the line break.  If
            // we included the line break then it would cause the line after the collapsed region
            // to appear on the same line as the folded region
            let span = range.Extent
            let trackingSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)

            _folds <- trackingSpan :: _folds
            _updated.Trigger System.EventArgs.Empty

    /// Delete the fold which corresponds to the given SnapshotPoint
    member x.DeleteFold (point:SnapshotPoint) = 
        let snapshot = _textBuffer.CurrentSnapshot
        let data = 
            _folds
            |> Seq.map (fun span -> TrackingSpanUtil.GetSpan snapshot span,span)
            |> Seq.map OptionUtil.combine2
            |> SeqUtil.filterToSome
            |> Seq.sortBy (fun (span,_) -> span.Start.Position)
        let ret = 
            match data |> Seq.tryFind (fun (span,_) -> span.Contains(point)) with
            | None -> false
            | Some(_,tracking) -> 
                _folds <- _folds |> Seq.ofList |> Seq.filter (fun t -> t <> tracking) |> List.ofSeq
                true
        _updated.Trigger System.EventArgs.Empty
        ret

    /// Delete all folds which intersect the given SnapshotSpan
    member x.DeleteAllFolds (span : SnapshotSpan) =
        _folds <-
            _folds
            |> Seq.map (TrackingSpanUtil.GetSpan _textBuffer.CurrentSnapshot)
            |> SeqUtil.filterToSome
            |> Seq.filter (fun s -> not (span.IntersectsWith(s)))
            |> Seq.map (fun span -> _textBuffer.CurrentSnapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive))
            |> List.ofSeq
        _updated.Trigger System.EventArgs.Empty

    interface IFoldData with
        member x.TextBuffer = _textBuffer
        member x.Folds = x.Folds
        member x.CreateFold range = x.CreateFold range
        member x.DeleteFold point = x.DeleteFold point
        member x.DeleteAllFolds span = x.DeleteAllFolds span
        [<CLIEvent>]
        member x.FoldsUpdated = _updated.Publish

type internal FoldManager 
    (
        _textView : ITextView,
        _foldData : IFoldData,
        _statusUtil : IStatusUtil,
        _outliningManager : IOutliningManager option
    ) =

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.CurrentSnapshot = _textView.TextSnapshot

    /// Even though Vim is line based we have to deal with regions provided by other plug-ins
    /// or Visual Studio which are not line based.  So look for any regions which cross the
    /// provided line
    member x.GetSpanForRegions point =
        point
        |> SnapshotPointUtil.GetContainingLine
        |> SnapshotLineUtil.GetExtentIncludingLineBreak

    /// Close 'count' folds under the given SnapshotPoint
    member x.CloseFold point count = 
        x.DoWithOutliningManager (fun outliningManager -> 
            // Get the collapsed regions and map them to SnapshotSpan values in this buffer.  Then
            // order them by most start and then length
            let span = x.GetSpanForRegions point
            outliningManager.GetAllRegions(span)
            |> Seq.filter (fun region -> not region.IsCollapsed)
            |> List.ofSeq
            |> List.rev
            |> Seq.truncate count
            |> Seq.iter (fun region -> outliningManager.TryCollapse(region) |> ignore))

    /// Close all folds which intersect with the given SnapshotSpan
    member x.CloseAllFolds (span : SnapshotSpan) =
        x.DoWithOutliningManager (fun outliningManager -> 
            // Get the collapsed regions and map them to SnapshotSpan values in this buffer.  Then
            // order them by most start and then length
            outliningManager.GetAllRegions(span)
            |> Seq.filter (fun region -> not region.IsCollapsed)
            |> Seq.iter (fun region -> outliningManager.TryCollapse(region) |> ignore))

    /// Create a fold over the given line range
    member x.CreateFold (range : SnapshotLineRange) = 
        _foldData.CreateFold range
        if range.Count > 1 then

            // Folds should be collapsed by default in vim.  Need to communicate with the outlining
            // manager to have this happen
            x.DoWithOutliningManager (fun outliningManager -> 
                outliningManager.CollapseAll(range.Extent, (fun collapsible ->
                    // The CollapseAll function will run the predicate over every ICollpasible which crosses
                    // the provided SnapshotSpan.  We only want to collapse the single region we just created
                    let span = TrackingSpanUtil.GetSpan _textView.TextSnapshot collapsible.Extent
                    match span with
                    | None -> false
                    | Some span -> span = range.Extent)) |> ignore)

    /// Do the given operation with the IOutliningManager.  If there is no IOutliningManager
    /// associated with this ITextView then no action will occur and an error message will
    /// be raised to the user
    member x.DoWithOutliningManager (action : IOutliningManager -> unit) = 
        match _outliningManager with
        | None -> _statusUtil.OnWarning Resources.Internal_FoldsNotSupported
        | Some outliningManager -> action outliningManager

    /// Delete the fold which corresponds to the given SnapshotPoint
    member x.DeleteFold point =
        if not (_foldData.DeleteFold point) then
            _statusUtil.OnError Resources.Common_NoFoldFound

    member x.DeleteAllFolds span =
        _foldData.DeleteAllFolds span

    /// Open 'count' folds under the point.  There is no explicit documentation on which folds should
    /// be opened when several folds exist under the caret but experimentation shows that it appears
    /// to be the most encompassing first followed by inner folds later.  The ordering provided
    /// by the IOutliningManager appears to mimic this ordering
    ///
    /// This method operates both on vim folds and any other outlining region
    member x.OpenFold point count = 
        x.DoWithOutliningManager (fun outliningManager -> 

            // Get the collapsed regions and map them to SnapshotSpan values in this buffer.  Then
            // order them by most start and then length
            let span = x.GetSpanForRegions point
            outliningManager.GetCollapsedRegions(span)
            |> Seq.truncate count
            |> Seq.iter (fun region -> outliningManager.Expand(region) |> ignore))

    /// Open all folds which intersect the given SnapshotSpan value
    member x.OpenAllFolds (span : SnapshotSpan) =
        x.DoWithOutliningManager (fun outliningManager -> 

            outliningManager.GetCollapsedRegions(span)
            |> Seq.iter (fun region -> outliningManager.Expand(region) |> ignore))

    interface IFoldManager with
        member x.TextView = _textView
        member x.CloseFold point count = x.CloseFold point count
        member x.CloseAllFolds span = x.CloseAllFolds span
        member x.CreateFold range = x.CreateFold range
        member x.DeleteFold point = x.DeleteFold point
        member x.DeleteAllFolds span = x.DeleteAllFolds span
        member x.OpenFold point count = x.OpenFold point count
        member x.OpenAllFolds span = x.OpenAllFolds span

/// Fold tagger for the IOutliningRegion tags created by folds.  Note that folds work
/// on an ITextBuffer level and not an ITextView level.  Hence this works directly with
/// IFoldManagerData instead of IFoldManager
type internal FoldTagger(_foldData : IFoldData) as this =

    let _textBuffer = _foldData.TextBuffer
    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()

    do 
        let handle _ = _tagsChanged.Trigger(this, new SnapshotSpanEventArgs(SnapshotUtil.GetExtent _textBuffer.CurrentSnapshot))
        _foldData.FoldsUpdated |> Event.add handle

    member x.GetTags (col : NormalizedSnapshotSpanCollection) =

        // Get the description for the given SnapshotSpan.  This is the text displayed for
        // the folded lines.
        let getDescription span = 
            let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
            sprintf "%d lines ---" ((endLine.LineNumber - startLine.LineNumber) + 1)

        if col.Count = 0 then
            Seq.empty
        else 
            let snapshot = (col.Item(0)).Snapshot
            _foldData.Folds
            |> Seq.filter ( fun span -> span.Snapshot = snapshot )
            |> Seq.map (fun span ->
                let description = getDescription span
                let hint = span.GetText()
                let tag = OutliningRegionTag(true, true, description, hint)
                TagSpan<OutliningRegionTag>(span, tag) :> ITagSpan<OutliningRegionTag> )

    interface ITagger<OutliningRegionTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.ContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<OutliningRegionTag>)>]
type FoldTaggerProvider
    [<ImportingConstructor>]
    (_factory : IFoldManagerFactory) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> textBuffer =
            let foldData = _factory.GetFoldData textBuffer
            let tagger = FoldTagger(foldData)
            tagger :> obj :?> ITagger<'T>

[<Export(typeof<IFoldManagerFactory>)>]
type FoldManagerFactory
    [<ImportingConstructor>]
    (
        _statusUtilFactory : IStatusUtilFactory,
        _outliningManagerService : IOutliningManagerService
    ) =

    /// Use an object instance as a key.  Makes it harder for components to ignore this
    /// service and instead manually query by a predefined key
    let _dataKey = new System.Object()

    let _managerKey = new System.Object()

    member x.GetFoldData (textBuffer : ITextBuffer) = 
        textBuffer.Properties.GetOrCreateSingletonProperty(_dataKey, (fun _ -> FoldData(textBuffer)))

    member x.GetFoldManager (textView : ITextView) = 
        textView.Properties.GetOrCreateSingletonProperty(_managerKey, (fun _ ->
            let outliningManager = 
                let outliningManager = _outliningManagerService.GetOutliningManager textView
                if outliningManager = null then
                    None
                else
                    Some outliningManager
            let statusUtil = _statusUtilFactory.GetStatusUtil textView
            let foldData = x.GetFoldData(textView.TextBuffer)
            FoldManager(textView, foldData :> IFoldData, statusUtil, outliningManager) :> IFoldManager))

    interface IFoldManagerFactory with
        member x.GetFoldData textBuffer = x.GetFoldData textBuffer :> IFoldData
        member x.GetFoldManager textView = x.GetFoldManager textView

