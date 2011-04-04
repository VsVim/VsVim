#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor


type internal CommandFactory
    ( 
        _operations : ICommonOperations, 
        _capture : IMotionCapture,
        _motionUtil : ITextViewMotionUtil, 
        _jumpList : IJumpList,
        _settings : IVimLocalSettings ) =

    let _textView = _operations.TextView

    /// Create the movement command bindings which are common to both Normal and 
    /// Visual mode
    member x.CreateStandardMovementBindings () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield ("h", NormalCommand.MoveCaretTo Direction.Left)
            yield ("<Left>", NormalCommand.MoveCaretTo Direction.Left)
            yield ("<Bs>", NormalCommand.MoveCaretTo Direction.Left)
            yield ("<C-h>", NormalCommand.MoveCaretTo Direction.Left)
            yield ("l", NormalCommand.MoveCaretTo Direction.Right)
            yield ("<Right>", NormalCommand.MoveCaretTo Direction.Right)
            yield ("<Space>", NormalCommand.MoveCaretTo Direction.Right)
            yield ("k", NormalCommand.MoveCaretTo Direction.Up)
            yield ("<Up>", NormalCommand.MoveCaretTo Direction.Up)
            yield ("<C-p>", NormalCommand.MoveCaretTo Direction.Up)
            yield ("j", NormalCommand.MoveCaretTo Direction.Down)
            yield ("<Down>", NormalCommand.MoveCaretTo Direction.Down)
            yield ("<C-n>", NormalCommand.MoveCaretTo Direction.Down)
            yield ("<C-j>", NormalCommand.MoveCaretTo Direction.Down)
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

