#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type internal MovementCommand = int -> unit

type internal CommandFactory( _operations : ICommonOperations) = 

    member private x.CreateMotionCommands() =
        let s : seq<KeyInput * MovementCommand> =
            seq {
                yield (InputUtil.CharToKeyInput('w'), (fun count -> _operations.MoveWordForward WordKind.NormalWord count))
                yield (InputUtil.CharToKeyInput('W'), (fun count -> _operations.MoveWordForward WordKind.BigWord count))
                yield (InputUtil.CharToKeyInput('b'), (fun count -> _operations.MoveWordBackward WordKind.NormalWord count))
                yield (InputUtil.CharToKeyInput('B'), (fun count -> _operations.MoveWordBackward WordKind.BigWord count))
            }
        s

    member private x.CreateSimpleMovementCommands() = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        let s : seq<KeyInput * MovementCommand> = 
            seq {
                yield (InputUtil.CharToKeyInput('h'), moveLeft)
                yield (InputUtil.WellKnownKeyToKeyInput LeftKey, moveLeft)
                yield (InputUtil.WellKnownKeyToKeyInput BackKey, moveLeft)
                yield (KeyInput('h', Key.H, ModifierKeys.Control), moveLeft)
                yield (InputUtil.CharToKeyInput('l'), moveRight)
                yield (InputUtil.WellKnownKeyToKeyInput RightKey, moveRight)
                yield (InputUtil.CharToKeyInput ' ', moveRight)
                yield (InputUtil.CharToKeyInput('k'), moveUp)
                yield (InputUtil.WellKnownKeyToKeyInput UpKey, moveUp)
                yield (KeyInput('p', Key.P, ModifierKeys.Control), moveUp)
                yield (InputUtil.CharToKeyInput('j'), moveDown)
                yield (InputUtil.WellKnownKeyToKeyInput DownKey, moveDown)
                yield (KeyInput('n', Key.N, ModifierKeys.Control),moveDown)
                yield (KeyInput('j', Key.J, ModifierKeys.Control),moveDown)        
                yield (InputUtil.CharToKeyInput('$'), (fun _ -> _operations.EditorOperations.MoveToEndOfLine(false)))
                yield (InputUtil.CharToKeyInput('^'), (fun _ -> _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false)))
            }
        s

    /// The sequence of commands which move the cursor.  Applicable in both Normal and Visual Mode
    member x.CreateMovementCommands() = 
        x.CreateSimpleMovementCommands()
            |> Seq.append (x.CreateMotionCommands())
