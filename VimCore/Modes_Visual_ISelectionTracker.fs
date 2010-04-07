#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text

type ISelectionTracker = 

    /// Is the selection currently being tracked
    abstract IsRunning : bool 

    abstract SelectedText : string
    abstract SelectedLines : SnapshotSpan
    abstract Start: unit -> unit
    abstract Stop: unit -> unit 
