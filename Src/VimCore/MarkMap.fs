#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type MarkMap(_bufferTrackingService : IBufferTrackingService) =

    /// This is a map from letter to key.  The key is the key into the property collection
    /// of the ITextBuffer which holds the ITrackingLineColumn for the global mark.  We
    /// don't use just the plain old Letter here because that's used for local marks
    let _letterToKeyMap = 
        Letter.All
        |> Seq.map (fun letter -> letter, obj())
        |> Map.ofSeq

    /// This is the map from Letter to the ITextBuffer where the global mark
    /// is stored.  The MarkMap table lives much longer than the individual marks 
    /// so we hold them in a WeakReference<T> to prevent holding the ITextBuffer 
    /// in memory.
    let mutable _globalMarkMap : Map<Letter, WeakReference<ITextBuffer>> = Map.empty

    /// Get the core information about the global mark represented by the letter
    member x.GetGlobalMarkData letter =
        match Map.tryFind letter _globalMarkMap with
        | None -> None
        | Some weakReference -> 
            match weakReference.Target with
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
            | None -> false
            | Some (_, trackingLineColumn, key) ->
                trackingLineColumn.TextBuffer.Properties.RemoveProperty(key) |> ignore
                trackingLineColumn.Close()
                true
        _globalMarkMap <- Map.remove letter _globalMarkMap
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
    member x.SetGlobalMark letter (vimTextBuffer : IVimTextBuffer) line column =
        // First clear out the existing mark if it exists.  
        x.RemoveGlobalMark letter |> ignore

        let trackingLineColumn = _bufferTrackingService.CreateLineColumn vimTextBuffer.TextBuffer line column LineColumnTrackingMode.Default
        let key = Map.find letter _letterToKeyMap
        vimTextBuffer.TextBuffer.Properties.AddProperty(key, trackingLineColumn)

        let value = WeakReferenceUtil.Create vimTextBuffer.TextBuffer
        _globalMarkMap <- Map.add letter value _globalMarkMap

    /// Get the given mark in the context of the given IVimTextBuffer
    member x.GetMark mark (vimBufferData : IVimBufferData) =
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

    /// Set the given mark to the specified line and column in the context of the IVimTextBuffer
    member x.SetMark mark (vimBufferData : IVimBufferData) line column = 
        let vimTextBuffer = vimBufferData.VimTextBuffer
        match mark with
        | Mark.GlobalMark letter -> 
            x.SetGlobalMark letter vimTextBuffer line column
            true
        | Mark.LocalMark localMark -> 
            vimTextBuffer.SetLocalMark localMark line column
        | Mark.LastJump ->
            vimBufferData.JumpList.SetLastJumpLocation line column
            true

    member x.Clear () = 

        // Close all of the ITrackingLineColumn instances
        Letter.All
        |> Seq.iter (fun letter -> x.RemoveGlobalMark letter |> ignore)

        _globalMarkMap <- Map.empty

    interface IMarkMap with
        member x.GlobalMarks = x.GlobalMarks
        member x.GetGlobalMark letter = x.GetGlobalMark letter
        member x.GetMark mark vimTextBuffer = x.GetMark mark vimTextBuffer
        member x.SetGlobalMark letter vimTextBuffer line column = x.SetGlobalMark letter vimTextBuffer line column
        member x.SetMark mark vimTextBuffer line column = x.SetMark mark vimTextBuffer line column
        member x.RemoveGlobalMark letter = x.RemoveGlobalMark letter
        member x.Clear() = x.Clear()

