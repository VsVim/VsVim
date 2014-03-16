#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Text.RegularExpressions

type ConfirmData = { 

    /// This is the regex which is being matched
    Regex : VimRegex

    /// The replacement text
    SubstituteText : string

    /// This is the current SnapshotSpan which is presented to the user for replacement
    CurrentMatch : SnapshotSpan 

    /// This is the last line number on which the substitute should occur
    LastLineNumber : int

    /// Is this a replace all operation? 
    IsReplaceAll : bool
}

type ConfirmAction = ConfirmData -> ModeSwitch 

type internal SubstituteConfirmMode
    (
        _vimBufferData : IVimBufferData,
        _operations : ICommonOperations
    ) as this = 

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimTextBuffer.TextBuffer
    let _textView = _vimBufferData.TextView
    let _globalSettings = _vimTextBuffer.GlobalSettings
    let _editorOperations = _operations.EditorOperations
    let _currentMatchChanged = Event<_>()
    let mutable _commandMap : Map<KeyInput, ConfirmAction> = Map.empty
    let mutable _confirmData : ConfirmData option = None

    do
        let add keyInput func = 
            let set = KeyNotationUtil.StringToKeyInput keyInput
            _commandMap <- Map.add set func _commandMap

        let finished = ModeSwitch.SwitchMode ModeKind.Normal

        add "y" this.DoSubstitute 
        add "l" this.DoSubstituteLast
        add "n" this.MoveToNext
        add "<Esc>" (fun _ -> ModeSwitch.SwitchMode ModeKind.Normal)
        add "q" (fun _ -> ModeSwitch.SwitchMode ModeKind.Normal)
        add "a" this.DoSubstituteAll 
        add "<C-e>" (fun _ -> 
            _editorOperations.PageUp(false)
            ModeSwitch.NoSwitch)
        add "<C-y>" (fun _ -> 
            _editorOperations.PageDown(false)
            ModeSwitch.NoSwitch)

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.CanProcess (keyInput : KeyInput) = not keyInput.IsMouseKey

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.CurrentMatch = 
        match _confirmData with
        | Some data -> Some data.CurrentMatch
        | None -> None

    member x.ConfirmData 
        with get() = _confirmData
        and set value =
            _confirmData <- value
            _currentMatchChanged.Trigger x.CurrentMatch

            match value with 
            | None -> 
                ()
            | Some data -> 

                // Adjust the caret to the new location and ensure it's visible to the user.  We need
                // to dig into collapsed regions here as well
                _operations.MoveCaretToPoint data.CurrentMatch.Start ViewFlags.Standard

    member x.CurrentSubstitute =
        match _confirmData with
        | None -> None
        | Some data -> 
            let replaceData = _operations.GetReplaceData x.CaretPoint
            data.Regex.Replace (data.CurrentMatch.GetText()) (data.SubstituteText) replaceData |> Some

    member x.EndOperation () = 
        x.ConfirmData <- None
        ModeSwitch.SwitchMode ModeKind.Normal

    /// Move to the next match given provided ConfirmData 
    member x.MoveToNext (data : ConfirmData) = 

        // First we need to get the point after the Current selection.  This function
        // is called after edits so it's possible the Snapshot is different.
        let point = 
            if data.CurrentMatch.Snapshot = _textBuffer.CurrentSnapshot then 
                Some data.CurrentMatch.End
            else 
                let point = data.CurrentMatch.Snapshot.CreateTrackingPoint((data.CurrentMatch.End.Position), PointTrackingMode.Positive)
                TrackingPointUtil.GetPoint _textBuffer.CurrentSnapshot point

        // Do the search from the given point in a line wise fashion
        let rec doSearch point = 
            let line = SnapshotPointUtil.GetContainingLine point
            if line.LineNumber > data.LastLineNumber || SnapshotPointUtil.IsEndPoint point then
                x.EndOperation()
            else 
                let span = SnapshotSpan(point, line.EndIncludingLineBreak)
                match RegexUtil.MatchSpan span data.Regex.Regex with
                | Some (span, _) ->
                    x.ConfirmData <- Some { data with CurrentMatch=span }
                    ModeSwitch.NoSwitch
                | None -> 
                    doSearch line.EndIncludingLineBreak

        match point with
        | None -> 
            x.EndOperation()
        | Some point -> 
            if data.IsReplaceAll then
                doSearch point
            else 
                let line = SnapshotPointUtil.GetContainingLine point
                doSearch line.EndIncludingLineBreak

    member x.ReplaceCurrent (data:ConfirmData) =
        let replaceData = _operations.GetReplaceData x.CaretPoint
        let text = data.Regex.Replace (data.CurrentMatch.GetText()) data.SubstituteText replaceData
        _textBuffer.Replace(data.CurrentMatch.Span, text) |> ignore

    /// Substitute the current match and move to the next
    member x.DoSubstitute (data:ConfirmData) = 
        x.ReplaceCurrent data
        x.MoveToNext data

    /// Substitute the current match and end the operation
    member x.DoSubstituteLast (data:ConfirmData) = 
        x.ReplaceCurrent data
        x.EndOperation()

    /// Substitute all remaining matches and exit the confirm operation
    member x.DoSubstituteAll data = 

        let lineSpans = 
            let line = SnapshotPointUtil.GetContainingLine data.CurrentMatch.Start
            let rest = 
                if line.LineNumber = data.LastLineNumber || line.LineNumber = line.Snapshot.LineCount - 1 then 
                    Seq.empty
                else 
                    let range = SnapshotLineRangeUtil.CreateForLineNumberRange line.Snapshot (line.LineNumber + 1) data.LastLineNumber
                    range.Lines |> Seq.map SnapshotLineUtil.GetExtentIncludingLineBreak
            let first = SnapshotSpan(data.CurrentMatch.Start, line.EndIncludingLineBreak)
            Seq.append (Seq.singleton first) rest

        let replaceData = _operations.GetReplaceData x.CaretPoint
        let doReplace = 
            if data.IsReplaceAll then data.Regex.ReplaceAll
            else data.Regex.Replace

        let edit = _textBuffer.CreateEdit()
        lineSpans 
        |> Seq.iter (fun span ->
            let text = doReplace (span.GetText()) data.SubstituteText replaceData
            edit.Replace(span.Span, text) |> ignore)
        if edit.HasEffectiveChanges then edit.Apply() |> ignore else edit.Cancel()

        x.EndOperation()

    interface ISubstituteConfirmMode with
        member x.CanProcess keyInput = x.CanProcess keyInput
        member x.CommandNames = _commandMap |> Seq.map (fun pair -> KeyInputSet.OneKeyInput pair.Key)
        member x.CurrentMatch = x.CurrentMatch
        member x.CurrentSubstitute = x.CurrentSubstitute
        member x.ModeKind = ModeKind.SubstituteConfirm
        member x.VimTextBuffer = _vimTextBuffer

        member x.Process ki = 

            // Guard against the case where confirm mode is incorrectly entered
            match _confirmData with
            | None -> 
                ProcessResult.OfModeKind ModeKind.Normal
            | Some data -> 

                // It's valid so process the input
                match Map.tryFind ki _commandMap with
                | None -> ProcessResult.Handled ModeSwitch.NoSwitch
                | Some func -> func data |> ProcessResult.Handled

        member x.OnClose() = ()
        member x.OnEnter arg =
            x.ConfirmData <- 
                match arg with
                | ModeArgument.None -> None
                | ModeArgument.FromVisual -> None
                | ModeArgument.InitialVisualSelection _ -> None
                | ModeArgument.InsertBlock (_, transaction) -> transaction.Complete(); None
                | ModeArgument.InsertWithCount _ -> None
                | ModeArgument.InsertWithCountAndNewLine _ -> None
                | ModeArgument.InsertWithTransaction transaction -> transaction.Complete(); None
                | ModeArgument.Substitute(span, range, data) ->
                    match VimRegexFactory.CreateForSubstituteFlags data.SearchPattern _globalSettings data.Flags with
                    | None -> None
                    | Some regex ->
                        let isReplaceAll = Util.IsFlagSet data.Flags SubstituteFlags.ReplaceAll
                        let data = { Regex=regex; SubstituteText=data.Substitute; CurrentMatch = span; LastLineNumber = range.LastLineNumber; IsReplaceAll=isReplaceAll}
                        Some data

        member x.OnLeave () = 
            x.ConfirmData <- None

        [<CLIEvent>]
        member x.CurrentMatchChanged = _currentMatchChanged.Publish
