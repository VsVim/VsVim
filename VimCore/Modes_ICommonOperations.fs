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

type IStatusUtil =

    /// Raised when there is a special status message that needs to be reported
    abstract OnStatus : string -> unit

    /// Raised when there is a long status message that needs to be reported
    abstract OnStatusLong : string seq -> unit 

    /// Raised when there is an error message that needs to be reported
    abstract OnError : string -> unit 

/// Common operations
type ICommonOperations =

    /// Associated ITextView
    abstract TextView : ITextView 

    /// Associated IEditorOperations
    abstract EditorOperations : IEditorOperations

    /// Implements the Join command.  Returns false in the case the join command cannot
    /// be complete (such as joining at the end of the buffer)
    abstract Join : SnapshotPoint -> JoinKind -> count : int -> bool

    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    abstract GoToDefinition : unit -> Result

    /// Go to the matching construct of the value under the cursor
    abstract GoToMatch : unit -> bool

    /// Go to the next "count" tab
    abstract GoToNextTab : count : int -> unit

    /// Go to the previous "count" tab
    abstract GoToPreviousTab : count : int -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Move the caret count spaces left on the same line
    abstract MoveCaretLeft : count : int -> unit

    /// Move the cursor countt spaces right on the same line
    abstract MoveCaretRight : count : int -> unit

    /// Move the cursor up count lines
    abstract MoveCaretUp : count : int -> unit

    /// Move the cursor down count lines
    abstract MoveCaretDown : count : int -> unit

    /// Move the cursor down count lines to the first non-blank
    abstract MoveCaretDownToFirstNonWhitespaceCharacter : count:int -> unit

    /// Move the cursor forward count WordKind's 
    abstract MoveWordForward : WordKind -> count : int -> unit

    /// Move the cursor backward count WordKind's
    abstract MoveWordBackward : WordKind -> count : int -> unit

    /// Jumps to a given mark in the buffer.  
    abstract JumpToMark : char -> IMarkMap -> Result

    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    abstract SetMark : IVimBuffer -> SnapshotPoint -> char -> Result

    /// Yank the text at the given span into the given register
    abstract Yank : SnapshotSpan -> MotionKind -> OperationKind -> Register -> unit

    /// Yank the text into the given register
    abstract YankText : string -> MotionKind -> OperationKind -> Register -> unit

    /// Paste after the passed in position.  Don't forget that a linewise paste
    /// operation needs to occur under the cursor.  Returns the SnapshotSpan of
    /// the text on the new snapshot
    abstract PasteAfter : SnapshotPoint -> text : string -> OperationKind -> SnapshotSpan

    /// Paste the text before the passed in position.  Returns the SnapshotSpan for the text in
    /// the new snapshot of the buffer
    abstract PasteBefore : SnapshotPoint -> text : string -> OperationKind -> SnapshotSpan 

    /// Insert the specified text at the cursor position "count" times
    abstract InsertText : text:string -> count : int -> ITextSnapshot

    /// Delete count lines starting from the cursor line.  The last line will 
    /// not have its break deleted
    abstract DeleteLines : count:int -> Register -> unit

    /// Delete from the cursor to the end of the current line and (count-1) more 
    /// lines.  
    abstract DeleteLinesFromCursor : count:int -> Register -> unit

    /// Delete count lines from the buffer starting from the cursor line
    abstract DeleteLinesIncludingLineBreak : count:int -> Register -> unit

    /// Delete from the cursor to the end of the current line and (count-1) more 
    /// lines.  
    abstract DeleteLinesIncludingLineBreakFromCursor : count:int -> Register -> unit

    /// Delete a range of text
    abstract DeleteSpan : SnapshotSpan -> MotionKind -> OperationKind -> Register -> ITextSnapshot

    /// Shift the count lines starting at the cursor right by the "ShiftWidth" setting
    abstract ShiftLinesRight : count:int -> unit

    /// Shift the count lines starting at the cursor left by the "ShiftWidth" setting
    abstract ShiftLinesLeft :  count:int -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting
    abstract ShiftSpanRight : SnapshotSpan -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting
    abstract ShiftSpanLeft : SnapshotSpan -> unit

    /// Save the current document
    abstract Save : unit -> unit

    /// Save the current document as the specified file
    abstract SaveAs : string -> unit

    /// Save all files
    abstract SaveAll : unit -> unit

    /// Close the current file
    abstract Close : checkDirty : bool -> unit

    /// Close all open files
    abstract CloseAll : checkDirty : bool -> unit

    /// Scroll the buffer by count lines in the given direction
    abstract ScrollLines : ScrollDirection -> count:int -> unit

    /// Scroll the buffer by the specified number of pages in the given direction
    abstract ScrollPages : ScrollDirection -> count:int -> unit

    /// Change the case of all letters appearing in the given span
    abstract ChangeLetterCase : SnapshotSpan -> unit

    /// Make the letters on the given span lower case
    abstract MakeLettersLowercase : SnapshotSpan -> unit

    /// Make the letters on the given span upper case
    abstract MakeLettersUppercase : SnapshotSpan -> unit