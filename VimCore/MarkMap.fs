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
    member x.GetMark mark (vimTextBuffer : IVimTextBuffer) = 
        match mark with
        | Mark.GlobalMark letter -> x.GetGlobalMark letter
        | Mark.LocalMark localMark -> vimTextBuffer.GetLocalMark localMark

    /// Set the given mark to the specified line and column in the context of the IVimTextBuffer
    member x.SetMark mark vimTextBuffer line column = 
        match mark with
        | Mark.GlobalMark letter -> x.SetGlobalMark letter vimTextBuffer line column
        | Mark.LocalMark localMark -> vimTextBuffer.SetLocalMark localMark line column

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

