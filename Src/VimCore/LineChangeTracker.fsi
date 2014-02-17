#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Used to track changes to the current line of an individual IVimBuffer
[<Class>]
type internal LineChangeTracker =
    interface ILineChangeTracker

