#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalModeData = {
    Command : string
    IsInRepeatLastChange : bool
    IsInReplace : bool
    OneTimeMode : ModeKind option 
} 

type internal NormalMode 
    ( 
        _bufferData : IVimBuffer, 
        _operations : IOperations,
        _statusUtil : IStatusUtil,
        _displayWindowBroker : IDisplayWindowBroker,
        _runner : ICommandRunner,
        _capture : IMotionCapture,
        _visualSpanCalculator : IVisualSpanCalculator ) as this =

    let _textView = _bufferData.TextView
    let _settings = _bufferData.Settings

    /// Reset state for data in Normal Mode
    let _emptyData = {
        Command = StringUtil.empty
        IsInRepeatLastChange = false
        IsInReplace = false
        OneTimeMode = None
    }

    /// Set of all char's Vim is interested in 
    let _coreCharSet = KeyInputUtil.VimKeyCharList |> Set.ofList

    /// Contains the state information for Normal mode
    let mutable _data = _emptyData

    let _eventHandlers = DisposableBag()

    do
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        let settings = _bufferData.Settings.GlobalSettings :> IVimSettings
        settings.SettingChanged.Subscribe this.OnGlobalSettingsChanged |> _eventHandlers.Add

    member this.TextView = _bufferData.TextView
    member this.TextBuffer = _bufferData.TextBuffer
    member this.CaretPoint = _bufferData.TextView.Caret.Position.BufferPosition
    member this.Settings = _bufferData.Settings
    member this.IsCommandRunnerPopulated = _runner.Commands |> SeqUtil.isNotEmpty
    member this.KeyRemapMode = 
        match _runner.KeyRemapMode with
        | Some(remapMode) -> remapMode
        | None -> KeyRemapMode.Normal
    member this.Command = _data.Command
    member this.Commands = 
        this.EnsureCommands()
        _runner.Commands

    member private this.EnsureCommands() = 
        if not this.IsCommandRunnerPopulated then
            let factory = Vim.Modes.CommandFactory(_operations, _capture, _bufferData.TextViewMotionUtil, _bufferData.JumpList, _bufferData.Settings)

            this.CreateSimpleCommands()
            |> Seq.append (factory.CreateMovementCommands())
            |> Seq.append (factory.CreateEditCommandsForNormalMode())
            |> Seq.append (this.CreateMotionCommands())
            |> Seq.append (this.CreateLongCommands())
            |> Seq.iter _runner.Add

            // Add in the special ~ command
            let _,command = this.GetTildeCommand()
            _runner.Add command

    /// Raised when a global setting is changed
    member private this.OnGlobalSettingsChanged (setting:Setting) = 
        
        // If the tildeop setting changes we need to update how we handle it
        if StringUtil.isEqual setting.Name GlobalSettingNames.TildeOpName && this.IsCommandRunnerPopulated then
            let name,command = this.GetTildeCommand()
            _runner.Remove name
            _runner.Add command

    member private x.ReplaceChar count reg = 
        let inner (ki:KeyInput) = 
            _data <- { _data with IsInReplace = false }
            if ki = KeyInputUtil.EscapeKey then LongCommandResult.Cancelled
            else 
                if not (_operations.ReplaceChar ki count) then
                    _operations.Beep()
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        _data <- { _data with IsInReplace = true }
        LongCommandResult.NeedMoreInput (Some KeyRemapMode.Language, inner)

    member x.WaitJumpToMark (count:int) (reg:Register) =
        let waitForKey (ki:KeyInput)  =
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap 
            match res with 
            | Modes.Failed(msg) -> _statusUtil.OnError msg
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput (Some KeyRemapMode.Language, waitForKey)

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.WaitMark (count:int) (reg:Register)= 
        let waitForKey (ki:KeyInput) =
            let cursor = TextViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _operations.Beep()
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput (Some KeyRemapMode.Language, waitForKey)

    /// Implements the '.' operator.  This is a special command in that it cannot be easily routed 
    /// to interfaces like ICommonOperations due to the complexity of repeating the command here.  
    member private this.RepeatLastChange countOpt reg =  

        if _data.IsInRepeatLastChange then _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
        else
            _data <- { _data with IsInRepeatLastChange = true }
            try

                let rec repeatChange change countOpt =

                    /// Repeat a text buffer edit.  
                    let repeatTextBufferChange change = 
                        match change with 
                        | TextChange.Insert(text) -> _operations.InsertText text (CommandUtil.CountOrDefault countOpt)
                        | TextChange.Delete(count) -> 
                            let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine this.TextView
                            let length = min count (caretLine.EndIncludingLineBreak.Position - caretPoint.Position)
                            let span = SnapshotSpanUtil.CreateWithLength caretPoint length
                            _operations.DeleteSpan span


                    match change with
                    | RepeatableChange.TextChange(change) -> repeatTextBufferChange change
                    | RepeatableChange.CommandChange(data) -> 

                        let countOpt = match countOpt with | Some(count) -> Some(count) | None -> data.Count
                        let reg = data.Register
                        let commandName = data.Command.KeyInputSet.Name

                        // Repeating a visual command is more complex because we need to calculate the
                        // new visual range
                        let repeatVisualOperation func = 
                            match data.VisualRunData with
                            | None -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                            | Some(oldSpan) ->
                                let span = _visualSpanCalculator.CalculateForTextView _bufferData.TextView oldSpan
                                func countOpt reg span |> ignore

                        match data.Command with 
                        | SimpleCommand(_, _, func) -> func countOpt reg |> ignore
                        | MotionCommand(_, _, func) -> 
    
                            // Repeating a motion based command is a bit more complex because we need to
                            // first re-run the motion to get the span to be processed
                            match data.MotionRunData with
                            | None -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                            | Some motionRunData ->
    
                                // Repeat the motion and process the results
                                match _bufferData.TextViewMotionUtil.GetMotion motionRunData.Motion motionRunData.MotionArgument with
                                | None ->  _statusUtil.OnError Resources.NormalMode_UnableToRepeatMotion
                                | Some(motionData) -> func countOpt reg motionData |> ignore

                        | LongCommand(_) -> 
                            _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                        | VisualCommand(_, _, _, func) -> 
                            repeatVisualOperation func
                        | LongVisualCommand(_, _, _, func) -> 
                            repeatVisualOperation func

                    | RepeatableChange.LinkedChange(left, right) ->
                        repeatChange left countOpt
                        repeatChange right None

                match _bufferData.Vim.ChangeTracker.LastChange with
                | None -> _operations.Beep()
                | Some(lastChange) -> repeatChange lastChange countOpt 
            finally
                _data <- { _data with IsInRepeatLastChange = false }

    /// Get the informatoin on how to handle the tilde command based on the current setting for tildeop
    member private this.GetTildeCommand count =
        let name = KeyInputUtil.CharToKeyInput '~' |> OneKeyInput
        let command = 
            if _bufferData.Settings.GlobalSettings.TildeOp then
                let func count reg (data:MotionData) = 
                    _operations.ChangeLetterCase data.OperationEditSpan
                    CommandResult.Completed ModeSwitch.NoSwitch
                MotionCommand(name, CommandFlags.Repeatable, func)
            else
                let func count _ = 
                    let count = CommandUtil.CountOrDefault count
                    _operations.ChangeLetterCaseAtCursor count
                    CommandResult.Completed ModeSwitch.NoSwitch
                SimpleCommand(name, CommandFlags.Repeatable, func)
        name,command

    /// Create the set of Command values which are not repeatable 
    member this.CreateLongCommands() = 

        seq {
            yield (
                "r", 
                CommandFlags.None, 
                fun count reg -> this.ReplaceChar count reg) 
            yield (
                "'", 
                CommandFlags.Movement, 
                fun count reg -> this.WaitJumpToMark count reg)
            yield (
                "`", 
                CommandFlags.Movement, 
                fun count reg -> this.WaitJumpToMark count reg)
            yield (
                "m", 
                CommandFlags.Movement, 
                fun count reg -> this.WaitMark count reg)
        }
        |> Seq.map (fun (str,kind, func) -> 
            let name = KeyNotationUtil.StringToKeyInput str |> OneKeyInput
            let func2 count reg = 
                let count = CommandUtil.CountOrDefault count
                func count reg 
            LongCommand(name, kind, func2))

    /// Create the simple commands
    member this.CreateSimpleCommands() =

        let join count kind = 
            let line = TextViewUtil.GetCaretLine this.TextView
            let count = if count = 1 then 2 else count
            match SnapshotLineRangeUtil.CreateForLineAndCount line count with
            | None -> _bufferData.Vim.VimHost.Beep()
            | Some(range) -> _operations.Join range kind

        let doNothing _ _ = ()
        let commands = 
            seq {
                yield (
                    "dd", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let span = _operations.DeleteLinesIncludingLineBreak count 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise)
                yield (
                    "yy", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                        let point = point.GetContainingLine().Start
                        let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Yank span OperationKind.LineWise )
                yield (
                    "<lt><lt>", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ShiftLinesLeft count)
                yield (
                    ">>", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ShiftLinesRight count)
                yield (
                    "&",
                    CommandFlags.Special,
                    ModeSwitch.NoSwitch,
                    fun _ _ -> 
                        let data = _bufferData.Vim.VimData
                        match data.LastSubstituteData with
                        | Some(data) -> 
                            let range = TextViewUtil.GetCaretLineRange _bufferData.TextView 1
                            _operations.Substitute data.SearchPattern data.Substitute range SubstituteFlags.None 
                        | None -> () )
                yield (
                    "gJ", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> join count JoinKind.KeepEmptySpaces )
                yield (
                    "gp", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.After true)
                yield (
                    "gP", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before true)
                yield (
                    "g&", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> 
                        let data = _bufferData.Vim.VimData
                        match data.LastSubstituteData with
                        | Some(data) -> 
                            let range = SnapshotLineRangeUtil.CreateForSnapshot this.TextBuffer.CurrentSnapshot
                            _operations.Substitute data.SearchPattern data.Substitute range data.Flags
                        | _ -> () )
                yield (
                    "u",  
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.Undo count)
                yield (
                    "zo", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.OpenFold (TextViewUtil.GetCaretLineRange _bufferData.TextView 1).Extent count)
                yield (
                    "zO", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.OpenAllFolds (TextViewUtil.GetCaretLineRange _bufferData.TextView 1).Extent)
                yield (
                    "zc", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.CloseFold (TextViewUtil.GetCaretLineRange _bufferData.TextView 1).Extent count)
                yield (
                    "zC", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.CloseAllFolds (TextViewUtil.GetCaretLineRange _bufferData.TextView 1).Extent)
                yield (
                    "zt", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ ->  _operations.EditorOperations.ScrollLineTop())
                yield (
                    "z.", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> 
                        _operations.EditorOperations.ScrollLineCenter() 
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield (
                    "zz", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.EditorOperations.ScrollLineCenter() )
                yield (
                    "z-", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ ->
                        _operations.EditorOperations.ScrollLineBottom() 
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield (
                    "zb", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.EditorOperations.ScrollLineBottom() )
                yield (
                    "zF", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.FoldLines count)
                yield (
                    "zd", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.DeleteOneFoldAtCursor() )
                yield (
                    "zD", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.DeleteAllFoldsAtCursor() )
                yield (
                    "zE", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.FoldManager.DeleteAllFolds() )
                yield (
                    "ZZ",
                    CommandFlags.Special,
                    ModeSwitch.NoSwitch,
                    fun _ _ ->  _operations.Close(true) |> ignore )
                yield (
                    "X", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let span = _operations.DeleteCharacterBeforeCursor count
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise)
                yield (
                    "p", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.After false)
                yield (
                    "P", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before false)
                yield (
                    "D", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let span = _operations.DeleteLinesFromCursor count
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise)
                yield (
                    "<C-r>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.Redo count)
                yield (
                    "<C-u>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Up count)
                yield (
                    "<C-d>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Down count)
                yield (
                    "<C-y>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollLines ScrollDirection.Up count)
                yield (
                    "<C-e>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollLines ScrollDirection.Down count)
                yield (
                    "<C-f>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "<S-Down>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "<PageDown>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "J", 
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> join count JoinKind.RemoveEmptySpaces )
                yield (
                    "<C-b>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<S-Up>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<PageUp>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<C-]>", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.GoToDefinitionWrapper())
                yield (
                    "gf", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _operations.GoToFile())
                yield (
                    "Y", 
                    CommandFlags.Special, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let point = 
                            this.TextView 
                            |> TextViewUtil.GetCaretLine 
                            |> SnapshotLineUtil.GetStart
                        let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Yank span OperationKind.LineWise)
                yield (
                    "<C-i>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.JumpNext count)
                yield (
                    "<C-o>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> _operations.JumpPrevious count)
                yield (
                    "<C-w><C-j>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewDown(this.TextView))
                yield (
                    "<C-w>j", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewDown(this.TextView))
                yield (
                    "<C-w><C-k>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewUp(this.TextView))
                yield (
                    "<C-w>k", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewUp(this.TextView))
                yield (
                    "z<Enter>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch,
                    fun count _ -> 
                        _operations.EditorOperations.ScrollLineTop()
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield (
                    "==",
                    CommandFlags.Repeatable,
                    ModeSwitch.NoSwitch,
                    fun count _ -> 
                        let range = TextViewUtil.GetCaretLineRange this.TextView count 
                        _bufferData.Vim.VimHost.FormatLines this.TextView range )
                yield (
                    "cc",
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                    ModeSwitch.SwitchMode ModeKind.Insert,
                    fun count reg ->  
                        let getDeleteSpan (range:SnapshotLineRange) =
                            if _settings.AutoIndent then 
                                let start = TextViewUtil.GetCaretLineIndent _textView
                                TextViewUtil.MoveCaretToPoint _textView start
                                SnapshotSpanUtil.Create start range.End
                            else
                                range.Extent
                        let span = TextViewUtil.GetCaretLineRange _textView count |> getDeleteSpan
                        _operations.DeleteSpan span 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise)
                yield (
                    "i", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    doNothing)
                yield (
                    "I", 
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun _ _ -> _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false)))
                yield (
                    ":", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Command, 
                    doNothing)
                yield (
                    "A", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun _ _ -> _operations.EditorOperations.MoveToEndOfLine(false)))
                yield (
                    "o", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun _ _ -> _operations.InsertLineBelow() |> ignore))
                yield (
                    "O", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun _ _ -> _operations.InsertLineAbove() |> ignore))
                yield (
                    "v", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.VisualCharacter, 
                    doNothing)
                yield (
                    "V", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.VisualLine, 
                    doNothing)
                yield (
                    "<C-q>", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.VisualBlock, 
                    doNothing)
                yield (
                    "s", 
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun count reg -> 
                        let span = _operations.DeleteCharacterAtCursor count 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise))
                yield (
                    "C", 
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun count reg -> 
                        let span = _operations.DeleteLinesFromCursor count 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise))
                yield (
                    "S", 
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun count reg -> 
                        let span = _operations.DeleteLines count 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise))
                yield (
                    "a", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Insert, 
                    (fun _ _ -> _operations.MoveCaretForAppend()) )
                yield (
                    "R", 
                    CommandFlags.Special,
                    ModeSwitch.SwitchMode ModeKind.Replace, 
                    doNothing)
            } |> Seq.map (fun (str, kind, switch, func) -> (str, kind, func, CommandResult.Completed switch))

        let aliasedCommands = 
            seq {
                yield (
                    ["x"; "<Del>"],
                    CommandFlags.Repeatable, 
                    ModeSwitch.NoSwitch,
                    fun count reg -> 
                        let span = _operations.DeleteCharacterAtCursor count 

                        // Need to respect the virtual edit setting here as we could have 
                        // deleted the last character on the line
                        _operations.MoveCaretForVirtualEdit()
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise)
                yield (
                    ["gUgU"; "gUU"],
                    CommandFlags.Repeatable,
                    ModeSwitch.NoSwitch,
                    fun _ _ ->
                        let line = TextViewUtil.GetCaretLine _textView
                        let span = EditSpan.Single (line.Extent)
                        _operations.ChangeLetterCaseToUpper span)
                yield (
                    ["gugu"; "guu"],
                    CommandFlags.Repeatable,
                    ModeSwitch.NoSwitch,
                    fun _ _ ->
                        let line = TextViewUtil.GetCaretLine _textView
                        let span = EditSpan.Single (line.Extent)
                        _operations.ChangeLetterCaseToLower span)
                yield (
                    ["g~g~"; "g~~"],
                    CommandFlags.Repeatable,
                    ModeSwitch.NoSwitch,
                    fun _ _ -> 
                        let line = TextViewUtil.GetCaretLine _textView
                        let span = EditSpan.Single (line.Extent)
                        _operations.ChangeLetterCase span)
                yield (
                    ["g?g?"; "g??"],
                    CommandFlags.Repeatable,
                    ModeSwitch.NoSwitch,
                    fun _ _ -> 
                        let line = TextViewUtil.GetCaretLine _textView
                        let span = EditSpan.Single (line.Extent)
                        _operations.ChangeLetterRot13 span)
            } |> Seq.map (fun (names, kind, switch, func) -> 
                names |> Seq.map (fun str -> (str, kind, func, CommandResult.Completed switch)))
              |> Seq.concat

        let allWithCount = 
            Seq.append commands aliasedCommands
            |> Seq.map(fun (str,kind,func,result) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str
                let func2 count reg =
                    let count = CommandUtil.CountOrDefault count
                    func count reg
                    result
                SimpleCommand(name, kind, func2))

        let needCountAsOpt = 
            seq {
                yield (
                    ["."], 
                    CommandFlags.Special, 
                    fun count reg -> this.RepeatLastChange count reg)
                yield (
                    ["<C-Home>"], 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.GoToLineOrFirst(count))
                yield (
                    ["gt"; "<C-PageDown>"], 
                    CommandFlags.Movement, 
                    fun count _ -> 
                        match count with 
                        | None -> _operations.GoToNextTab Direction.Forward 1
                        | Some(count) -> _operations.GoToTab count)
                yield (
                    ["gT"; "<C-PageUp>"], 
                    CommandFlags.Movement, 
                    fun count _ -> 
                        let count = OptionUtil.getOrDefault 1 count
                        _operations.GoToNextTab Direction.Backward count)
            }
            |> Seq.map(fun (nameList,kind,func) -> 
                nameList |> Seq.map (fun str ->
                    let name = KeyNotationUtil.StringToKeyInputSet str
                    let func2 count reg = 
                        func count reg
                        CommandResult.Completed ModeSwitch.NoSwitch
                    SimpleCommand(name, kind, func2)))
            |> Seq.concat

        Seq.append allWithCount needCountAsOpt 

    /// Create all motion commands
    member this.CreateMotionCommands() =
    
        let complex : seq<string * CommandFlags * ModeKind option * (int -> Register -> MotionData -> unit)> =
            seq {
                yield (
                    "c", 
                    CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, 
                    Some ModeKind.Insert, 
                    fun _ reg data -> 
                        let span = _operations.ChangeSpan data 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span data.OperationKind)
                yield (
                    "d", 
                    CommandFlags.None, 
                    None, 
                    fun _ reg data -> 
                        _operations.DeleteSpan data.OperationSpan 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete data.OperationSpan data.OperationKind)
                yield (
                    "gU",
                    CommandFlags.Repeatable,
                    None,
                    fun _ _ data -> _operations.ChangeLetterCaseToUpper data.EditSpan)
                yield (
                    "gu",
                    CommandFlags.Repeatable,
                    None,
                    fun _ _ data -> _operations.ChangeLetterCaseToLower data.EditSpan)
                yield (
                    "g?",
                    CommandFlags.Repeatable,
                    None,
                    fun _ _ data -> _operations.ChangeLetterRot13 data.EditSpan)
                yield (
                    "y", 
                    CommandFlags.None, 
                    None, 
                    fun _ reg data -> _operations.UpdateRegisterForSpan reg RegisterOperation.Yank data.OperationSpan data.OperationKind)
                yield (
                    "<lt>", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.ShiftLineRangeLeft 1 data.OperationLineRange)
                yield (
                    ">", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.ShiftLineRangeRight 1 data.OperationLineRange)
                yield (
                    "zf", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.FoldManager.CreateFold data.OperationSpan)
                yield (
                    "=",
                    CommandFlags.Repeatable,
                    None,
                    fun _ _ data -> _bufferData.Vim.VimHost.FormatLines this.TextView data.OperationLineRange)
            }

        complex
        |> Seq.map (fun (str, extraFlags, modeKindOpt, func) ->
            let name = KeyNotationUtil.StringToKeyInputSet str
            let func2 count reg data =
                let count = CommandUtil.CountOrDefault count
                func count reg data
                match modeKindOpt with
                | None -> CommandResult.Completed ModeSwitch.NoSwitch
                | Some(modeKind) -> CommandResult.Completed (ModeSwitch.SwitchMode modeKind)
            let flags = extraFlags ||| CommandFlags.Repeatable
            MotionCommand(name, flags, func2))

    member this.Reset() =
        _runner.ResetState()
        _data <- _emptyData
    
    member this.ProcessCore (ki:KeyInput) =
        let command = _data.Command + ki.Char.ToString()
        _data <- {_data with Command=command }

        match _runner.Run ki with
        | RunKeyInputResult.NeedMoreKeyInput -> ProcessResult.Processed
        | RunKeyInputResult.NestedRunDetected -> ProcessResult.Processed
        | RunKeyInputResult.CommandRan(_,modeSwitch) ->

            // If we are in the one time mode then switch back to the previous
            // mode
            let modeSwitch = 
                match _data.OneTimeMode with
                | None -> modeSwitch 
                | Some(modeKind) -> ModeSwitch.SwitchMode modeKind

            this.Reset()
            ProcessResult.OfModeSwitch modeSwitch
        | RunKeyInputResult.CommandErrored(_) -> 
            this.Reset()
            ProcessResult.Processed
        | RunKeyInputResult.CommandCancelled -> 
            this.Reset()
            ProcessResult.Processed
        | RunKeyInputResult.NoMatchingCommand ->
            this.Reset()
            _operations.Beep()
            ProcessResult.Processed

    interface INormalMode with 
        member this.KeyRemapMode = this.KeyRemapMode
        member this.IsInReplace = _data.IsInReplace
        member this.VimBuffer = _bufferData
        member this.Command = this.Command
        member this.CommandRunner = _runner
        member this.CommandNames = 
            this.EnsureCommands()
            _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)

        member this.ModeKind = ModeKind.Normal
        member this.OneTimeMode = _data.OneTimeMode

        member this.CanProcess (ki:KeyInput) =
            let doesCommandStartWith ki =
                let name = OneKeyInput ki
                _runner.Commands 
                |> Seq.filter (fun command -> command.KeyInputSet.StartsWith name)
                |> SeqUtil.isNotEmpty

            if _displayWindowBroker.IsSmartTagSessionActive then false
            elif _displayWindowBroker.IsCompletionActive then false
            elif _displayWindowBroker.IsSignatureHelpActive then false
            elif _runner.IsWaitingForMoreInput then  true
            elif doesCommandStartWith ki then true
            elif Option.isSome ki.RawChar && KeyModifiers.None = ki.KeyModifiers && Set.contains ki.Char _coreCharSet then true
            else false

        member this.Process ki = this.ProcessCore ki
        member this.OnEnter arg = 
            this.EnsureCommands()
            this.Reset()
            
            // Process the argument if it's applicable
            match arg with 
            | ModeArgument.None -> ()
            | ModeArgument.FromVisual -> ()
            | ModeArgument.Subsitute(_) -> ()
            | ModeArgument.OneTimeCommand(modeKind) -> _data <- { _data with OneTimeMode = Some modeKind }

        member this.OnLeave () = ()
        member this.OnClose() = _eventHandlers.DisposeAll()
    

