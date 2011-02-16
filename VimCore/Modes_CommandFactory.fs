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

    member x.CreateStandardMovementCommandsCore () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield ("h", moveLeft)
            yield ("<Left>", moveLeft)
            yield ("<Bs>", moveLeft)
            yield ("<C-h>", moveLeft)
            yield ("l", moveRight)
            yield ("<Right>", moveRight)
            yield ("<Space>", moveRight)
            yield ("k", moveUp)
            yield ("<Up>", moveUp)
            yield ("<C-p>", moveUp)
            yield ("j", moveDown)
            yield ("<Down>", moveDown)
            yield ("<C-n>", moveDown)
            yield ("<C-j>", moveDown)
            yield ("n", fun count -> _operations.MoveToNextOccuranceOfLastSearch count false)
            yield ("N", fun count -> _operations.MoveToNextOccuranceOfLastSearch count true)
            yield ("*", fun count -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.ForwardWithWrap count)
            yield ("#", fun count -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.BackwardWithWrap count)
            yield ("g*", fun count -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count)
            yield ("g#", fun count -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count)
            yield ("gd", fun _ -> _operations.GoToLocalDeclaration())
            yield ("gD", fun  _ -> _operations.GoToGlobalDeclaration())
        }

    member x.CreateStandardMovementCommands() =
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (notation,func) ->
            let kiSet = KeyNotationUtil.StringToKeyInputSet notation
            let funcWithReg opt reg = 
                func (CommandUtil2.CountOrDefault opt)
                CommandResult.Completed NoSwitch
            CommandBinding.LegacySimpleCommand (kiSet,CommandFlags.Movement, funcWithReg))

    /// Build up a set of LegacyMotionCommand values from applicable Motion values.  These will 
    /// move the cursor to the result of the motion
    member x.CreateMovementsFromMotions() =
        let processMotionBinding binding =
            match binding with
            | MotionBinding.Simple (name, _, motion) -> 

                // Convert the Motion into a NormalCommand which moves the caret for the given Motion
                let command = NormalCommand.MoveCaretToMotion motion
                CommandBinding.NormalCommand(name, CommandFlags.Movement, command) 

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
                CommandBinding.ComplexNormalCommand(name, flags, bindDataStorage)

        _capture.MotionBindings
        |> Seq.filter (fun binding -> Util.IsFlagSet binding.MotionFlags MotionFlags.CursorMovement)
        |> Seq.map processMotionBinding

    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommands()
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard |> Seq.append motion

