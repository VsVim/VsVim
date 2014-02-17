#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal CommandFactory
    ( 
        _operations : ICommonOperations, 
        _capture : IMotionCapture
    ) =

    let _textView = _operations.TextView

    /// The movement command bindings which are common to both Normal and 
    /// Visual mode
    static let SharedStandardMovementBindings = 
        seq {
            yield ("gd", NormalCommand.GoToLocalDeclaration)
            yield ("gD", NormalCommand.GoToGlobalDeclaration)
        } 
        |> Seq.map (fun (name, command) -> 
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet name
            CommandBinding.NormalBinding (keyInputSet, CommandFlags.Movement, command))
        |> List.ofSeq

    /// The set of commands which move the caret as a scroll operation
    static let SharedScrollCommands =
        seq {
            yield ("z<Enter>", CommandFlags.Movement, NormalCommand.ScrollCaretLineToTop false)
            yield ("zt", CommandFlags.Movement, NormalCommand.ScrollCaretLineToTop true)
            yield ("z.", CommandFlags.Movement, NormalCommand.ScrollCaretLineToMiddle false)
            yield ("zz", CommandFlags.Movement, NormalCommand.ScrollCaretLineToMiddle true)
            yield ("z-", CommandFlags.Movement, NormalCommand.ScrollCaretLineToBottom false)
            yield ("zb", CommandFlags.Movement, NormalCommand.ScrollCaretLineToBottom true)
            yield ("<C-b>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<C-d>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Down, true))
            yield ("<C-e>", CommandFlags.Movement, NormalCommand.ScrollWindow ScrollDirection.Down)
            yield ("<C-f>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
            yield ("<C-u>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Up, true))
            yield ("<C-y>", CommandFlags.Movement, NormalCommand.ScrollWindow ScrollDirection.Up)
            yield ("<S-Down>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
            yield ("<S-Up>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<PageUp>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<PageDown>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
        } 
        |> Seq.map (fun (str, flags, command) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
            CommandBinding.NormalBinding (keyInputSet, flags, command))
        |> List.ofSeq

    /// The set of selection starting commands.  These are only enabled when 'keymodel' contains
    /// the startsel option
    static let SharedSelectionCommands =
        seq {
            yield ("<S-Up>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.Up)
            yield ("<S-Right>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.Right)
            yield ("<S-Down>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.Down)
            yield ("<S-Left>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.Left)
            yield ("<S-Home>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.Home)
            yield ("<S-End>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.End)
            yield ("<S-PageUp>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.PageUp)
            yield ("<S-PageDown>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.PageDown)
            yield ("<C-S-Up>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlUp)
            yield ("<C-S-Right>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlRight)
            yield ("<C-S-Down>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlDown)
            yield ("<C-S-Left>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlLeft)
            yield ("<C-S-Home>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlHome)
            yield ("<C-S-End>", CommandFlags.Repeatable, NormalCommand.SwitchToSelection CaretMovement.ControlEnd)
        }
        |> Seq.map (fun (str, flags, command) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
            CommandBinding.NormalBinding (keyInputSet, flags, command))
        |> List.ofSeq

    member x.CreateScrollCommands() = 
        SharedScrollCommands

    member x.CreateSelectionCommands() = 
        SharedSelectionCommands

    /// Build up a set of NormalCommandBinding values from applicable Motion values.  These will 
    /// move the cursor to the result of the motion
    member x.CreateMovementsFromMotions() =
        let processMotionBinding (binding : MotionBinding) =

            match binding with
            | MotionBinding.Simple (name, _, motion) -> 

                // Convert the Motion into a NormalCommand which moves the caret for the given Motion
                let command = NormalCommand.MoveCaretToMotion motion
                CommandBinding.NormalBinding(name, CommandFlags.Movement, command) 

            | MotionBinding.Complex (name, motionFlags, bindDataStorage) ->

                // We're starting with a BindData<Motion> and need to instead produce a BindData<NormalCommand>
                // where the command will move the motion 
                let bindDataStorage = bindDataStorage.Convert (fun motion -> NormalCommand.MoveCaretToMotion motion)

                // Create the flags.  Make sure that we set that Escape can be handled if the
                // motion itself can handle escape
                let flags = 
                    if Util.IsFlagSet motionFlags MotionFlags.HandlesEscape then 
                        CommandFlags.Movement ||| CommandFlags.HandlesEscape
                    else
                        CommandFlags.Movement
                CommandBinding.ComplexNormalBinding (name, flags, bindDataStorage)

        _capture.MotionBindings
        |> Seq.filter (fun binding -> Util.IsFlagSet binding.MotionFlags MotionFlags.CaretMovement)
        |> Seq.map processMotionBinding
        |> List.ofSeq

    /// Create movement commands for the text-object Motions.  These are described in :help text-objects
    /// section.  All text-object motions will contain the TextObjectSelection flag
    member x.CreateMovementTextObjectCommands() =
        let processMotionBinding (binding : MotionBinding) =

            // Determine what kind of text object we are dealing with here
            let textObjectKind = 
                if Util.IsFlagSet binding.MotionFlags MotionFlags.TextObjectWithLineToCharacter then
                    TextObjectKind.LineToCharacter
                elif Util.IsFlagSet binding.MotionFlags MotionFlags.TextObjectWithAlwaysCharacter then
                    TextObjectKind.AlwaysCharacter
                elif Util.IsFlagSet binding.MotionFlags MotionFlags.TextObjectWithAlwaysLine then
                    TextObjectKind.AlwaysLine
                else
                    TextObjectKind.None

            match binding with
            | MotionBinding.Simple (name, _, motion) -> 

                let command = VisualCommand.MoveCaretToTextObject (motion, textObjectKind)
                CommandBinding.VisualBinding(name, CommandFlags.Movement, command) 

            | MotionBinding.Complex (name, motionFlags, bindDataStorage) ->

                // We're starting with a BindData<Motion> and need to instead produce a BindData<VisualCommand>
                // where the command will move the motion 
                let bindDataStorage = bindDataStorage.Convert (fun motion -> VisualCommand.MoveCaretToTextObject (motion, textObjectKind))

                // Create the flags.  Make sure that we set that Escape can be handled if the
                // motion itself can handle escape
                let flags = 
                    if Util.IsFlagSet motionFlags MotionFlags.HandlesEscape then 
                        CommandFlags.Movement ||| CommandFlags.HandlesEscape
                    else
                        CommandFlags.Movement
                CommandBinding.ComplexVisualBinding (name, flags, bindDataStorage)

        _capture.MotionBindings
        |> Seq.filter (fun binding -> Util.IsFlagSet binding.MotionFlags MotionFlags.TextObject)
        |> Seq.map processMotionBinding
        |> List.ofSeq

    member x.CreateMovementCommands() = 
        let standard = SharedStandardMovementBindings
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard 
        |> Seq.append motion
        |> List.ofSeq

    /// Create the macro editing commands for the given information.  This relies on listening to events
    /// and the observable values are added to the Disposable bag so the caller may unsubscribe at a 
    /// later time
    member x.CreateMacroEditCommands (runner : ICommandRunner) (macroRecorder : IMacroRecorder) (bag : DisposableBag) = 

        // Check IMacroRecorder state and return the proper command based on it
        let getMacroCommand () = 
            let name = KeyNotationUtil.StringToKeyInputSet "q"
            if macroRecorder.IsRecording then
                CommandBinding.NormalBinding (name, CommandFlags.Special, NormalCommand.RecordMacroStop)
            else
                CommandBinding.ComplexNormalBinding (name, CommandFlags.Special, BindDataStorage<_>.CreateForSingleChar KeyRemapMode.None NormalCommand.RecordMacroStart)
        
        // Raised when macro recording starts or stops.  
        let onMacroRecordingChanged _ = 
            let command = getMacroCommand()
            runner.Remove command.KeyInputSet
            runner.Add command

        // Need to listen to macro recording start / stop in order to insert the appropriate
        // command
        macroRecorder.RecordingStarted.Subscribe onMacroRecordingChanged |> bag.Add
        macroRecorder.RecordingStopped.Subscribe onMacroRecordingChanged |> bag.Add

        // Go ahead and add in the initial command
        runner.Add (getMacroCommand())
