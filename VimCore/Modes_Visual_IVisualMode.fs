#light

namespace Vim.Modes.Visual
open Vim
open Vim.Modes

/// Interface for members specific to IVisualMode
type IVisualMode = 
    interface IMode 
    abstract BeginExplicitMove : unit -> unit
    abstract EndExplicitMove : unit -> unit

