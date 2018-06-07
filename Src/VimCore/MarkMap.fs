#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type MarkMap(_bufferTrackingService: IBufferTrackingService) =

    /// This is a map from letter to key.  The key is the key into the property collection
    /// of the ITextBuffer which holds the ITrackingLineColumn for the global mark.  We
    /// don't use just the plain old Letter here because that's used for local marks
    let _letterToKeyMap = 
        Letter.All
        |> Seq.map (fun letter -> letter, obj())
        |> Map.ofSeq

    let _markSetEvent = StandardEvent<MarkChangedEventArgs>()
    let _markDeletedEvent = StandardEvent<MarkChangedEventArgs>()

    /// This is the map from Letter to the ITextBuffer where the global mark
    /// is stored.
    let mutable _globalMarkMap: Map<Letter, ITextBuffer> = Map.empty

    /// This is a map from a Letter to a buffer name (file path),
    /// row and column. It is used for unloaded buffers.
    let mutable _globalUnloadedMarkMap: Map<Letter, string * int * int> = Map.empty

    let mutable _globalLastExitedMap: Map<string, int * int> = Map.empty

    /// Get the core information about the global mark represented by the letter
    member x.GetGlobalMarkData letter =
        match Map.tryFind letter _globalMarkMap with
        | None -> None
        | Some textBuffer -> 
            let key = Map.find letter _letterToKeyMap
            match PropertyCollectionUtil.GetValue<ITrackingLineColumn> key textBuffer.Properties with
            | None -> None
            | Some trackingLineColumn -> Some (letter, trackingLineColumn, key)

    /// Delete a global mark if it exists
    member x.RemoveGlobalMark letter = 
        let result = 
            match x.GetGlobalMarkData letter with
            | Some (_, trackingLineColumn, key) ->
                trackingLineColumn.TextBuffer.Properties.RemoveProperty(key) |> ignore
                trackingLineColumn.Close()
                true
            | None ->
                match _globalUnloadedMarkMap.TryFind letter with
                | Some _ -> true
                | None -> false
        _globalMarkMap <- Map.remove letter _globalMarkMap
        _globalUnloadedMarkMap <- _globalUnloadedMarkMap.Remove letter
        result

    /// Get all of the global mark letters and their associated VirtualSnapshotPoint
    member x.GlobalMarks =
        _globalMarkMap
        |> Seq.map (fun keyValuePair -> keyValuePair.Key)
        |> Seq.map (fun letter ->
            match x.GetGlobalMarkData letter with
            | None -> None
            | Some (_, trackingLineColumn, _) -> 
                match trackingLineColumn.VirtualPoint with
                | None -> None
                | Some virtualPoint -> Some (letter, virtualPoint))
        |> SeqUtil.filterToSome

    /// Get the global mark location
    member x.GetGlobalMark letter = 
        match x.GetGlobalMarkData letter with
        | None -> None
        | Some (_, trackingLineColumn, _) -> trackingLineColumn.VirtualPoint

    /// Set the global mark to the value in question
    member x.SetGlobalMark letter (vimTextBuffer: IVimTextBuffer) line column =
        // First clear out the existing mark if it exists.  
        x.RemoveGlobalMark letter |> ignore

        let trackingLineColumn = _bufferTrackingService.CreateLineColumn vimTextBuffer.TextBuffer line column LineColumnTrackingMode.Default
        let key = Map.find letter _letterToKeyMap
        vimTextBuffer.TextBuffer.Properties.AddProperty(key, trackingLineColumn)

        let textBuffer = vimTextBuffer.TextBuffer
        _globalMarkMap <- Map.add letter textBuffer _globalMarkMap

    /// Get the given mark in the context of the given IVimTextBuffer
    member x.GetMark mark (vimBufferData: IVimBufferData) =
        match mark with
        | Mark.GlobalMark letter -> 

            // A global mark can exist in any ITextBuffer, make sure we only return the global mark if it
            // exists in this ITextBuffer
            x.GetGlobalMark letter |> OptionUtil.map2 (fun point -> 
                if point.Position.Snapshot.TextBuffer = vimBufferData.TextBuffer then
                    Some point
                else 
                    None)
        | Mark.LocalMark localMark -> 
            vimBufferData.VimTextBuffer.GetLocalMark localMark
        | Mark.LastJump -> 
            vimBufferData.JumpList.LastJumpLocation
        | Mark.LastExitedPosition ->
            let getZeroZero() =
                let snapshot = SnapshotUtil.GetStartPoint vimBufferData.TextBuffer.CurrentSnapshot
                Some (VirtualSnapshotPointUtil.OfPoint snapshot)

            let line, column =
                match _globalLastExitedMap.TryFind (vimBufferData.Vim.VimHost.GetName vimBufferData.TextBuffer) with
                | Some (line, column) -> line, column
                | None -> 0, 0

            // not using TryGetPointInLine because None is returned when column and lineLength are both 0,
            //  which can be valid in this case
            match SnapshotUtil.TryGetLine vimBufferData.TextBuffer.CurrentSnapshot line with
            | None -> getZeroZero()
            | Some snapshotLine ->
                if column > snapshotLine.Length && not (column = 0 && snapshotLine.Length = 0) then
                    getZeroZero()
                else
                    let snapshot = snapshotLine.Start.Add(column)
                    Some (VirtualSnapshotPointUtil.OfPoint snapshot)

    /// Get the buffer name, line and column associated with a mark
    member x.GetMarkInfo (mark: Mark) (vimBufferData: IVimBufferData) =

        let getPointInfo (point: VirtualSnapshotPoint) =
            let textLine = point.Position.GetContainingLine()
            let line = textLine.LineNumber
            let column = point.Position.Position - textLine.Start.Position
            let column = if point.IsInVirtualSpace then column + point.VirtualSpaces else column
            let name = vimBufferData.Vim.VimHost.GetName point.Position.Snapshot.TextBuffer
            MarkInfo(mark.Char, name, line, column) |> Some

        match mark with
        | Mark.GlobalMark letter ->
            match x.GetGlobalMark letter with
            | Some point -> getPointInfo point
            | None ->
                match _globalUnloadedMarkMap.TryFind letter with
                | Some (name, line, column) -> MarkInfo(mark.Char, name, line, column) |> Some
                | None -> None
        |_ ->
            match x.GetMark mark vimBufferData with
            | Some point -> getPointInfo point
            | None -> None

    /// Set the given mark to the specified line and column in the context of the specified IVimBufferData
    member x.SetMark mark (vimBufferData: IVimBufferData) line column = 
        let vimTextBuffer = vimBufferData.VimTextBuffer
        let result =
            match mark with
            | Mark.GlobalMark letter -> 
                x.SetGlobalMark letter vimTextBuffer line column
                true
            | Mark.LocalMark localMark -> 
                vimTextBuffer.SetLocalMark localMark line column
            | Mark.LastJump ->
                vimBufferData.JumpList.SetLastJumpLocation line column
                true
            | Mark.LastExitedPosition ->
                let bufferName = vimBufferData.VimTextBuffer.Name
                if not (System.String.IsNullOrEmpty bufferName) then
                    _globalLastExitedMap <-
                        _globalLastExitedMap.Remove bufferName
                        |> Map.add bufferName (line, column)
                    true
                else
                    false
        if result then
            let args = MarkChangedEventArgs(mark, vimBufferData)
            _markSetEvent.Trigger x args
        result

    /// Delete the given mark in the context of the specified IVimBufferData
    member x.DeleteMark mark (vimBufferData: IVimBufferData) =
        let vimTextBuffer = vimBufferData.VimTextBuffer
        let result =
            match mark with 
            | Mark.LocalMark localMark -> vimTextBuffer.RemoveLocalMark localMark
            | Mark.GlobalMark letter -> x.RemoveGlobalMark letter
            | Mark.LastJump -> false
            | Mark.LastExitedPosition -> false
        if result then
            let args = MarkChangedEventArgs(mark, vimBufferData)
            _markDeletedEvent.Trigger x args
        result

    /// Unload the buffer recording the last exited position
    member x.UnloadBuffer (vimBufferData: IVimBufferData) bufferName line column =
        let textBuffer = vimBufferData.TextBuffer

        let unloadGlobalMark (letter: Letter) =
            let mark = Mark.GlobalMark letter
            match x.GetGlobalMarkData letter with
            | None -> ()
            | Some (_, trackingLineColumn, key) ->
                if not (System.String.IsNullOrEmpty bufferName) then
                    match trackingLineColumn.Point with
                    | None -> ()
                    | Some point ->

                    // Add an unloaded mark corresponding to the current position.
                    let line, column = SnapshotPointUtil.GetLineColumn point
                    _globalUnloadedMarkMap <-
                        _globalUnloadedMarkMap.Remove letter
                        |> Map.add letter (bufferName, line, column)

                // Close the tracking item.
                trackingLineColumn.TextBuffer.Properties.RemoveProperty(key) |> ignore
                trackingLineColumn.Close()

            // Remove the mark from the global mark map.
            _globalMarkMap <- Map.remove letter _globalMarkMap

        // Unload all the global marks associated with the text buffer.
        x.GlobalMarks
        |> Seq.filter (fun (_, point) -> point.Position.Snapshot.TextBuffer = textBuffer)
        |> Seq.iter (fun (letter, point) -> unloadGlobalMark letter)

        // Record the last exited position.
        if not (System.String.IsNullOrEmpty bufferName) then
            _globalLastExitedMap <-
                _globalLastExitedMap.Remove bufferName
                |> Map.add bufferName (line, column)
            true
        else
            false

    /// Reload the marks associated with a buffer
    member x.ReloadBuffer (vimBufferData: IVimBufferData) bufferName =

        let reloadGlobalMark letter line column =
            _globalUnloadedMarkMap.Remove letter |> ignore
            x.SetGlobalMark letter vimBufferData.VimTextBuffer line column

        let unloadedMarks =
            _globalUnloadedMarkMap
            |> Seq.map (fun pair -> pair.Key, pair.Value)
            |> Seq.filter (fun (letter, (name, _, _)) -> name = bufferName)
            |> Seq.map (fun (letter, (_, line, column)) -> (letter, line, column))
            |> Seq.toList

        let result = unloadedMarks.Length <> 0

        unloadedMarks
        |> Seq.iter (fun (letter, line, column) -> reloadGlobalMark letter line column)

        result

    member x.Clear () = 

        // Close all of the ITrackingLineColumn instances
        Letter.All
        |> Seq.iter (fun letter -> x.RemoveGlobalMark letter |> ignore)

        _globalMarkMap <- Map.empty
        _globalUnloadedMarkMap <- Map.empty

    interface IMarkMap with
        member x.GlobalMarks = x.GlobalMarks
        member x.GetGlobalMark letter = x.GetGlobalMark letter
        member x.GetMark mark vimBufferData = x.GetMark mark vimBufferData
        member x.GetMarkInfo mark vimBufferData = x.GetMarkInfo mark vimBufferData
        member x.SetGlobalMark letter vimTextBuffer line column = x.SetGlobalMark letter vimTextBuffer line column
        member x.SetMark mark vimBufferData line column = x.SetMark mark vimBufferData line column
        member x.DeleteMark mark vimBufferData = x.DeleteMark mark vimBufferData
        member x.UnloadBuffer vimBufferData name line column = x.UnloadBuffer vimBufferData name line column
        member x.ReloadBuffer vimBufferData name = x.ReloadBuffer vimBufferData name
        member x.RemoveGlobalMark letter = x.RemoveGlobalMark letter
        member x.Clear() = x.Clear()

        [<CLIEvent>]
        member x.MarkSet = _markSetEvent.Publish

        [<CLIEvent>]
        member x.MarkDeleted = _markDeletedEvent.Publish
