#light

namespace Vim.Modes.Normal
open Vim
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
        _incrementalSearch : IIncrementalSearch,
        _statusUtil : IStatusUtil,
        _displayWindowBroker : IDisplayWindowBroker,
        _runner : ICommandRunner,
        _capture : IMotionCapture,
        _visualSpanCalculator : IVisualSpanCalculator ) as this =

    /// Reset state for data in Normal Mode
    let _emptyData = {
        Command = StringUtil.empty
        IsInRepeatLastChange = false
        IsInReplace = false
        OneTimeMode = None
    }

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
    member this.IncrementalSearch = _incrementalSearch
    member this.IsCommandRunnerPopulated = _runner.Commands |> SeqUtil.isNotEmpty
    member this.Command = _data.Command
    member this.Commands = 
        this.EnsureCommands()
        _runner.Commands

    member this.IsOperatorPending = 
        let isOperator command = 
            match command with 
            | SimpleCommand(_) -> false
            | LongCommand(_) -> false
            | VisualCommand(_) -> false 
            | MotionCommand(_) -> not command.IsMovement

        match _runner.State with
        | NoInput -> false
        | NotEnoughInput -> false
        | NotEnoughMatchingPrefix(command,_) -> isOperator command
        | NotFinishWithCommand(command) -> isOperator command

    member private this.EnsureCommands() = 
        if not this.IsCommandRunnerPopulated then
            this.CreateSimpleCommands()
            |> Seq.append (this.CreateMovementCommands())
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

    /// Begin an incremental search.  Called when the user types / into the editor
    member this.BeginIncrementalSearch (kind:SearchKind) count reg =
        let before = TextViewUtil.GetCaretPoint _bufferData.TextView
        let rec inner (ki:KeyInput) = 
            match _incrementalSearch.Process ki with
            | SearchComplete -> 
                _bufferData.JumpList.Add before |> ignore
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
            | SearchCancelled -> LongCommandResult.Cancelled
            | SearchNeedMore ->  LongCommandResult.NeedMoreInput inner
        _incrementalSearch.Begin kind
        LongCommandResult.NeedMoreInput inner
    
    member private x.ReplaceChar count reg = 
        let inner (ki:KeyInput) = 
            _data <- { _data with IsInReplace = false }
            if ki.Key = VimKey.Escape then LongCommandResult.Cancelled
            else 
                if not (_operations.ReplaceChar ki count) then
                    _operations.Beep()
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        _data <- { _data with IsInReplace = true }
        LongCommandResult.NeedMoreInput inner

    member x.WaitJumpToMark (count:int) (reg:Register) =
        let waitForKey (ki:KeyInput)  =
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap 
            match res with 
            | Modes.Failed(msg) -> _statusUtil.OnError msg
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.WaitMark (count:int) (reg:Register)= 
        let waitForKey (ki:KeyInput) =
            let cursor = TextViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _operations.Beep()
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput waitForKey

    /// Implements the '.' operator.  This is a special command in that it cannot be easily routed 
    /// to interfaces like ICommonOperations due to the complexity of repeating the command here.  
    member private this.RepeatLastChange countOpt reg =  

        if _data.IsInRepeatLastChange then _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
        else
            _data <- { _data with IsInRepeatLastChange = true }
            try

                let rec repeatChange change countOpt =
                    match change with
                    | TextChange(newText) -> _operations.InsertText newText (CommandUtil.CountOrDefault countOpt) 
                    | CommandChange(data) -> 
                        let countOpt = match countOpt with | Some(count) -> Some(count) | None -> data.Count
                        let reg = data.Register
                        let commandName = data.Command.KeyInputSet.Name
                        match data.Command with 
                        | SimpleCommand(_,_,func) -> func countOpt reg |> ignore
                        | MotionCommand(_,_,func) -> 
    
                            // Repeating a motion based command is a bit more complex because we need to
                            // first re-run the motion to get the span to be processed
                            match data.MotionRunData with
                            | None -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                            | Some(motionRunData) ->
    
                                // Repeat the motion and process the results
                                let motionName = motionRunData.MotionCommand.KeyInputSet.Name
                                match motionRunData.MotionFunction MotionUse.AfterOperator motionRunData.Count with
                                | None ->  _statusUtil.OnError (Resources.NormalMode_UnableToRepeatMotion commandName motionName)
                                | Some(motionData) -> func countOpt reg motionData |> ignore
    
                        | LongCommand(_) -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                        | VisualCommand(_,_,kind,func) -> 
                            // Repeating a visual command is more complex because we need to calculate the
                            // new visual range
                            match data.VisualRunData with
                            | None -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand commandName)
                            | Some(oldSpan) ->
                                let span = _visualSpanCalculator.CalculateForTextView _bufferData.TextView oldSpan
                                func countOpt reg span |> ignore

                                
                    | LinkedChange(left, right) ->
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
                    _operations.ChangeLetterCase data.OperationSpan
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
                "/", 
                CommandFlags.Movement ||| CommandFlags.HandlesEscape, 
                fun count reg -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap count reg)
            yield (
                "?", 
                CommandFlags.Movement, 
                fun count reg -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap count reg)
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

        let noSwitch = 
            seq {
                yield (
                    "dd", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.DeleteLinesIncludingLineBreak count reg)
                yield (
                    "yy", 
                    CommandFlags.Repeatable, 
                    fun count reg -> 
                        let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                        let point = point.GetContainingLine().Start
                        let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                        _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg  )
                yield (
                    "<lt><lt>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ShiftLinesLeft count)
                yield (
                    ">>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ShiftLinesRight count)
                yield (
                    "gJ", 
                    CommandFlags.Repeatable, 
                    fun count reg -> 
                        let view = _bufferData.TextView
                        let caret = TextViewUtil.GetCaretPoint view
                        _operations.Join caret Modes.JoinKind.KeepEmptySpaces count |> ignore )
                yield (
                    "gp", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteAfterCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield (
                    "gP", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteBeforeCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield (
                    "g*", 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count)
                yield (
                    "g#", 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count)
                yield (
                    "gt", 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.GoToNextTab count)
                yield (
                    "gT", 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.GoToPreviousTab count)
                yield (
                    "u",  
                    CommandFlags.Special, 
                    fun count _ -> _operations.Undo count)
                yield (
                    "zo", 
                    CommandFlags.Special, 
                    fun count _ -> _operations.OpenFold (TextViewUtil.GetCaretLineSpan _bufferData.TextView) count)
                yield (
                    "zO", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.OpenAllFolds (TextViewUtil.GetCaretLineSpan _bufferData.TextView) )
                yield (
                    "zc", 
                    CommandFlags.Special, 
                    fun count _ -> _operations.CloseFold (TextViewUtil.GetCaretLineSpan _bufferData.TextView) count)
                yield (
                    "zC", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.CloseAllFolds (TextViewUtil.GetCaretLineSpan _bufferData.TextView) )
                yield (
                    "zt", 
                    CommandFlags.Movement, 
                    fun _ _ ->  _operations.EditorOperations.ScrollLineTop())
                yield (
                    "z.", 
                    CommandFlags.Movement, 
                    fun _ _ -> 
                        _operations.EditorOperations.ScrollLineCenter() 
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield (
                    "zz", 
                    CommandFlags.Movement, 
                    fun _ _ -> _operations.EditorOperations.ScrollLineCenter() )
                yield (
                    "z-", 
                    CommandFlags.Movement, 
                    fun _ _ ->
                        _operations.EditorOperations.ScrollLineBottom() 
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield (
                    "zb", 
                    CommandFlags.Movement, 
                    fun _ _ -> _operations.EditorOperations.ScrollLineBottom() )
                yield (
                    "zF", 
                    CommandFlags.Special, 
                    fun count _ -> _operations.FoldLines count)
                yield (
                    "zd", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.DeleteOneFoldAtCursor() )
                yield (
                    "zD", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.DeleteAllFoldsAtCursor() )
                yield (
                    "zE", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.FoldManager.DeleteAllFolds() )
                yield (
                    "x", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.DeleteCharacterAtCursor count reg)
                yield (
                    "<Del>", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.DeleteCharacterAtCursor count reg)
                yield (
                    "X", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.DeleteCharacterBeforeCursor count reg)
                yield (
                    "p", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false)
                yield (
                    "P", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteBeforeCursor reg.StringValue count reg.Value.OperationKind false)
                yield (
                    "n", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveToNextOccuranceOfLastSearch count false)
                yield (
                    "N", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveToNextOccuranceOfLastSearch count true)
                yield (
                    "*", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.ForwardWithWrap count)
                yield (
                    "#", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.BackwardWithWrap count)
                yield (
                    "D", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.DeleteLinesFromCursor count reg)
                yield (
                    "<C-r>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.Redo count)
                yield (
                    "<C-u>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Up count)
                yield (
                    "<C-d>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Down count)
                yield (
                    "<C-y>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollLines ScrollDirection.Up count)
                yield (
                    "<C-e>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollLines ScrollDirection.Down count)
                yield (
                    "<C-f>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "<S-Down>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "<PageDown>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Down count)
                yield (
                    "J", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.JoinAtCaret count)
                yield (
                    "<C-b>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<S-Up>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<PageUp>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.ScrollPages ScrollDirection.Up count)
                yield (
                    "<C-]>", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.GoToDefinitionWrapper())
                yield (
                    "gd", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.GoToLocalDeclaration())
                yield (
                    "gD", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.GoToGlobalDeclaration())
                yield (
                    "gf", 
                    CommandFlags.Special, 
                    fun _ _ -> _operations.GoToFile())
                yield (
                    "Y", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.YankLines count reg)
                yield (
                    "<Tab>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.JumpNext count)
                yield (
                    "<C-i>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.JumpNext count)
                yield (
                    "<C-o>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.JumpPrevious count)
                yield (
                    "%", 
                    CommandFlags.Repeatable, 
                    fun _ _ -> _operations.GoToMatch() |> ignore)
                yield (
                    "<C-w><C-j>", 
                    CommandFlags.Movement, 
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewDown(this.TextView))
                yield (
                    "<C-w>j", 
                    CommandFlags.Movement, 
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewDown(this.TextView))
                yield (
                    "<C-w><C-k>", 
                    CommandFlags.Movement, 
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewUp(this.TextView))
                yield (
                    "<C-w>k", 
                    CommandFlags.Movement, 
                    fun _ _ -> _bufferData.Vim.VimHost.MoveViewUp(this.TextView))
                yield (
                    "<C-PageDown>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.GoToNextTab count)
                yield (
                    "<C-PageUp>", 
                    CommandFlags.Repeatable, 
                    fun count _ -> _operations.GoToPreviousTab count)
                yield (
                    "z<Enter>", 
                    CommandFlags.Movement, 
                    fun count _ -> 
                        _operations.EditorOperations.ScrollLineTop()
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
            }
            |> Seq.map(fun (str,kind,func) -> (str,kind,func,CommandResult.Completed ModeSwitch.NoSwitch))

        let doNothing _ _ = ()
        let doSwitch =
            seq {
                yield (
                    "cc", 
                    ModeKind.Insert, 
                    fun count reg ->  
                        let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                        let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                        let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                        _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore )
                yield (
                    "i", 
                    ModeKind.Insert, 
                    doNothing)
                yield (
                    "I", 
                    ModeKind.Insert, 
                    (fun _ _ -> _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false)))
                yield (
                    ":", 
                    ModeKind.Command, 
                    doNothing)
                yield (
                    "A", 
                    ModeKind.Insert, 
                    (fun _ _ -> _operations.EditorOperations.MoveToEndOfLine(false)))
                yield (
                    "o", 
                    ModeKind.Insert, 
                    (fun _ _ -> _operations.InsertLineBelow() |> ignore))
                yield (
                    "O", 
                    ModeKind.Insert, 
                    (fun _ _ -> _operations.InsertLineAbove() |> ignore))
                yield (
                    "v", 
                    ModeKind.VisualCharacter, 
                    doNothing)
                yield (
                    "V", 
                    ModeKind.VisualLine, 
                    doNothing)
                yield (
                    "<C-q>", 
                    ModeKind.VisualBlock, 
                    doNothing)
                yield (
                    "s", 
                    ModeKind.Insert, 
                    (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (
                    "C", 
                    ModeKind.Insert, 
                    (fun count reg -> _operations.DeleteLinesFromCursor count reg))
                yield (
                    "S", 
                    ModeKind.Insert, 
                    (fun count reg -> _operations.DeleteLines count reg))
                yield (
                    "a", 
                    ModeKind.Insert, 
                    (fun _ _ -> _operations.MoveCaretForAppend()) )
                yield (
                    "R", 
                    ModeKind.Replace, 
                    doNothing)
            }
            |> Seq.map(fun (str,switch,func) -> (str,CommandFlags.None,func,CommandResult.Completed (ModeSwitch.SwitchMode switch)))

        let allWithCount = 
            Seq.append noSwitch doSwitch 
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
                    ".", 
                    CommandFlags.Special, 
                    fun count reg -> this.RepeatLastChange count reg)
                yield (
                    "<C-Home>", 
                    CommandFlags.Movement, 
                    fun count _ -> _operations.GoToLineOrFirst(count))
            }
            |> Seq.map(fun (str,kind,func) -> 
                let name = KeyNotationUtil.StringToKeyInputSet str
                let func2 count reg = 
                    func count reg
                    CommandResult.Completed ModeSwitch.NoSwitch
                SimpleCommand(name, kind, func2))

        Seq.append allWithCount needCountAsOpt 

    /// Create all motion commands
    member this.CreateMotionCommands() =
    
        let complex : seq<string * CommandFlags * ModeKind option * (int -> Register -> MotionData -> unit)> =
            seq {
                yield (
                    "d", 
                    CommandFlags.None, 
                    None, 
                    fun _ reg data -> _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore)
                yield (
                    "y", 
                    CommandFlags.None, 
                    None, 
                    fun _ reg data -> _operations.Yank data.OperationSpan data.MotionKind data.OperationKind reg)
                yield (
                    "c", 
                    CommandFlags.LinkedWithNextTextChange, 
                    Some ModeKind.Insert, 
                    fun _ reg data -> _operations.ChangeSpan data reg)
                yield (
                    "<lt>", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.ShiftSpanLeft 1 data.OperationSpan)
                yield (
                    ">", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.ShiftSpanRight 1 data.OperationSpan)
                yield (
                    "zf", 
                    CommandFlags.None, 
                    None, 
                    fun _ _ data -> _operations.FoldManager.CreateFold data.OperationSpan)
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

    /// Create all of the movement commands
    member this.CreateMovementCommands() =
        let factory = Vim.Modes.CommandFactory(_operations, _capture)
        factory.CreateMovementCommands()

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
        member this.IsOperatorPending = this.IsOperatorPending
        member this.IsWaitingForInput = _runner.IsWaitingForMoreInput
        member this.IncrementalSearch = _incrementalSearch
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
            elif CharUtil.IsLetterOrDigit(ki.Char) then true
            elif doesCommandStartWith ki then true
            elif KeyInputUtil.CoreCharactersSet |> Set.contains ki.Char then true
            else false

        member this.Process ki = this.ProcessCore ki
        member this.OnEnter arg = 
            this.EnsureCommands()
            this.Reset()
            
            // Process the argument if it's applicable
            match arg with 
            | ModeArgument.None -> ()
            | ModeArgument.FromVisual -> ()
            | ModeArgument.OneTimeCommand(modeKind) -> _data <- { _data with OneTimeMode = Some modeKind }

        member this.OnLeave () = ()
        member this.OnClose() = _eventHandlers.DisposeAll()
    

