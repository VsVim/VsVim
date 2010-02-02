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

    /// Delete count lines starting at the cursor and place them into the specified
    /// register
    abstract DeleteLines : count:int -> Register -> unit

    /// Delete from the cursor to the end of the current line and (count-1) more 
    /// lines.  
    abstract DeleteLinesFromCursor : count:int -> Register -> unit

    /// Delete count characters starting at the cursor.  This will not delete past the 
    /// end of the current line
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

    interface Modes.ICommonOperations

