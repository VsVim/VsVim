#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Flags for the substitute command
type SubstituteFlags = 
    | None = 0
    /// Replace all occurances on the line
    | ReplaceAll = 0x1
    /// Ignore case for the search pattern
    | IgnoreCase = 0x2
    /// Report only 
    | ReportOnly = 0x4


/// Normal mode operations
type IOperations =
    /// Handle the :edit command
    abstract EditFile : fileName : string -> unit

    /// Put the text into the file 
    abstract Put : text : string -> ITextSnapshotLine -> isAfter : bool -> unit

    /// Substitute Command implementation
    abstract Substitute : pattern : string -> replace : string -> SnapshotSpan -> SubstituteFlags -> unit

    interface Modes.ICommonOperations


