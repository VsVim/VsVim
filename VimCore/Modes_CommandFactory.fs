#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor


type internal CommandFactory
    ( 
        _operations : ICommonOperations, 
        _capture : IMotionCapture,
        _motionUtil : IMotionUtil, 
        _jumpList : IJumpList,
        _settings : IVimLocalSettings ) =

    let _textView = _operations.TextView

    /// Create the movement command bindings which are common to both Normal and 
    /// Visual mode
    member x.CreateStandardMovementBindings () = 
        seq {
            yield ("gd", NormalCommand.GoToLocalDeclaration)
            yield ("gD", NormalCommand.GoToGlobalDeclaration)
        } |> Seq.map (fun (name, command) -> 
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet name
            CommandBinding.NormalBinding (keyInputSet, CommandFlags.Movement, command))

    /// Build up a set of LegacyMotionCommand values from applicable Motion values.  These will 
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
        |> Seq.filter (fun binding -> Util.IsFlagSet binding.MotionFlags MotionFlags.CursorMovement)
        |> Seq.map processMotionBinding

    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementBindings()
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard |> Seq.append motion

    /// Returns the set of commands which move the caret as a scroll operation
    member x.CreateScrollCommands () =
        seq {
            yield ("z<Enter>", CommandFlags.Movement, NormalCommand.ScrollCaretLineToTop false)
            yield ("zt", CommandFlags.Movement, NormalCommand.ScrollCaretLineToTop true)
            yield ("z.", CommandFlags.Movement, NormalCommand.ScrollCaretLineToMiddle false)
            yield ("zz", CommandFlags.Movement, NormalCommand.ScrollCaretLineToMiddle true)
            yield ("z-", CommandFlags.Movement, NormalCommand.ScrollCaretLineToBottom false)
            yield ("zb", CommandFlags.Movement, NormalCommand.ScrollCaretLineToBottom true)
            yield ("<C-b>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<C-d>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Down, true))
            yield ("<C-e>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Down, false))
            yield ("<C-f>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
            yield ("<C-u>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Up, true))
            yield ("<C-y>", CommandFlags.Movement, NormalCommand.ScrollLines (ScrollDirection.Up, false))
            yield ("<S-Down>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
            yield ("<S-Up>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<PageUp>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Up)
            yield ("<PageDown>", CommandFlags.Movement, NormalCommand.ScrollPages ScrollDirection.Down)
        } |> Seq.map (fun (str, flags, command) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
            CommandBinding.NormalBinding (keyInputSet, flags, command))

