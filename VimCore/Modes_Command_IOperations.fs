#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Normal mode operations
type IOperations =
    /// Handle the :edit command
    abstract EditFile : fileName : string -> unit

    /// Put the text into the file 
    abstract Put : text : string -> ITextSnapshotLine -> isAfter : bool -> unit

    interface Modes.ICommonOperations


