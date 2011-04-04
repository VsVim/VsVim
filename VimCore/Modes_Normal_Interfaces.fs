#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    /// Go to the definition of the word under the cursor
    abstract GoToDefinitionWrapper : unit -> unit

    /// Go to the specified line number or the first line if no value is specified
    abstract GoToLineOrFirst : int option -> unit

    /// GoTo the specified line number or the last line if no value is specified
    abstract GoToLineOrLast : int option -> unit

    inherit Modes.ICommonOperations

