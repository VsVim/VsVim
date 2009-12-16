#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    abstract TextView : ITextView
    abstract Mark : NormalModeData -> NormalModeResult
    abstract JumpToMark : char -> MarkMap -> Modes.Result
    abstract InsertLineAbove : unit -> unit
    abstract ReplaceChar : KeyInput -> int -> bool
    abstract YankLines : int -> Register -> unit
    abstract DeleteCharacterAtCursor : int -> Register -> unit 
    abstract DeleteCharacterBeforeCursor : int -> Register -> unit
    abstract PasteAfter : string -> int -> OperationKind -> bool -> unit
    abstract PasteBefore : string -> int -> bool -> unit