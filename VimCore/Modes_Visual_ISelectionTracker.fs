#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text

type ISelectionTracker = 
    abstract IsRunning : bool 
    abstract SelectedText : string
    abstract Start: unit -> unit
    abstract Stop: unit -> unit 
