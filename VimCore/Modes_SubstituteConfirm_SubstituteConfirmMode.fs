#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Text.RegularExpressions
open Vim.RegexUtil

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
        _buffer : IVimBuffer,
        _operations : ICommonOperations ) as this = 

    let _textBuffer = _buffer.TextBuffer
    let _factory = VimRegexFactory(_buffer.Settings.GlobalSettings)
    let _currentMatchChanged = Event<_>()
    let mutable _commandMap : Map<KeyInput, ConfirmAction> = Map.empty
    let mutable _confirmData : ConfirmData option = None

    do
        let add keyInput func = 
            let set = KeyNotationUtil.StringToKeyInput keyInput
            _commandMap <- Map.add set func _commandMap

        let finished = ModeSwitch.SwitchMode ModeKind.Normal

        add "y" this.DoSubstitute 
        add "l" this.DoSubstitute 
        add "n" this.MoveToNext
        add "<Esc>" (fun _ -> ModeSwitch.SwitchMode ModeKind.Normal)
        add "q" (fun _ -> ModeSwitch.SwitchMode ModeKind.Normal)
        add "a" this.DoSubstituteAll 
        add "<C-e>" (fun _ -> 
            _operations.ScrollPages ScrollDirection.Up 1
            ModeSwitch.NoSwitch)
        add "<C-y>" (fun _ -> 
            _operations.ScrollPages ScrollDirection.Down 1 
            ModeSwitch.NoSwitch)

    member this.CurrentMatch = 
        match _confirmData with
        | Some(data) -> Some data.CurrentMatch
        | None -> None

    member this.ConfirmData 
        with get() = _confirmData
        and set value =
            _confirmData <- value
            _currentMatchChanged.Trigger this.CurrentMatch

    /// Move to the next match given provided ConfirmData 
    member this.MoveToNext (data:ConfirmData) = 
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
                ModeSwitch.SwitchMode ModeKind.Normal
            else 
                let span = SnapshotSpanUtil.CreateFromBounds point line.EndIncludingLineBreak
                let capture = data.Regex.Regex.Match (span.GetText()) 
                if capture.Success then
                    let start = SnapshotPointUtil.Add capture.Index point
                    let span = SnapshotSpanUtil.CreateWithLength start capture.Length
                    this.ConfirmData <- Some {data with CurrentMatch=span}
                    ModeSwitch.NoSwitch
                else
                    doSearch line.EndIncludingLineBreak

        match point with
        | None -> ModeSwitch.SwitchMode ModeKind.Normal
        | Some(point) -> 
            if data.IsReplaceAll then
                doSearch point
            else 
                let line = SnapshotPointUtil.GetContainingLine point
                doSearch line.EndIncludingLineBreak

    /// Substitute the current match and move to the next
    member this.DoSubstitute (data:ConfirmData) = 
        let text = data.Regex.Replace (data.CurrentMatch.GetText()) data.SubstituteText 1 
        _textBuffer.Replace(data.CurrentMatch.Span, text) |> ignore
        this.MoveToNext data

    member this.DoSubstituteAll data = ModeSwitch.NoSwitch

    interface ISubstituteConfirmMode with
        member x.CanProcess ki = true
        member x.CommandNames = _commandMap |> Seq.map (fun pair -> KeyInputSet.OneKeyInput pair.Key)
        member x.CurrentMatch = x.CurrentMatch
        member x.ModeKind = ModeKind.SubstituteConfirm
        member x.VimBuffer = _buffer

        member x.Process ki = 

            // Guard against the case where confirm mode is incorrectly entered
            match _confirmData with
            | None -> ProcessResult.SwitchMode ModeKind.Normal
            | Some(data) -> 

                // It's valid so process the input
                match Map.tryFind ki _commandMap with
                | None -> ProcessResult.Processed
                | Some(func) -> func data |> ProcessResult.OfModeSwitch

        member x.OnClose() = ()
        member x.OnEnter arg =
            x.ConfirmData <- 
                match arg with
                | ModeArgument.None -> None
                | ModeArgument.OneTimeCommand(_) -> None
                | ModeArgument.FromVisual -> None
                | ModeArgument.Subsitute(span, range, data) ->
                    match _factory.CreateForSubstituteFlags data.SearchPattern data.Flags with
                    | None -> None
                    | Some(regex) ->
                        let isReplaceAll = Utils.IsFlagSet data.Flags SubstituteFlags.ReplaceAll
                        let data = { Regex=regex; SubstituteText=data.Substitute; CurrentMatch =span; LastLineNumber=range.EndLineNumber; IsReplaceAll=isReplaceAll}
                        Some data

        member x.OnLeave () = this.ConfirmData <- None

        [<CLIEvent>]
        member x.CurrentMatchChanged = _currentMatchChanged.Publish
