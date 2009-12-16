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
    abstract JumpToMark : char -> MarkMap -> Result
    abstract SetMark : char -> MarkMap -> Result
