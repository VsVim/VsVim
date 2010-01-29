#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text

type ISelectionTracker = 

    /// Is the selection currently being tracked
    abstract IsRunning : bool 

    /// Whether or not the next call to Start should update the Selection.  When
    /// false it will accept the selection instead of resetting it
    //abstract UpdateSelectionOnNextStart : bool with get,set

    abstract SelectedText : string
    abstract SelectedLines : SnapshotSpan
    abstract Start: unit -> unit
    abstract Stop: unit -> unit 
