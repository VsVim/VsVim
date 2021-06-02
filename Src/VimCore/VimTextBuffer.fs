#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal VimTextBuffer 
    (
        textBuffer: ITextBuffer,
        localAbbreviationMap: IVimLocalAbbreviationMap,
        localKeyMap: IVimLocalKeyMap,
        localSettings: IVimLocalSettings,
        bufferTrackingService: IBufferTrackingService,
        undoRedoOperations: IUndoRedoOperations,
        wordUtil: WordUtil,
        vim: IVim
    ) =

    let _textBuffer = textBuffer
    let _localAbbreviationMap = localAbbreviationMap
    let _localKeyMap = localKeyMap
    let _localSettings = localSettings
    let _bufferTrackingService = bufferTrackingService
    let _undoRedoOperations = undoRedoOperations
    let _wordUtil = wordUtil
    let _vim = vim
    let _vimHost = _vim.VimHost
    let _globalSettings = _localSettings.GlobalSettings
    let _modeLineInterpreter = ModeLineInterpreter(_textBuffer, _localSettings)
    let _switchedModeEvent = StandardEvent<SwitchModeKindEventArgs>()
    let _markSetEvent = StandardEvent<MarkTextBufferEventArgs>()
    let _wordNavigator = _wordUtil.WordNavigator

    let mutable _modeKind = ModeKind.Normal
    let mutable _lastVisualSelection: ITrackingVisualSelection option = None
    let mutable _insertStartPoint: ITrackingLineColumn option = None
    let mutable _lastInsertExitPoint: ITrackingLineColumn option = None
    let mutable _lastEditPoint: ITrackingLineColumn option = None
    let mutable _lastChangeOrYankStart: ITrackingLineColumn option = None
    let mutable _lastChangeOrYankEnd: ITrackingLineColumn option = None
    let mutable _isSoftTabStopValidForBackspace = true
    let mutable _inOneTimeCommand: ModeKind option = None
    let mutable _inSelectModeOneCommand: bool = false

    /// Raise the mark set event
    member x.RaiseMarkSet localMark =
        let mark = Mark.LocalMark localMark
        let args = MarkTextBufferEventArgs(mark, _textBuffer)
        _markSetEvent.Trigger x args

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

            x.RaiseMarkSet LocalMark.LastSelectionStart
            x.RaiseMarkSet LocalMark.LastSelectionEnd

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
                    let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset point
                    let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default
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
                    let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset point
                    let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default
                    Some trackingLineColumn

            x.RaiseMarkSet LocalMark.LastInsertExit

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
                    let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset point
                    let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.LastEditPoint
                    Some trackingLineColumn

            x.RaiseMarkSet LocalMark.LastEdit

     member x.LastChangeOrYankStart
        with get() = 
            match _lastChangeOrYankStart with
            | None -> None
            | Some trackingLineColumn -> trackingLineColumn.Point
        and set value = 

            // First clear out the previous information
            match _lastChangeOrYankStart with
            | None -> ()
            | Some trackingLineColumn -> trackingLineColumn.Close()

            _lastChangeOrYankStart <-
                match value with
                | None -> None
                | Some point -> 
                    let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset point
                    let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default
                    Some trackingLineColumn

            x.RaiseMarkSet LocalMark.LastChangeOrYankStart

     member x.LastChangeOrYankEnd
        with get() = 
            match _lastChangeOrYankEnd with
            | None -> None
            | Some trackingLineColumn -> trackingLineColumn.Point
        and set value = 

            // First clear out the previous information
            match _lastChangeOrYankEnd with
            | None -> ()
            | Some trackingLineColumn -> trackingLineColumn.Close()

            _lastChangeOrYankEnd <-
                match value with
                | None -> None
                | Some point -> 
                    let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset point
                    let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default
                    Some trackingLineColumn

            x.RaiseMarkSet LocalMark.LastChangeOrYankEnd

     member x.InOneTimeCommand
        with get() = _inOneTimeCommand
        and set value = _inOneTimeCommand <- value

    member x.InSelectModeOneTimeCommand
        with get() = _inSelectModeOneCommand
        and set value = _inSelectModeOneCommand <- value

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

    /// Whether to use virtual space
    member x.UseVirtualSpace =
        match _modeKind with
        | ModeKind.SelectBlock
        | ModeKind.VisualBlock
            -> _globalSettings.IsVirtualEditBlock
        | ModeKind.Insert
        | ModeKind.Replace
            -> _globalSettings.IsVirtualEditInsert
        | _
            -> _globalSettings.IsVirtualEditAll

    /// Check the contents of the buffer for a modeline, returning a tuple of
    /// the line we used as a modeline, if any, and a string representing the
    /// first sub-option that produced an error if any
    member x.CheckModeLine windowSettings =
        _modeLineInterpreter.CheckModeLine windowSettings

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
        x.LastChangeOrYankStart <- None
        x.LastChangeOrYankEnd <- None

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
        | LocalMark.LastChangeOrYankStart ->
            x.LastChangeOrYankStart |> Option.map VirtualSnapshotPointUtil.OfPoint
        | LocalMark.LastChangeOrYankEnd ->
            x.LastChangeOrYankEnd |> Option.map VirtualSnapshotPointUtil.OfPoint

    /// Set the local mark at the given line number and offset
    member x.SetLocalMark localMark lineNumber offset = 
        match localMark with
        | LocalMark.Letter letter -> 
            // Remove the mark.  This will take core of closing out the tracking data for us so that we don't 
            // leak it
            x.RemoveLocalMark localMark |> ignore

            let trackingLineColumn = _bufferTrackingService.CreateLineOffset _textBuffer lineNumber offset LineColumnTrackingMode.Default
            _textBuffer.Properties.[letter] <- trackingLineColumn
            true
        | LocalMark.Number _ -> false
        | LocalMark.LastSelectionEnd -> false
        | LocalMark.LastSelectionStart -> false
        | LocalMark.LastInsertExit -> false
        | LocalMark.LastEdit -> false
        | LocalMark.LastChangeOrYankStart -> false
        | LocalMark.LastChangeOrYankEnd -> false

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
        | LocalMark.LastChangeOrYankStart -> false
        | LocalMark.LastChangeOrYankEnd -> false

    /// Switch to the desired mode
    member x.SwitchMode modeKind modeArgument =
        _modeKind <- modeKind

        let args = SwitchModeKindEventArgs(modeKind, modeArgument)
        _switchedModeEvent.Trigger x args

    interface IVimTextBuffer with
        member x.TextBuffer = _textBuffer
        member x.GlobalSettings = _globalSettings
        member x.GlobalAbbreviationMap = _localAbbreviationMap.GlobalAbbreviationMap
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
        member x.LastChangeOrYankStart
            with get() = x.LastChangeOrYankStart
            and set value = x.LastChangeOrYankStart <- value
        member x.LastChangeOrYankEnd
            with get() = x.LastChangeOrYankEnd
            and set value = x.LastChangeOrYankEnd <- value
        member x.InOneTimeCommand
            with get() = x.InOneTimeCommand
            and set value = x.InOneTimeCommand <- value
        member x.InSelectModeOneTimeCommand
            with get() = x.InSelectModeOneTimeCommand
            and set value = x.InSelectModeOneTimeCommand <- value
        member x.LocalMarks = x.LocalMarks
        member x.LocalAbbreviationMap = _localAbbreviationMap
        member x.LocalKeyMap = _localKeyMap
        member x.LocalSettings = _localSettings
        member x.ModeKind = _modeKind
        member x.Name = _vimHost.GetName _textBuffer
        member x.UndoRedoOperations = _undoRedoOperations
        member x.Vim = _vim
        member x.WordUtil = _wordUtil
        member x.WordNavigator = _wordNavigator
        member x.UseVirtualSpace = x.UseVirtualSpace
        member x.CheckModeLine windowSettings = x.CheckModeLine windowSettings
        member x.Clear() = x.Clear()
        member x.GetLocalMark localMark = x.GetLocalMark localMark
        member x.SetLocalMark localMark lineNumber offset = x.SetLocalMark localMark lineNumber offset
        member x.RemoveLocalMark localMark = x.RemoveLocalMark localMark
        member x.SwitchMode modeKind modeArgument = x.SwitchMode modeKind modeArgument

        [<CLIEvent>]
        member x.SwitchedMode = _switchedModeEvent.Publish

        [<CLIEvent>]
        member x.MarkSet = _markSetEvent.Publish
