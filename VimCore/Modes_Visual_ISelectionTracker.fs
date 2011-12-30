#light

namespace Vim.Modes.Visual
open Vim
open Microsoft.VisualStudio.Text

/// Responsible for updating the selection as appropriate for a given Visual
/// mode after a movement command is executed
type ISelectionTracker = 

    /// Visual Kind this ISelectionTracker is tracking
    abstract VisualKind : VisualKind

    /// Is the selection currently being tracked
    abstract IsRunning : bool 

    /// Update the selection based on the current position of the caret or the active
    /// incremental search
    abstract UpdateSelection : unit -> unit

    /// Start tracking the selection
    abstract Start : unit -> unit

    /// Reset the selectiond and stop tracking
    abstract Stop : unit -> unit

