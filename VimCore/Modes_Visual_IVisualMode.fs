#light

namespace Vim.Modes.Visual
open Vim
open Vim.Modes

/// Interface for members specific to IVisualMode
type IVisualMode = 
    interface IMode 
    abstract Operations : IOperations
    abstract InExplicitMove : bool

