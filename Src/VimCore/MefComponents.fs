#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.Generic
open System.Diagnostics
open System

/// This is the type responsible for tracking a line + column across edits to the
/// underlying ITextBuffer.  In a perfect world this would be implemented as an 
/// ITrackingSpan so we didn't have to update the locations on every single 
/// change to the ITextBuffer.  
///
/// Unfortunately the limitations of the ITrackingSpaninterface prevent us from doing 
/// that. One strategy you might employ is to say you'll track the Span which represents
/// the extent of the line you care about.  This works great right until you consider
/// the case where the line break before your Span is deleted.  Because you can't access 
/// the full text of that ITextVersion you can't "find" the new front of the line
/// you are tracking (same happens with the end).
///
/// So for now we're stuck with updating these on every edit to the ITextBuffer.  Not
/// ideal but will have to do for now.  
type internal TrackingLineColumn 
    ( 
        _textBuffer: ITextBuffer,
        _offset: int,
        _mode: LineColumnTrackingMode,
        _onClose: TrackingLineColumn -> unit
    ) =

    /// This is the SnapshotSpan of the line that we are tracking.  It is None in the
    /// case of a deleted line
    let mutable _line: ITextSnapshotLine option  = None

    /// When the line this TrackingLineColumn is deleted, this will record the version 
    /// number of the last valid version containing the line.  That way if we undo this 
    /// can become valid again
    let mutable _lastValidVersion: (int * int) option  = None

    let mutable _offset = _offset

    member x.TextBuffer = _textBuffer

    member x.Line 
        with get() = _line
        and set value = _line <- value

    member x.Offset = _offset

    member x.IsValid = Option.isSome _line

    member x.VirtualPoint = 
        match _line with
        | None -> None
        | Some line -> Some (VirtualSnapshotPoint(line, _offset))

    member x.Point = 
        match x.VirtualPoint with
        | None -> None
        | Some point -> Some point.Position

    member x.Close () =
        _onClose x
        _line <- None
        _lastValidVersion <- None

    /// Update the internal tracking information based on the new ITextSnapshot
    member x.OnBufferChanged (e: TextContentChangedEventArgs) =
        match _line with 
        | Some snapshotLine ->
            if e.AfterVersion <> snapshotLine.Snapshot.Version then
                x.AdjustForChange snapshotLine e
        | None -> x.CheckForUndo e

    /// The change occurred and we are in a valid state.  Update our cached data against 
    /// this change
    ///
    /// Note: This method occurs on a hot path, especially in macro playback, hence we
    /// take great care to avoid allocations on this path whenever possible
    member x.AdjustForChange (oldLine: ITextSnapshotLine) (e: TextContentChangedEventArgs) =
        let changes = e.Changes
        match _mode with
        | LineColumnTrackingMode.Default ->
            if x.IsLineDeletedByChange oldLine changes then
                // If this shouldn't survive a full line deletion and there is a deletion
                // then we are invalid
                x.MarkInvalid oldLine
            else
                x.AdjustForChangeCore oldLine e
        | LineColumnTrackingMode.LastEditPoint ->
            if x.IsLineDeletedByChange oldLine changes then
                _offset <- 0

            let newSnapshot = e.After
            let number = min oldLine.LineNumber (newSnapshot.LineCount - 1)
            _line <- Some (newSnapshot.GetLineFromLineNumber number)
        | LineColumnTrackingMode.SurviveDeletes -> x.AdjustForChangeCore oldLine e

    member x.AdjustForChangeCore (oldLine: ITextSnapshotLine) (e: TextContentChangedEventArgs) =
        let newSnapshot = e.After
        let changes = e.Changes

        // Calculate the line number delta for this given change. All we care about here
        // is the line number.  So changes line shortening the line don't matter for 
        // us because the column position is fixed
        let mutable delta = 0
        for change in changes do
            if change.LineCountDelta <> 0 && change.OldPosition <= oldLine.Start.Position then
                // The change occurred before our line and there is a delta.  This is the 
                // delta we need to apply to our line number
                delta <- delta + change.LineCountDelta

        let number = oldLine.LineNumber + delta
        if number >= 0 && number < newSnapshot.LineCount then
            _line <- Some (newSnapshot.GetLineFromLineNumber number) 
        else
            x.MarkInvalid oldLine

    /// Is the specified line deleted by this change
    member x.IsLineDeletedByChange (snapshotLine: ITextSnapshotLine) (changes: INormalizedTextChangeCollection) =

        if changes.IncludesLineChanges then
            let span = snapshotLine.ExtentIncludingLineBreak.Span

            let mutable isDeleted = false
            for change in changes do
                if change.LineCountDelta < 0 && change.OldSpan.Contains span then  
                    isDeleted <- true

            isDeleted
        else
            false

    /// This line was deleted at some point in the past and hence we're invalid.  If the 
    /// current change is an Undo back to the last version where we were valid then we
    /// become valid again
    member x.CheckForUndo (e: TextContentChangedEventArgs) = 
        Debug.Assert(not x.IsValid)
        match _lastValidVersion with
        | Some (version, lineNumber) ->
            let newSnapshot = e.After
            let newVersion = e.AfterVersion
            if newVersion.ReiteratedVersionNumber = version && lineNumber <= newSnapshot.LineCount then 
                _line <- Some (newSnapshot.GetLineFromLineNumber(lineNumber))
                _lastValidVersion <- None
        | None -> ()

    /// For whatever reason this is now invalid.  Store the last good information so we can
    /// recover during an undo operation
    member x.MarkInvalid (snapshotLine: ITextSnapshotLine) =
        _line <- None
        _lastValidVersion <- Some (snapshotLine.Snapshot.Version.ReiteratedVersionNumber, snapshotLine.LineNumber)

    override x.ToString() =
        match x.VirtualPoint with
        | Some point ->
            let line = SnapshotPointUtil.GetContainingLine point.Position
            sprintf "%d,%d - %s" line.LineNumber _offset (point.ToString())
        | None -> "Invalid"

    interface ITrackingLineColumn with
        member x.TextBuffer = _textBuffer
        member x.TrackingMode = _mode
        member x.VirtualPoint = x.VirtualPoint
        member x.Point = x.Point
        member x.Column = match x.Point with Some p -> Some (SnapshotColumn(p)) | None -> None
        member x.VirtualColumn = match x.VirtualPoint with Some p -> Some (VirtualSnapshotColumn(p)) | None -> None
        member x.Close() = x.Close()

/// Implementation of ITrackingVisualSpan which is used to track a VisualSpan across edits
/// to the ITextBuffer
[<RequireQualifiedAccess>]
type internal TrackingVisualSpan =

    /// Tracks the origin SnapshotPoint of the character span, the number of lines it 
    /// composes of and the length of the span on the last line
    | Character of TrackingLineColumn: ITrackingLineColumn * LineCount: int * LastLineMaxPositionCount: int

    /// Tracks the origin of the SnapshotLineRange and the number of lines it should comprise
    /// of
    | Line of TrackingLineColumn: ITrackingLineColumn * LineCount: int

    /// Tracks the origin of the block selection, it's tabstop by width by height
    | Block of TrackingLineColumn: ITrackingLineColumn * TabStop: int * Width: int * Height: int * EndOfLine: bool

    with

    member x.TrackingLineColumn =
        match x with
        | Character (trackingLineColumn, _, _) -> trackingLineColumn
        | Line (trackingLineColumn, _) -> trackingLineColumn
        | Block (trackingLineColumn, _, _, _, _) -> trackingLineColumn

    member x.TextBuffer =
        x.TrackingLineColumn.TextBuffer

    /// Calculate the VisualSpan against the current ITextSnapshot
    member x.VisualSpan = 
        let snapshot = x.TextBuffer.CurrentSnapshot

        match x.TrackingLineColumn.Point with
        | None ->
            None
        | Some point ->
            match x with
            | Character (_, lineCount, lastLineMaxPositionCount) ->
                let characterSpan = CharacterSpan(point, lineCount, lastLineMaxPositionCount)
                VisualSpan.Character characterSpan
            | Line (_, count) ->
                let line = SnapshotPointUtil.GetContainingLine point
                SnapshotLineRangeUtil.CreateForLineAndMaxCount line count 
                |> VisualSpan.Line
            | Block (_, tabStop, width, height, endOfLine) ->
                let virtualPoint = VirtualSnapshotPointUtil.OfPoint point
                let blockSpan = BlockSpan(virtualPoint, tabStop, width, height, endOfLine)
                VisualSpan.Block blockSpan
            |> Some

    member x.Close() =
        match x with
        | Character (trackingLineColumn, _, _) -> trackingLineColumn.Close()
        | Line (trackingLineColumn, _) -> trackingLineColumn.Close()
        | Block (trackingLineColumn, _, _, _, _) -> trackingLineColumn.Close()

    static member Create (bufferTrackingService: IBufferTrackingService) visualSpan =
        match visualSpan with
        | VisualSpan.Character characterSpan ->

            // Implemented by tracking the start point of the SnapshotSpan, the number of lines
            // in the span and the length of the final line
            let textBuffer = characterSpan.Snapshot.TextBuffer
            let trackingLineColumn = 
                let line, offset = VirtualSnapshotPointUtil.GetLineAndOffset characterSpan.VirtualStart
                bufferTrackingService.CreateLineOffset textBuffer line.LineNumber offset LineColumnTrackingMode.Default

            TrackingVisualSpan.Character (trackingLineColumn, characterSpan.LineCount, characterSpan.LastLineMaxPositionCount)

        | VisualSpan.Line snapshotLineRange ->

            // Setup an ITrackingLineColumn at the 0 column of the first line.  This actually may be doable
            // with an ITrackingPoint but for now sticking with an ITrackinglineColumn
            let textBuffer = snapshotLineRange.Snapshot.TextBuffer
            let trackingLineColumn = bufferTrackingService.CreateLineOffset textBuffer snapshotLineRange.StartLineNumber 0 LineColumnTrackingMode.Default
            TrackingVisualSpan.Line (trackingLineColumn, snapshotLineRange.Count)

        | VisualSpan.Block blockSpan ->

            // Setup an ITrackLineColumn at the top left of the block selection
            let trackingLineColumn =
                let textBuffer = blockSpan.TextBuffer
                let line, offset = VirtualSnapshotPointUtil.GetLineAndOffset blockSpan.VirtualStart.VirtualStartPoint
                bufferTrackingService.CreateLineOffset textBuffer line.LineNumber offset LineColumnTrackingMode.Default

            TrackingVisualSpan.Block (trackingLineColumn, blockSpan.TabStop, blockSpan.SpacesLength, blockSpan.Height, blockSpan.EndOfLine)

    interface ITrackingVisualSpan with
        member x.TextBuffer = x.TextBuffer
        member x.VisualSpan = x.VisualSpan
        member x.Close() = x.Close()

type internal TrackingVisualSelection 
    (
        _trackingVisualSpan: ITrackingVisualSpan,
        _path: SearchPath,
        _column: int,
        _blockCaretLocation: BlockCaretLocation
    ) =

    member x.VisualSelection =
        match _trackingVisualSpan.VisualSpan with
        | None -> 
            None
        | Some visualSpan -> 
            let visualSelection =
                match visualSpan with
                | VisualSpan.Character span -> VisualSelection.Character (span, _path)
                | VisualSpan.Line lineRange -> VisualSelection.Line (lineRange, _path, _column)
                | VisualSpan.Block blockSpan -> VisualSelection.Block (blockSpan, _blockCaretLocation)
            Some visualSelection

    member x.CaretPoint =
        x.VisualSelection |> Option.map (fun visualSelection -> visualSelection.GetCaretPoint SelectionKind.Inclusive)

    /// Get the caret in the given VisualSpan
    member x.GetCaret visualSpan = 
        x.VisualSelection |> Option.map (fun visualSelection -> visualSelection.GetCaretPoint SelectionKind.Inclusive)

    /// Create an ITrackingVisualSelection with the provided data
    static member Create (bufferTrackingService: IBufferTrackingService) (visualSelection: VisualSelection)=

        // Track the inclusive VisualSpan.  Internally the VisualSelection type represents values as inclusive
        // ones.  
        let trackingVisualSpan = bufferTrackingService.CreateVisualSpan visualSelection.VisualSpan
        let path, column, blockCaretLocation =
            match visualSelection with
            | VisualSelection.Character (_, path) -> path, 0, BlockCaretLocation.TopRight
            | VisualSelection.Line (_, path, column) -> path, column, BlockCaretLocation.TopRight
            | VisualSelection.Block (_, blockCaretLocation) -> SearchPath.Forward, 0, blockCaretLocation
        TrackingVisualSelection(trackingVisualSpan, path, column, blockCaretLocation)

    interface ITrackingVisualSelection with
        member x.CaretPoint = x.CaretPoint
        member x.TextBuffer = _trackingVisualSpan.TextBuffer
        member x.VisualSelection = x.VisualSelection
        member x.Close() = _trackingVisualSpan.Close()

type internal TrackedData = {
    List: List<TrackingLineColumn>
    Observer: System.IDisposable 
}

/// Service responsible for tracking various parts of an ITextBuffer which can't be replicated
/// by simply using ITrackingSpan or ITrackingPoint.
[<Export(typeof<IBufferTrackingService>)>]
[<Sealed>]
type internal BufferTrackingService() = 

    static let _key = obj()

    member x.FindTrackedData (textBuffer: ITextBuffer) =
        PropertyCollectionUtil.GetValue<TrackedData> _key textBuffer.Properties
    
    /// Remove the TrackingLineColumn from the map.  If it is the only remaining 
    /// TrackingLineColumn assigned to the ITextBuffer, remove it from the map
    /// and unsubscribe from the Changed event
    member x.Remove (trackingLineColumn: TrackingLineColumn) = 
        let textBuffer = trackingLineColumn.TextBuffer
        match x.FindTrackedData textBuffer with
        | Some trackedData -> 
            trackedData.List.Remove trackingLineColumn |> ignore
            if trackedData.List.Count = 0 then
                trackedData.Observer.Dispose()
                textBuffer.Properties.RemoveProperty _key |> ignore
        | None -> ()

    /// Add the TrackingLineColumn to the map.  If this is the first item in the
    /// map then subscribe to the Changed event on the buffer
    member x.Add (trackingLineColumn: TrackingLineColumn) =
        let textBuffer = trackingLineColumn.TextBuffer
        let trackedData = 
            match x.FindTrackedData textBuffer with
            | Some trackedData -> trackedData
            | None -> 
                let observer = textBuffer.Changed |> Observable.subscribe x.OnBufferChanged
                let trackedData = { List = List<TrackingLineColumn>(); Observer = observer }
                textBuffer.Properties.AddProperty(_key, trackedData)
                trackedData
        trackedData.List.Add trackingLineColumn

    member x.OnBufferChanged (e: TextContentChangedEventArgs) = 
        match x.FindTrackedData e.Before.TextBuffer with
        | Some trackedData -> trackedData.List |> Seq.iter (fun trackingLineColumn -> trackingLineColumn.OnBufferChanged e)
        | None -> ()

    member x.CreateLineOffset (textBuffer: ITextBuffer) lineNumber offset mode = 
        let trackingLineColumn = TrackingLineColumn(textBuffer, offset, mode, x.Remove)
        let textSnapshot = textBuffer.CurrentSnapshot
        let textLine = textSnapshot.GetLineFromLineNumber(lineNumber)
        trackingLineColumn.Line <-  Some textLine
        x.Add trackingLineColumn
        trackingLineColumn

    member x.CreateColumn (column: SnapshotColumn) mode =
        let textBuffer = column.Snapshot.TextBuffer
        x.CreateLineOffset textBuffer column.LineNumber column.Offset mode

    member x.HasTrackingItems textBuffer = 
        x.FindTrackedData textBuffer |> Option.isSome

    interface IBufferTrackingService with
        member x.CreateLineOffset textBuffer lineNumber offset mode = x.CreateLineOffset textBuffer lineNumber offset mode :> ITrackingLineColumn
        member x.CreateColumn column mode = x.CreateColumn column mode :> ITrackingLineColumn
        member x.CreateVisualSpan visualSpan = TrackingVisualSpan.Create x visualSpan :> ITrackingVisualSpan
        member x.CreateVisualSelection visualSelection = TrackingVisualSelection.Create x visualSelection :> ITrackingVisualSelection
        member x.HasTrackingItems textBuffer = x.HasTrackingItems textBuffer

/// Component which monitors commands across IVimBuffer instances and 
/// updates the LastCommand value for repeat purposes
[<Export(typeof<IVimBufferCreationListener>)>]
type internal ChangeTracker
    [<ImportingConstructor>]
    (
        _vim: IVim
    ) =

    let _vimData = _vim.VimData

    member x.OnVimBufferCreated (vimBuffer: IVimBuffer) =
        let handler = x.OnCommandRan
        vimBuffer.NormalMode.CommandRunner.CommandRan |> Event.add handler
        vimBuffer.VisualLineMode.CommandRunner.CommandRan |> Event.add handler
        vimBuffer.VisualBlockMode.CommandRunner.CommandRan |> Event.add handler
        vimBuffer.VisualCharacterMode.CommandRunner.CommandRan |> Event.add handler
        vimBuffer.InsertMode.CommandRan |> Event.add handler
        vimBuffer.ReplaceMode.CommandRan |> Event.add handler

    member x.OnCommandRan (args: CommandRunDataEventArgs) = 
        let data = args.CommandRunData
        let command = data.CommandBinding
        if command.IsMovement || command.IsSpecial then
            // Movement and special commands don't participate in change tracking
            ()
        elif command.IsRepeatable then
            let storedCommand = StoredCommand.OfCommand data.Command data.CommandBinding
            x.StoreCommand storedCommand
        else 
            _vimData.LastCommand <- None

    /// Store the given StoredCommand as the last command executed in the IVimBuffer.  Take into
    /// account linking with the previous command
    member x.StoreCommand currentCommand = 
        match _vimData.LastCommand with
        | None -> 
            // No last command so no linking to consider
            _vimData.LastCommand <- Some currentCommand
        | Some lastCommand ->
            let shouldLink = 
                Util.IsFlagSet currentCommand.CommandFlags CommandFlags.LinkedWithPreviousCommand ||
                Util.IsFlagSet lastCommand.CommandFlags CommandFlags.LinkedWithNextCommand 
            if shouldLink then
                _vimData.LastCommand <- StoredCommand.LinkedCommand (lastCommand, currentCommand) |> Some
            else
                _vimData.LastCommand <- Some currentCommand

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.OnVimBufferCreated buffer

/// Implements the safe dispatching interface which prevents application crashes for 
/// exceptions reaching the dispatcher loop
[<Export(typeof<IProtectedOperations>)>]
type internal ProtectedOperations =

    val _errorHandlers: List<Lazy<IExtensionErrorHandler>>

    [<ImportingConstructor>]
    new ([<ImportMany>] errorHandlers: Lazy<IExtensionErrorHandler> seq) = 
        { _errorHandlers = errorHandlers |> GenericListUtil.OfSeq }

    new (errorHandler: IExtensionErrorHandler) = 
        let l = Lazy<IExtensionErrorHandler>(fun _ -> errorHandler)
        let list = l |> Seq.singleton |> GenericListUtil.OfSeq
        { _errorHandlers = list }

    new () =
        { _errorHandlers = Seq.empty |> GenericListUtil.OfSeq }

    /// Produce a delegate that can safely execute the given action.  If it throws an exception 
    /// then make sure to alert the error handlers
    member private x.GetProtectedAction (action : Action): Action =
        let a () = 
            try
                action.Invoke()
            with
            | e -> x.AlertAll e
        Action(a)

    member private x.GetProtectedEventHandler (eventHandler: EventHandler): EventHandler = 
        let a sender e = 
            try
                eventHandler.Invoke(sender, e)
            with
            | e -> x.AlertAll e
        EventHandler(a)

    /// Alert all of the IExtensionErrorHandlers that the given Exception occurred.  Be careful to guard
    /// against them for Exceptions as we are still on the dispatcher loop here and exceptions would be
    /// fatal
    member x.AlertAll e = 
        VimTrace.TraceError(e)
        for handler in x._errorHandlers do
            try
                handler.Value.HandleError(x, e)
            with 
            | e -> Debug.Fail((sprintf "Error handler threw: %O" e))

    interface IProtectedOperations with
        member x.GetProtectedAction action = x.GetProtectedAction action
        member x.GetProtectedEventHandler eventHandler = x.GetProtectedEventHandler eventHandler
        member x.Report ex = x.AlertAll ex

[<Export(typeof<IVimSpecificServiceHost>)>]
type internal VimSpecificServiceHost
    [<ImportingConstructor>]
    (
        _vimHost: Lazy<IVimHost>,
        [<ImportMany>] _services: Lazy<IVimSpecificService> seq
    ) =

    member x.GetService<'T>(): 'T option =
        let vimHost = _vimHost.Value
        _services 
            |> Seq.map (fun serviceLazy -> 
                try
                    let service = serviceLazy.Value
                    if service.HostIdentifier = vimHost.HostIdentifier then
                        match service :> obj with
                        | :? 'T as t -> Some t
                        | _ -> None
                    else None
                with
                | _ -> None)
            |> SeqUtil.filterToSome
            |> SeqUtil.tryHeadOnly

    interface IVimSpecificServiceHost with
        member x.GetService<'T>() = x.GetService<'T>()

[<Export(typeof<IWordCompletionSessionFactoryService>)>]
type internal VimWordCompletionSessionFactoryService 
    [<ImportingConstructor>]
    (
        _vimSpecificServiceHost: IVimSpecificServiceHost
    ) =

    let _created = StandardEvent<WordCompletionSessionEventArgs>()

    member x.CreateWordCompletionSession textView wordSpan words isForward =
        match _vimSpecificServiceHost.GetService<IWordCompletionSessionFactory>() with
        | None -> None
        | Some factory -> 
            match factory.CreateWordCompletionSession textView wordSpan words isForward with
            | None -> None
            | Some session -> 
                _created.Trigger x (WordCompletionSessionEventArgs(session))
                Some session

    interface IWordCompletionSessionFactoryService with
        member x.CreateWordCompletionSession textView wordSpan words isForward = x.CreateWordCompletionSession textView wordSpan words isForward     
        [<CLIEvent>]
        member x.Created = _created.Publish

type internal SingleSelectionUtil(_textView: ITextView) =

    member x.IsMultiSelectionSupported = false

    member x.GetSelectedSpans () =
        let caretPoint = _textView.Caret.Position.VirtualBufferPosition
        let anchorPoint = _textView.Selection.AnchorPoint
        let activePoint = _textView.Selection.ActivePoint
        seq { yield SelectedSpan(caretPoint, anchorPoint, activePoint) }

    member x.SetSelectedSpans (selectedSpans: SelectedSpan seq) =
        let selectedSpan = Seq.head selectedSpans
        _textView.Caret.MoveTo(selectedSpan.CaretPoint) |> ignore
        if selectedSpan.Length <> 0 then
            _textView.Selection.Select(selectedSpan.AnchorPoint, selectedSpan.ActivePoint)

    interface ISelectionUtil with
        member x.IsMultiSelectionSupported = x.IsMultiSelectionSupported
        member x.GetSelectedSpans() = x.GetSelectedSpans()
        member x.SetSelectedSpans selectedSpans = x.SetSelectedSpans selectedSpans

type internal SingleSelectionUtilFactory() =

    member x.GetSelectionUtil textView =
        SingleSelectionUtil(textView) :> ISelectionUtil

    interface ISelectionUtilFactory with
        member x.GetSelectionUtil textView = x.GetSelectionUtil textView

[<Export(typeof<ISelectionUtilFactoryService>)>]
type internal SelectionUtilService 
    [<ImportingConstructor>]
    (
        _vimSpecificServiceHost: IVimSpecificServiceHost
    ) =

    member x.GetSelectionUtilFactory () =
        match _vimSpecificServiceHost.GetService<ISelectionUtilFactory>() with
        | Some selectionUtilFactory -> selectionUtilFactory
        | None -> SingleSelectionUtilFactory() :> ISelectionUtilFactory

    interface ISelectionUtilFactoryService with
        member x.GetSelectionUtilFactory () = x.GetSelectionUtilFactory()
