#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    abstract ReplaceChar : KeyInput -> count:int -> bool

    /// Delete count characters starting at the cursor.  This will not delete past the 
    /// end of the current line
    abstract DeleteCharacterAtCursor : count:int -> SnapshotSpan

    abstract DeleteCharacterBeforeCursor : count:int -> SnapshotSpan
    abstract PasteAfterCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd: bool -> unit
    abstract PasteBeforeCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd:bool -> unit

    /// Adds an empty line to the buffer below the cursor and returns the resulting ITextSnapshotLine
    abstract InsertLineBelow : unit -> ITextSnapshotLine

    /// Insert a line above the current cursor position and returns the resulting ITextSnapshotLine
    abstract InsertLineAbove : unit -> ITextSnapshotLine

    /// Go to the definition of the word under the cursor
    abstract GoToDefinitionWrapper : unit -> unit

    /// Go to the specified line number or the first line if no value is specified
    abstract GoToLineOrFirst : int option -> unit

    /// GoTo the specified line number or the last line if no value is specified
    abstract GoToLineOrLast : int option -> unit

    /// Move the caret for an append operation
    abstract MoveCaretForAppend : unit -> unit

    /// Jump to the next item in the jump list
    abstract JumpNext : count:int -> unit

    /// Jump to the previous item in the jump list
    abstract JumpPrevious : count:int -> unit

    /// Change the case of the character at cursor
    abstract ChangeLetterCaseAtCursor : count:int -> unit 

    inherit Modes.ICommonOperations

