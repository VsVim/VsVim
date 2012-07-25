﻿
#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

// TODO: Need to add a Close method and do actions like close the ITrackingVisualSpan.  Or
// add tests to verify that it goes away after close
type internal VimTextBuffer 
    (
        _textBuffer : ITextBuffer,
        _localSettings : IVimLocalSettings,
        _wordNavigator : ITextStructureNavigator,
        _bufferTrackingService : IBufferTrackingService,
        _vim : IVim
    ) =

    let _vimHost = _vim.VimHost
    let _globalSettings = _localSettings.GlobalSettings
    let _switchedModeEvent = StandardEvent<SwitchModeKindEventArgs>()
    let mutable _modeKind = ModeKind.Normal
    let mutable _lastVisualSelection : ITrackingVisualSelection option = None
    let mutable _lastInsertExitPoint : ITrackingLineColumn option = None
    let mutable _lastEditPoint : ITrackingLineColumn option = None

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
                    let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default
                    Some trackingLineColumn

    /// Get all of the local marks in the IVimTextBuffer.
    member x.LocalMarks = 
        LocalMark.All
        |> Seq.map (fun localMark ->
            match x.GetLocalMark localMark with
            | None -> None
            | Some point -> Some (localMark, point))
        |> SeqUtil.filterToSome

    /// Get the specified local mark value
    member x.GetLocalMark localMark =

        match localMark with
        | LocalMark.Letter letter ->
            _textBuffer.Properties
            |> PropertyCollectionUtil.GetValue<ITrackingLineColumn> letter
            |> OptionUtil.map2 (fun trackingLineColumn -> trackingLineColumn.VirtualPoint)
        | LocalMark.LastInsertExit ->
            x.LastInsertExitPoint |> Option.map VirtualSnapshotPointUtil.OfPoint
        | LocalMark.LastEdit ->
            x.LastEditPoint |> Option.map VirtualSnapshotPointUtil.OfPoint
        | LocalMark.LastSelectionStart ->
            x.LastVisualSelection 
            |> Option.map (fun visualSelection -> 
                visualSelection.VisualSpan.Start |> VirtualSnapshotPointUtil.OfPoint) 
        | LocalMark.LastSelectionEnd ->
            x.LastVisualSelection
            |> Option.map (fun visualSelection -> 
                visualSelection.VisualSpan.End |> VirtualSnapshotPointUtil.OfPoint)

    /// Set the local mark at the given line and column
    member x.SetLocalMark localMark line column = 
        match localMark with
        | LocalMark.Letter letter -> 
            // First close out the existing mark at this location if it exists
            match PropertyCollectionUtil.GetValue<ITrackingLineColumn> letter _textBuffer.Properties with
            | None -> ()
            | Some trackingLineColumn -> trackingLineColumn.Close()

            let trackingLineColumn = _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.Default
            _textBuffer.Properties.[letter] <- trackingLineColumn
            true
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
        member x.Vim = _vim
        member x.WordNavigator = _wordNavigator
        member x.GetLocalMark localMark = x.GetLocalMark localMark
        member x.SetLocalMark localMark line column = x.SetLocalMark localMark line column
        member x.SwitchMode modeKind modeArgument = x.SwitchMode modeKind modeArgument

        [<CLIEvent>]
        member x.SwitchedMode = _switchedModeEvent.Publish

