#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    abstract InsertLineAbove : unit -> unit
    abstract ReplaceChar : KeyInput -> count:int -> bool
    abstract YankLines : count:int -> Register -> unit
    abstract DeleteCharacterAtCursor : count:int -> Register -> unit 
    abstract DeleteCharacterBeforeCursor : count:int -> Register -> unit
    abstract PasteAfterCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd: bool -> unit
    abstract PasteBeforeCursor : text:string -> count:int -> moveCursorToEnd:bool -> unit
    interface Modes.ICommonOperations