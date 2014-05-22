
#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimTextBuffer 
    (
        _textBuffer : ITextBuffer,
        _localSettings : IVimLocalSettings,
        _wordNavigator : ITextStructureNavigator,
        _bufferTrackingService : IBufferTrackingService,
        _undoRedoOperations : IUndoRedoOperations,
        _vim : IVim
    ) =

    let _vimHost = _vim.VimHost
    let _globalSettings = _localSettings.GlobalSettings
    let _switchedModeEvent = StandardEvent<SwitchModeKindEventArgs>()
    let mutable _modeKind = ModeKind.Normal
    let mutable _lastVisualSelection : ITrackingVisualSelection option = None
    let mutable _insertStartPoint : ITrackingLineColumn option = None
    let mutable _lastInsertExitPoint : ITrackingLineColumn option = None
    let mutable _lastEditPoint : ITrackingLineColumn option = None
    let mutable _isSoftTabStopValidForBackspace = true

    member x.LastVisualSelection 
        with get() =
            match _lastVisualSelection with
            | None -> None
            | Some trackingVisualSelection -> trackingVisualSelection.VisualSelection
        and set value = 

            // First clear out the previous information
            match _lastVisualSelection with
            | None -> ()
            | Some trackingVisualSelection -> trackingVisualSelection.Close()

            _lastVisualSelection <- 
                match value with
                | None -> None
                | Some visualSelection -> Some (_bufferTrackingService.CreateVisualSelection visualSelection)

    member x.InsertStartPoint
        with get() = 
            match _insertStartPoint with
            | None -> None
            | Some insertStartPoint -> insertStartPoint.Point
        and set value = 

            // First clear out the previous information
            match _insertStartPoint with
            | None -> ()
            | Some insertStartPoint -> insertStartPoint.Close()

            _insertStartPoint <-
                match value with
                | None -> None
                | Some point -> 
                    let line, column = SnapshotPointUtil.GetLineColumn point
                    let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default
                    Some trackingLineColumn

    member x.LastInsertExitPoint
        with get() = 
            match _lastInsertExitPoint with
            | None -> None
            | Some lastInsertExitPoint -> lastInsertExitPoint.Point
        and set value = 

            // First clear out the previous information
            match _lastInsertExitPoint with
            | None -> ()
            | Some lastInsertExitPoint -> lastInsertExitPoint.Close()

            _lastInsertExitPoint <-
                match value with
                | None -> None
                | Some point -> 
                    let line, column = SnapshotPointUtil.GetLineColumn point
                    let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default
                    Some trackingLineColumn

     member x.LastEditPoint
        with get() = 
            match _lastEditPoint with
            | None -> None
            | Some _lastEditPoint -> _lastEditPoint.Point
        and set value = 

            // First clear out the previous information
            match _lastEditPoint with
            | None -> ()
            | Some _lastEditPoint -> _lastEditPoint.Close()

            _lastEditPoint <-
                match value with
                | None -> None
                | Some point -> 
                    let line, column = SnapshotPointUtil.GetLineColumn point
                    let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.LastEditPoint
                    Some trackingLineColumn

    member x.IsSoftTabStopValidForBackspace 
        with get() = _isSoftTabStopValidForBackspace
        and set value = _isSoftTabStopValidForBackspace <- value

    /// Get all of the local marks in the IVimTextBuffer.
    member x.LocalMarks = 
        LocalMark.All
        |> Seq.map (fun localMark ->
            match x.GetLocalMark localMark with
            | None -> None
            | Some point -> Some (localMark, point))
        |> SeqUtil.filterToSome

    /// Clear out all of the cached data.  Essentially we need to dispose all of our marks 
    member x.Clear() =
        // First clear out the Letter based marks
        let properties = _textBuffer.Properties
        Letter.All |> Seq.iter (fun letter ->
            match PropertyCollectionUtil.GetValue<ITrackingLineColumn> letter properties with
            | None -> ()
            | Some trackingLineColumn -> trackingLineColumn.Close())

        // Clear out the other items
        x.LastEditPoint <- None
        x.InsertStartPoint <- None
        x.IsSoftTabStopValidForBackspace <- true
        x.LastInsertExitPoint <- None
        x.LastVisualSelection <- None

    /// Get the specified local mark value
    member x.GetLocalMark localMark =
        match localMark with
        | LocalMark.Letter letter ->
            _textBuffer.Properties
            |> PropertyCollectionUtil.GetValue<ITrackingLineColumn> letter
            |> OptionUtil.map2 (fun trackingLineColumn -> trackingLineColumn.VirtualPoint)
        | LocalMark.Number _ ->
            // TODO: implement numbered mark support
            None
        | LocalMark.LastInsertExit ->
            x.LastInsertExitPoint |> Option.map VirtualSnapshotPointUtil.OfPoint
        | LocalMark.LastEdit ->
            x.LastEditPoint |> Option.map VirtualSnapshotPointUtil.OfPoint
        | LocalMark.LastSelectionStart ->
            x.LastVisualSelection 
            |> Option.map (fun visualSelection -> visualSelection.VisualSpan.Start |> VirtualSnapshotPointUtil.OfPoint) 
        | LocalMark.LastSelectionEnd ->
            x.LastVisualSelection
            |> Option.map (fun visualSelection -> visualSelection.VisualSpan.End |> VirtualSnapshotPointUtil.OfPoint)

    /// Set the local mark at the given line and column
    member x.SetLocalMark localMark line column = 
        match localMark with
        | LocalMark.Letter letter -> 
            // Remove the mark.  This will take core of closing out the tracking data for us so that we don't 
            // leak it
            x.RemoveLocalMark localMark |> ignore

            let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default
            _textBuffer.Properties.[letter] <- trackingLineColumn
            true
        | LocalMark.Number _ -> false
        | LocalMark.LastSelectionEnd -> false
        | LocalMark.LastSelectionStart -> false
        | LocalMark.LastInsertExit -> false
        | LocalMark.LastEdit -> false

    member x.RemoveLocalMark localMark = 
        match localMark with
        | LocalMark.Letter letter -> 
            // Close out the existing mark at this location if it exists
            match PropertyCollectionUtil.GetValue<ITrackingLineColumn> letter _textBuffer.Properties with
            | None -> false
            | Some trackingLineColumn -> 
                trackingLineColumn.Close()
                _textBuffer.Properties.RemoveProperty letter
        | LocalMark.Number _ -> false
        | LocalMark.LastSelectionEnd -> false
        | LocalMark.LastSelectionStart -> false
        | LocalMark.LastInsertExit -> false
        | LocalMark.LastEdit -> false

    /// Switch to the desired mode
    member x.SwitchMode modeKind modeArgument =
        _modeKind <- modeKind

        let args = SwitchModeKindEventArgs(modeKind, modeArgument)
        _switchedModeEvent.Trigger x args

    interface IVimTextBuffer with
        member x.TextBuffer = _textBuffer
        member x.GlobalSettings = _globalSettings
        member x.LastVisualSelection 
            with get() = x.LastVisualSelection
            and set value = x.LastVisualSelection <- value
        member x.InsertStartPoint
            with get() = x.InsertStartPoint
            and set value = x.InsertStartPoint <- value
        member x.IsSoftTabStopValidForBackspace
            with get() = x.IsSoftTabStopValidForBackspace
            and set value = x.IsSoftTabStopValidForBackspace <- value
        member x.LastInsertExitPoint
            with get() = x.LastInsertExitPoint
            and set value = x.LastInsertExitPoint <- value
        member x.LastEditPoint
            with get() = x.LastEditPoint
            and set value = x.LastEditPoint <- value
        member x.LocalMarks = x.LocalMarks
        member x.LocalSettings = _localSettings
        member x.ModeKind = _modeKind
        member x.Name = _vimHost.GetName _textBuffer
        member x.UndoRedoOperations = _undoRedoOperations
        member x.Vim = _vim
        member x.WordNavigator = _wordNavigator
        member x.Clear() = x.Clear()
        member x.GetLocalMark localMark = x.GetLocalMark localMark
        member x.SetLocalMark localMark line column = x.SetLocalMark localMark line column
        member x.RemoveLocalMark localMark = x.RemoveLocalMark localMark
        member x.SwitchMode modeKind modeArgument = x.SwitchMode modeKind modeArgument

        [<CLIEvent>]
        member x.SwitchedMode = _switchedModeEvent.Publish

