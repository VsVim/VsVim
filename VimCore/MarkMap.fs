#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type GlobalMarkData = {
    VimTextBuffer : IVimTextBuffer
    TrackingLineColumn : ITrackingLineColumn 
}

type MarkMap( _bufferTrackingService : IBufferTrackingService) =

    /// This is the map containing the letters to the global mark positions.  The
    /// MarkMap table lives much longer than the individual marks so we hold them
    /// in a WeakReference<T> to prevent holding the ITextBuffer in memory
    let mutable _globalMarkMap : Map<Letter, WeakReference<GlobalMarkData>> = Map.empty

    member x.GlobalMarksCore =
        _globalMarkMap
        |> Seq.map (fun keyValuePair -> 
            match keyValuePair.Value.Target with
            | None -> None
            | Some trackingLineColumn -> Some (keyValuePair.Key, trackingLineColumn))
        |> SeqUtil.filterToSome

    member x.GlobalMarks =
        x.GlobalMarksCore
        |> Seq.map (fun (letter, globalMarkData) ->
            match globalMarkData.TrackingLineColumn.VirtualPoint with
            | None -> None
            | Some point -> Some (letter, point))
        |> SeqUtil.filterToSome

    /// Get the core information about the global mark represented by the letter
    member x.GetGlobalMarkCore letter =
        match Map.tryFind letter _globalMarkMap with
        | None -> None
        | Some weakReference -> weakReference.Target

    /// Get the global mark location
    member x.GetGlobalMark letter = 
        match x.GetGlobalMarkCore letter with
        | None -> None
        | Some globalMarkData -> globalMarkData.TrackingLineColumn.VirtualPoint

    /// Set the global mark to the value in question
    member x.SetGlobalMark letter (vimTextBuffer : IVimTextBuffer) line column =
        // First clear out the existing mark if it exists.  
        match x.GetGlobalMarkCore letter with 
        | None -> ()
        | Some globalMarkData -> globalMarkData.TrackingLineColumn.Close()

        let trackingLineColumn = _bufferTrackingService.CreateLineColumn vimTextBuffer.TextBuffer line column LineColumnTrackingMode.Default
        let globalMarkData = {
            VimTextBuffer = vimTextBuffer
            TrackingLineColumn = trackingLineColumn } |> WeakReferenceUtil.Create

        _globalMarkMap <- Map.add letter globalMarkData _globalMarkMap

    /// Get the given mark in the context of the given IVimTextBuffer
    member x.GetMark mark (vimBufferData : VimBufferData) =
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
    member x.SetMark mark (vimBufferData : VimBufferData) line column = 
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

    member x.ClearGlobalMarks () = 

        // Close all of the ITrackingLineColumn instances
        x.GlobalMarksCore
        |> Seq.iter (fun (_, globalMarkData) -> globalMarkData.TrackingLineColumn.Close())

        _globalMarkMap <- Map.empty

    interface IMarkMap with
        member x.GlobalMarks = x.GlobalMarks
        member x.GetGlobalMark letter = x.GetGlobalMark letter
        member x.GetMark mark vimTextBuffer = x.GetMark mark vimTextBuffer
        member x.SetGlobalMark letter vimTextBuffer line column = x.SetGlobalMark letter vimTextBuffer line column
        member x.SetMark mark vimTextBuffer line column = x.SetMark mark vimTextBuffer line column
        member x.ClearGlobalMarks() = x.ClearGlobalMarks()

