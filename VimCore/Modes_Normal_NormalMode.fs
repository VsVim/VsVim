#light

namespace Vim.Modes.Normal
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalModeData = {
    Command : string
    IsInReplace : bool
    OneTimeMode : ModeKind option 
}

type internal NormalMode 
    ( 
        _buffer : IVimBuffer, 
        _operations : ICommonOperations,
        _statusUtil : IStatusUtil,
        _displayWindowBroker : IDisplayWindowBroker,
        _runner : ICommandRunner,
        _capture : IMotionCapture
    ) as this =

    let _textView = _buffer.TextView
    let _settings = _buffer.Settings

    /// Reset state for data in Normal Mode
    let _emptyData = {
        Command = StringUtil.empty
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
        let settings = _buffer.Settings.GlobalSettings :> IVimSettings
        settings.SettingChanged.Subscribe this.OnGlobalSettingsChanged |> _eventHandlers.Add

    member this.TextView = _buffer.TextView
    member this.TextBuffer = _buffer.TextBuffer
    member this.CaretPoint = _buffer.TextView.Caret.Position.BufferPosition
    member this.Settings = _buffer.Settings
    member this.IsCommandRunnerPopulated = _runner.Commands |> SeqUtil.isNotEmpty
    member this.KeyRemapMode = 
        match _runner.KeyRemapMode with
        | Some remapMode -> remapMode
        | None -> KeyRemapMode.Normal
    member this.Command = _data.Command
    member this.Commands = 
        this.EnsureCommands()
        _runner.Commands

    member x.EnsureCommands() = 
        if not x.IsCommandRunnerPopulated then
            let factory = Vim.Modes.CommandFactory(_operations, _capture, _buffer.MotionUtil, _buffer.JumpList, _buffer.Settings)

            x.CreateCommandBindings()
            |> Seq.append (factory.CreateMovementCommands())
            |> Seq.append (factory.CreateScrollCommands())
            |> Seq.iter _runner.Add

            // Add in the special ~ command
            let _, command = x.GetTildeCommand()
            _runner.Add command

            // Add in the macro command
            factory.CreateMacroEditCommands _runner _buffer.Vim.MacroRecorder _eventHandlers

    /// Raised when a global setting is changed
    member x.OnGlobalSettingsChanged (setting:Setting) = 
        
        // If the 'tildeop' setting changes we need to update how we handle it
        if StringUtil.isEqual setting.Name GlobalSettingNames.TildeOpName && x.IsCommandRunnerPopulated then
            let name, command = x.GetTildeCommand()
            _runner.Remove name
            _runner.Add command

    /// Bind the character in a replace character command: 'r'.  
    member x.BindReplaceChar () =
        let func () = 
            _data <- { _data with IsInReplace = true }
            BindData<_>.CreateForSingle (Some KeyRemapMode.Language) (fun ki -> 
                _data <- { _data with IsInReplace = false }
                NormalCommand.ReplaceChar ki)
        BindDataStorage.Complex func

    /// Get the information on how to handle the tilde command based on the current setting for 'tildeop'
    member x.GetTildeCommand () =
        let name = KeyInputUtil.CharToKeyInput '~' |> OneKeyInput
        let flags = CommandFlags.Repeatable
        let command = 
            if _buffer.Settings.GlobalSettings.TildeOp then
                CommandBinding.MotionBinding (name, flags, (fun motion -> NormalCommand.ChangeCaseMotion (ChangeCharacterKind.ToggleCase, motion)))
            else
                CommandBinding.NormalBinding (name, flags, NormalCommand.ChangeCaseCaretPoint ChangeCharacterKind.ToggleCase)
        name, command

    /// Create the CommandBinding instances for the supported NormalCommand values
    member x.CreateCommandBindings() =
        let normalSeq = 
            seq {
                yield ("a", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.InsertAfterCaret)
                yield ("A", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.InsertAtEndOfLine)
                yield ("C", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.ChangeTillEndOfLine)
                yield ("cc", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.ChangeLines)
                yield ("dd", CommandFlags.Repeatable, NormalCommand.DeleteLines)
                yield ("D", CommandFlags.Repeatable, NormalCommand.DeleteTillEndOfLine)
                yield ("gf", CommandFlags.None, NormalCommand.GoToFileUnderCaret false)
                yield ("gJ", CommandFlags.Repeatable, NormalCommand.JoinLines JoinKind.KeepEmptySpaces)
                yield ("gI", CommandFlags.None, NormalCommand.InsertAtStartOfLine)
                yield ("gp", CommandFlags.Repeatable, NormalCommand.PutAfterCaret true)
                yield ("gP", CommandFlags.Repeatable, NormalCommand.PutBeforeCaret true)
                yield ("gt", CommandFlags.Special, NormalCommand.GoToNextTab Path.Forward)
                yield ("gT", CommandFlags.Special, NormalCommand.GoToNextTab Path.Backward)
                yield ("gugu", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToLowerCase)
                yield ("guu", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToLowerCase)
                yield ("gUgU", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToUpperCase)
                yield ("gUU", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToUpperCase)
                yield ("g~g~", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToggleCase)
                yield ("g~~", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.ToggleCase)
                yield ("g?g?", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.Rot13)
                yield ("g??", CommandFlags.Repeatable, NormalCommand.ChangeCaseCaretLine ChangeCharacterKind.Rot13)
                yield ("g&", CommandFlags.Special, NormalCommand.RepeatLastSubstitute true)
                yield ("i", CommandFlags.None, NormalCommand.InsertBeforeCaret)
                yield ("I", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.InsertAtFirstNonBlank)
                yield ("J", CommandFlags.Repeatable, NormalCommand.JoinLines JoinKind.RemoveEmptySpaces)
                yield ("o", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.InsertLineBelow)
                yield ("O", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.InsertLineAbove)
                yield ("p", CommandFlags.Repeatable, NormalCommand.PutAfterCaret false)
                yield ("P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaret false)
                yield ("R", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, NormalCommand.ReplaceAtCaret)
                yield ("s", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.SubstituteCharacterAtCaret)
                yield ("S", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.ChangeLines)
                yield ("u", CommandFlags.Special, NormalCommand.Undo)
                yield ("v", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.VisualCharacter, ModeArgument.None))
                yield ("V", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.VisualLine, ModeArgument.None))
                yield ("x", CommandFlags.Repeatable, NormalCommand.DeleteCharacterAtCaret)
                yield ("X", CommandFlags.Repeatable, NormalCommand.DeleteCharacterBeforeCaret)
                yield ("Y", CommandFlags.None, NormalCommand.YankLines)
                yield ("yy", CommandFlags.None, NormalCommand.YankLines)
                yield ("zo", CommandFlags.Special, NormalCommand.OpenFoldUnderCaret)
                yield ("zO", CommandFlags.Special, NormalCommand.OpenAllFoldsUnderCaret)
                yield ("zc", CommandFlags.Special, NormalCommand.CloseFoldUnderCaret)
                yield ("zC", CommandFlags.Special, NormalCommand.CloseAllFoldsUnderCaret)
                yield ("zd", CommandFlags.Special, NormalCommand.DeleteFoldUnderCaret)
                yield ("zD", CommandFlags.Special, NormalCommand.DeleteAllFoldsUnderCaret)
                yield ("zE", CommandFlags.Special, NormalCommand.DeleteAllFoldsInBuffer)
                yield ("zF", CommandFlags.Special, NormalCommand.FoldLines)
                yield ("ZZ", CommandFlags.Special, NormalCommand.WriteBufferAndQuit)
                yield ("<Insert>", CommandFlags.None, NormalCommand.InsertBeforeCaret)
                yield ("<C-i>", CommandFlags.Movement, NormalCommand.JumpToNewerPosition)
                yield ("<C-o>", CommandFlags.Movement, NormalCommand.JumpToOlderPosition)
                yield ("<C-PageDown>", CommandFlags.Special, NormalCommand.GoToNextTab Path.Forward)
                yield ("<C-PageUp>", CommandFlags.Special, NormalCommand.GoToNextTab Path.Backward)
                yield ("<C-q>", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.VisualBlock, ModeArgument.None))
                yield ("<C-r>", CommandFlags.Special, NormalCommand.Redo)
                yield ("<C-w><C-j>", CommandFlags.None, NormalCommand.GoToView Direction.Down)
                yield ("<C-w>j", CommandFlags.None, NormalCommand.GoToView Direction.Down)
                yield ("<C-w><C-k>", CommandFlags.None, NormalCommand.GoToView Direction.Up)
                yield ("<C-w>k", CommandFlags.None, NormalCommand.GoToView Direction.Up)
                yield ("<C-w><C-l>", CommandFlags.None, NormalCommand.GoToView Direction.Right)
                yield ("<C-w>l", CommandFlags.None, NormalCommand.GoToView Direction.Right)
                yield ("<C-w><C-h>", CommandFlags.None, NormalCommand.GoToView Direction.Left)
                yield ("<C-w>h", CommandFlags.None, NormalCommand.GoToView Direction.Left)
                yield ("<C-w><C-s>", CommandFlags.None, NormalCommand.SplitViewHorizontally)
                yield ("<C-w>s", CommandFlags.None, NormalCommand.SplitViewHorizontally)
                yield ("<C-w><C-v>", CommandFlags.None, NormalCommand.SplitViewVertically)
                yield ("<C-w>v", CommandFlags.None, NormalCommand.SplitViewVertically)
                yield ("<C-w><C-g><C-f>", CommandFlags.None, NormalCommand.GoToFileUnderCaret true)
                yield ("<C-w>gf", CommandFlags.None, NormalCommand.GoToFileUnderCaret true)
                yield ("<C-]>", CommandFlags.Special, NormalCommand.GoToDefinition)
                yield ("<Del>", CommandFlags.Repeatable, NormalCommand.DeleteCharacterAtCaret)
                yield ("[p", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("[P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("]p", CommandFlags.Repeatable, NormalCommand.PutAfterCaretWithIndent)
                yield ("]P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("&", CommandFlags.Special, NormalCommand.RepeatLastSubstitute false)
                yield (".", CommandFlags.Special, NormalCommand.RepeatLastCommand)
                yield ("<lt><lt>", CommandFlags.Repeatable, NormalCommand.ShiftLinesLeft)
                yield (">>", CommandFlags.Repeatable, NormalCommand.ShiftLinesRight)
                yield ("==", CommandFlags.Repeatable, NormalCommand.FormatLines)
                yield (":", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.Command, ModeArgument.None))
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.NormalBinding (keyInputSet, flags, command))
            
        let motionSeq = 
            seq {
                yield ("c", CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable, NormalCommand.ChangeMotion)
                yield ("d", CommandFlags.Repeatable, NormalCommand.DeleteMotion)
                yield ("gU", CommandFlags.Repeatable, (fun motion -> NormalCommand.ChangeCaseMotion (ChangeCharacterKind.ToUpperCase, motion)))
                yield ("gu", CommandFlags.Repeatable, (fun motion -> NormalCommand.ChangeCaseMotion (ChangeCharacterKind.ToLowerCase, motion)))
                yield ("g?", CommandFlags.Repeatable, (fun motion -> NormalCommand.ChangeCaseMotion (ChangeCharacterKind.Rot13, motion)))
                yield ("y", CommandFlags.None, NormalCommand.Yank)
                yield ("zf", CommandFlags.None, NormalCommand.FoldMotion)
                yield ("<lt>", CommandFlags.Repeatable, NormalCommand.ShiftMotionLinesLeft)
                yield (">", CommandFlags.Repeatable, NormalCommand.ShiftMotionLinesRight)
                yield ("=", CommandFlags.Repeatable, NormalCommand.FormatMotion)
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.MotionBinding (keyInputSet, flags, command))

        let complexSeq = 
            seq {
                yield ("r", CommandFlags.Repeatable, x.BindReplaceChar ())
                yield ("'", CommandFlags.Movement, BindDataStorage<_>.CreateForSingleChar None NormalCommand.JumpToMark)
                yield ("`", CommandFlags.Movement, BindDataStorage<_>.CreateForSingleChar None NormalCommand.JumpToMark)
                yield ("m", CommandFlags.Special, BindDataStorage<_>.CreateForSingleChar None NormalCommand.SetMarkToCaret)
                yield ("@", CommandFlags.Special, BindDataStorage<_>.CreateForSingleChar None NormalCommand.RunMacro)
            } |> Seq.map (fun (str, flags, storage) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.ComplexNormalBinding (keyInputSet, flags, storage))
        Seq.append normalSeq motionSeq |> Seq.append complexSeq

    member this.Reset() =
        _runner.ResetState()
        _data <- _emptyData
    
    member this.ProcessCore (ki:KeyInput) =
        let command = _data.Command + ki.Char.ToString()
        _data <- { _data with Command = command }

        let run () = 
            match _runner.Run ki with
            | BindResult.NeedMoreInput _ -> 
                ProcessResult.Handled ModeSwitch.NoSwitch
            | BindResult.Complete commandData -> 
    
                // If we are in the one time mode then switch back to the previous
                // mode
                let result = 
                    match _data.OneTimeMode with
                    | None -> ProcessResult.OfCommandResult commandData.CommandResult
                    | Some modeKind -> ProcessResult.OfModeKind modeKind
    
                this.Reset()
                result
            | BindResult.Error -> 
                this.Reset()
                ProcessResult.Handled ModeSwitch.NoSwitch
            | BindResult.Cancelled -> 
                this.Reset()
                ProcessResult.Handled ModeSwitch.NoSwitch

        match ki = KeyInputUtil.EscapeKey, _data.OneTimeMode with
        | true, Some modeKind -> ProcessResult.OfModeKind modeKind
        | true, None -> run ()
        | false, _ -> run ()

    interface INormalMode with 
        member this.KeyRemapMode = this.KeyRemapMode
        member this.IsInReplace = _data.IsInReplace
        member this.VimBuffer = _buffer
        member this.Command = this.Command
        member this.CommandRunner = _runner
        member this.CommandNames = 
            this.EnsureCommands()
            _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)

        member this.ModeKind = ModeKind.Normal
        member this.OneTimeMode = _data.OneTimeMode

        member this.CanProcess (ki : KeyInput) =
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
            elif Option.isSome _data.OneTimeMode && ki = KeyInputUtil.EscapeKey then true
            else false

        member this.Process ki = this.ProcessCore ki
        member this.OnEnter arg = 
            this.EnsureCommands()
            this.Reset()

            // Process the argument if it's applicable
            match arg with 
            | ModeArgument.None -> ()
            | ModeArgument.FromVisual -> ()
            | ModeArgument.Substitute(_) -> ()
            | ModeArgument.OneTimeCommand modeKind -> _data <- { _data with OneTimeMode = Some modeKind }
            | ModeArgument.InsertWithCount _ -> ()
            | ModeArgument.InsertWithCountAndNewLine _ -> ()
            | ModeArgument.InsertWithTransaction transaction -> transaction.Complete()

        member this.OnLeave () = ()
        member this.OnClose() = _eventHandlers.DisposeAll()
    

