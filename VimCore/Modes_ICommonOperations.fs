#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type JoinKind = 
    | RemoveEmptySpaces
    | KeepEmptySpaces

type Result = 
    | Succeeded
    | Failed of string

/// Common operations
type ICommonOperations =
    abstract TextView : ITextView 

    /// Implements the Join command.  Returns false in the case the join command cannot
    /// be complete (such as joining at the end of the buffer)
    abstract Join : SnapshotPoint -> JoinKind -> count : int -> bool

    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    abstract GoToDefinition : IVimHost -> Result

    /// Move the caret count spaces left on the same line
    abstract MoveCaretLeft : count : int -> unit

    /// Move the cursor countt spaces right on the same line
    abstract MoveCaretRight : count : int -> unit

    /// Move the cursor up count lines
    abstract MoveCaretUp : count : int -> unit

    /// Move the cursor down count lines
    abstract MoveCaretDown : count : int -> unit
    
    /// Jumps to a given mark in the buffer.  
    /// TODO: Support global marks.  
    abstract JumpToMark : char -> MarkMap -> Result

    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    abstract SetMark : char -> MarkMap -> SnapshotPoint -> Result

    /// Yank the span into the given register
    abstract Yank : SnapshotSpan -> MotionKind -> OperationKind -> Register -> unit

    /// Paste after the passed in position.  Don't forget that a linewise paste
    /// operation needs to occur under the cursor.  Returns the SnapshotSpan of
    /// the text on the new snapshot
    abstract PasteAfter : SnapshotPoint -> text : string -> OperationKind -> SnapshotSpan

    /// Paste the text before the passed in position.  Returns the SnapshotSpan for the text in
    /// the new snapshot of the buffer
    abstract PasteBefore : SnapshotPoint -> text : string -> SnapshotSpan 

    /// Delete a range of text
    abstract DeleteSpan : SnapshotSpan -> MotionKind -> OperationKind -> Register -> ITextSnapshot

    /// Shift the lines in the span count spaces to the right
    abstract ShiftRight : SnapshotSpan -> count : int -> ITextSnapshot

    /// Shift the lines in the span count spaces to the left
    abstract ShiftLeft : SnapshotSpan -> count : int -> ITextSnapshot
