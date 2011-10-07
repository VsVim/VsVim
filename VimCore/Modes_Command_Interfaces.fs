#light

namespace Vim.Modes.Command
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type ICommandProcessor =

    /// Run the specified command
    abstract RunCommand : char list -> RunResult

