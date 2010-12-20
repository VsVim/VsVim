#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

[<RequireQualifiedAccess>]
type RegisterOperation = 
    | Delete
    | Yank

type OperationsData = {
    EditorOperations : IEditorOperations
    EditorOptions : IEditorOptions
    FoldManager : IFoldManager
    JumpList : IJumpList
    KeyMap : IKeyMap
    LocalSettings : IVimLocalSettings
    OutliningManager : IOutliningManager
    RegisterMap : IRegisterMap 
    StatusUtil : IStatusUtil
    SearchService : ISearchService
    TextView : ITextView
    UndoRedoOperations : IUndoRedoOperations
    VimData : IVimData
    VimHost : IVimHost
    Navigator : ITextStructureNavigator
}

type JoinKind = 
| RemoveEmptySpaces
| KeepEmptySpaces

type Result = 
| Succeeded
| Failed of string

[<RequireQualifiedAccess>]
type PutKind =
| Before
| After

/// Common operations
type ICommonOperations =

    /// Associated ITextView
    abstract TextView : ITextView 

    /// Associated IEditorOperations
    abstract EditorOperations : IEditorOperations

    /// Associated IFoldManager
    abstract FoldManager : IFoldManager

    /// Associated IUndoRedoOperations
    abstract UndoRedoOperations : IUndoRedoOperations

    /// Run the beep operation
    abstract Beep : unit -> unit

    /// Change the case of all letters appearing in the given span
    abstract ChangeLetterCase : SnapshotSpan -> unit

    /// Change the text represented by the given Motion.  Returns the SnapshotSpan 
    /// of the original ITextSnapshot which was modified.  Maybe different
    /// than the passed in value
    abstract ChangeSpan : MotionData -> SnapshotSpan

    /// Change the case of all letters appearing in the given span
    abstract ChangeLetterCaseBlock : NormalizedSnapshotSpanCollection -> unit

    /// Close the current file
    abstract Close : checkDirty : bool -> unit

    /// Close all open files
    abstract CloseAll : checkDirty : bool -> unit

    /// Close count foldse in the given SnapshotSpan
    abstract CloseFold : SnapshotSpan -> count:int -> unit

    /// Close all folds which intersect with the given SnapshotSpan
    abstract CloseAllFolds : SnapshotSpan -> unit

    /// Delete one folds at the cursor
    abstract DeleteOneFoldAtCursor : unit -> unit

    /// Delete all folds at the cursor
    abstract DeleteAllFoldsAtCursor : unit -> unit

    /// Delete count lines starting from the cursor line.  The last line will 
    /// not have its break deleted
    abstract DeleteLines : count:int -> SnapshotSpan

    /// Delete from the cursor to the end of the current line and (count-1) more 
    /// lines.  
    abstract DeleteLinesFromCursor : count:int -> SnapshotSpan

    /// Delete count lines from the buffer starting from the cursor line
    abstract DeleteLinesIncludingLineBreak : count:int -> SnapshotSpan

    /// Delete from the cursor to the end of the current line and (count-1) more 
    /// lines.  
    abstract DeleteLinesIncludingLineBreakFromCursor : count:int -> SnapshotSpan

    /// Delete the lines in the given span.  Does not include the final line break
    abstract DeleteLinesInSpan : SnapshotSpan -> SnapshotSpan

    /// Delete a range of text
    abstract DeleteSpan : SnapshotSpan -> unit

    /// Delete a range of text
    abstract DeleteBlock : NormalizedSnapshotSpanCollection -> unit

    /// Ensure the caret is on the visible screen
    abstract EnsureCaretOnScreen : unit -> unit

    /// Ensure the caret is on screen and that it is not in a collapsed region
    abstract EnsureCaretOnScreenAndTextExpanded : unit -> unit

    /// Fold count lines under the cursor
    abstract FoldLines : count:int -> unit

    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    abstract GoToDefinition : unit -> Result

    /// Go to the file named in the word under the cursor
    abstract GoToFile : unit -> unit

    /// Go to the local declaration of the word under the cursor
    abstract GoToLocalDeclaration : unit -> unit

    /// Go to the global declaration of the word under the cursor
    abstract GoToGlobalDeclaration : unit -> unit

    /// Go to the matching construct of the value under the cursor
    abstract GoToMatch : unit -> bool

    /// Go to the next "count" tab
    abstract GoToNextTab : count : int -> unit

    /// Go to the previous "count" tab
    abstract GoToPreviousTab : count : int -> unit

    /// Insert the specified text at the cursor position "count" times
    abstract InsertText : text:string -> count : int -> unit

    /// Insert the data at the specified point with the specified OperationKind
    abstract InsertTextAt : SnapshotPoint -> text:string-> OperationKind -> unit

    /// Insert the data at the specified point with the specified OperationKind and return
    /// the SnapshotSpan of the inserted text
    abstract InsertTextAtWithReturn : SnapshotPoint -> text:string-> OperationKind -> SnapshotSpan

    /// Insert a line above the current cursor position and returns the resulting ITextSnapshotLine
    abstract InsertLineAbove : unit -> ITextSnapshotLine

    /// Adds an empty line to the buffer below the cursor and returns the resulting ITextSnapshotLine
    abstract InsertLineBelow : unit -> ITextSnapshotLine

    /// Joins the lines in the range
    abstract Join : SnapshotLineRange -> JoinKind -> unit

    /// Jumps to a given mark in the buffer.  
    abstract JumpToMark : char -> IMarkMap -> Result

    /// Make the letters on the given span lower case
    abstract MakeLettersLowercase : SnapshotSpan -> unit

    /// Make the letters on the given span upper case
    abstract MakeLettersUppercase : SnapshotSpan -> unit

    /// Move the caret to a given point on the screen
    abstract MoveCaretToPoint : SnapshotPoint -> unit

    /// Move the caret to the MotionData value
    abstract MoveCaretToMotionData : MotionData -> unit

    /// Move the caret count spaces left on the same line
    abstract MoveCaretLeft : count : int -> unit

    /// Move the cursor count spaces right on the same line
    abstract MoveCaretRight : count : int -> unit

    /// Move the cursor up count lines
    abstract MoveCaretUp : count : int -> unit

    /// Move the cursor down count lines
    abstract MoveCaretDown : count : int -> unit

    /// Maybe adjust the caret to respect the virtual edit setting
    abstract MoveCaretForVirtualEdit : unit -> unit

    /// Move the caret the number of lines in the given direction and scroll the view
    abstract MoveCaretAndScrollLines : ScrollDirection -> count:int -> unit

    /// Move to the next "count" occurrence of the last search
    abstract MoveToNextOccuranceOfLastSearch : count:int -> isReverse:bool -> unit

    /// Move to the next occurrence of the word under the cursor
    abstract MoveToNextOccuranceOfWordAtCursor : SearchKind -> count:int -> unit

    /// Move to the next occurrence of the word under the cursor
    abstract MoveToNextOccuranceOfPartialWordAtCursor : SearchKind -> count:int -> unit

    /// Move the cursor backward count WordKind's
    abstract MoveWordBackward : WordKind -> count : int -> unit

    /// Move the cursor forward count WordKind's 
    abstract MoveWordForward : WordKind -> count : int -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Open count folds in the given SnapshotSpan 
    abstract OpenFold : SnapshotSpan -> count:int -> unit

    /// Open all folds which inersect with the given SnapshotSpan
    abstract OpenAllFolds : SnapshotSpan -> unit

    /// Paste after the passed in position.  Don't forget that a linewise paste
    /// operation needs to occur under the cursor.  Returns the SnapshotSpan of
    /// the text on the new snapshot
    abstract PasteAfter : SnapshotPoint -> text : string -> OperationKind -> SnapshotSpan

    abstract PasteAfterCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd: bool -> unit

    /// Paste the text before the passed in position.  Returns the SnapshotSpan for the text in
    /// the new snapshot of the buffer
    abstract PasteBefore : SnapshotPoint -> text : string -> OperationKind -> SnapshotSpan 

    abstract PasteBeforeCursor : text:string -> count:int -> opKind:OperationKind -> moveCursorToEnd:bool -> unit

    /// Paste over the selected text
    abstract PasteOver : SnapshotSpan -> Register -> unit

    /// Put the specified StringData at the given point 
    abstract PutAt : SnapshotPoint -> StringData -> OperationKind -> unit

    /// Put the specified StringData at the caret 
    abstract PutAtCaret : StringData -> OperationKind -> PutKind -> unit

    /// Put the specified StringData at the given point 
    abstract PutAtWithReturn : SnapshotPoint -> StringData -> OperationKind -> SnapshotSpan

    /// Redo the buffer changes "count" times
    abstract Redo : count:int -> unit

    /// Save the current document
    abstract Save : unit -> bool 

    /// Save the current document as the specified file
    abstract SaveAs : string -> bool

    /// Save all files
    abstract SaveAll : unit -> bool

    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    abstract SetMark : IVimBuffer -> SnapshotPoint -> char -> Result

    /// Scrolls the number of lines given and keeps the caret in the view
    abstract ScrollLines : ScrollDirection -> count:int -> unit

    /// Scroll the buffer by the specified number of pages in the given direction
    abstract ScrollPages : ScrollDirection -> count:int -> unit

    /// Shift the count lines starting at the cursor right by the "ShiftWidth" setting
    abstract ShiftLinesRight : count:int -> unit

    /// Shift the count lines starting at the cursor left by the "ShiftWidth" setting
    abstract ShiftLinesLeft :  count:int -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting multiplied
    /// by the multiplier
    abstract ShiftLineRangeRight : multiplier:int -> SnapshotLineRange -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting multiplied
    /// by the multiplier
    abstract ShiftBlockRight : multiplier:int -> NormalizedSnapshotSpanCollection -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting
    abstract ShiftLineRangeLeft : multiplier:int -> SnapshotLineRange -> unit

    /// Shift the lines in the span to the right by the "ShiftWidth" setting
    abstract ShiftBlockLeft : multiplier:int -> NormalizedSnapshotSpanCollection -> unit

    /// Substitute Command implementation
    abstract Substitute : pattern : string -> replace : string -> SnapshotLineRange -> SubstituteFlags -> unit

    /// Undo the buffer changes "count" times
    abstract Undo : count:int -> unit

    /// Update the register for the given register operation
    abstract UpdateRegisterForSpan : Register -> RegisterOperation -> SnapshotSpan -> OperationKind -> unit

    /// Update the register for the given register operation
    abstract UpdateRegisterForCollection : Register -> RegisterOperation -> NormalizedSnapshotSpanCollection -> OperationKind -> unit

    /// Wrap the edit code in an undo transaction.  Ensures the caret is reset during an undo
    abstract WrapEditInUndoTransaction : name:string -> editAction:(unit -> unit) -> unit

    /// Wrap the edit code in an undo transaction.  Ensures the caret is reset during an undo
    abstract WrapEditInUndoTransactionWithReturn : name:string -> editAction:(unit -> 'a) -> 'a

