#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

type IIncrementalSearch = 
    abstract InSearch : bool
    abstract CurrentSearch : SearchData option
    abstract LastSearch : SearchData with get, set

    /// Processes the next piece of input.  Returns true when the incremental search operation is complete
    abstract Process : KeyInput -> bool

    /// Called when a search is about to begin
    abstract Begin : SearchKind -> unit

    /// Find the next match of the LastSearch
    abstract FindNextMatch : count:int -> unit

    [<CLIEvent>]
    abstract CurrentSearchSpanChanged : IEvent<SnapshotSpan option>


