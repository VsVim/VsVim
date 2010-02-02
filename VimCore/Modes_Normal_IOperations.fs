#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    abstract ReplaceChar : KeyInput -> count:int -> bool
    abstract YankLines : count:int -> Register -> unit
    abstract DeleteCharacterAtCursor : count:int -> Register -> unit 
    abstract DeleteCharacterBeforeCursor : count:int -> Register -> unit
    abstract PasteAfterCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd: bool -> unit
    abstract PasteBeforeCursor : text:string -> count:int -> moveCursorToEnd:bool -> unit

    /// Adds an empty line to the buffer below the cursor and returns the resulting ITextSnapshotLine
    abstract InsertLineBelow : unit -> ITextSnapshotLine

    /// Insert a line above the current cursor position and returns the resulting ITextSnapshotLine
    abstract InsertLineAbove : unit -> ITextSnapshotLine

    /// Join the lines at the current caret point
    abstract JoinAtCaret : count:int -> unit

    /// Go to the definition of the word under the cursor
    abstract GoToDefinitionWrapper : unit -> unit

    /// Scroll the buffer by count lines in the given direction
    abstract Scroll : ScrollDirection -> count:int -> unit

    interface Modes.ICommonOperations

