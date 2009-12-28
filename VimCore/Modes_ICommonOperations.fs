#light

namespace Vim.Modes
open Vim
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

    /// Move the caret count spaces left on the same line
    abstract MoveCaretLeft : count : int -> unit

    /// Move the cursor countt spaces right on the same line
    abstract MoveCaretRight : count : int -> unit

    /// Move the cursor up count lines
    abstract MoveCaretUp : count : int -> unit

    /// Move the cursor down count lines
    abstract MoveCaretDown : count : int -> unit
    
    abstract JumpToMark : char -> MarkMap -> Result
    abstract SetMark : char -> MarkMap -> Result
