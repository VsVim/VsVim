#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal DefaultOperations (_data : Vim.Modes.OperationsData ) =
    inherit Modes.CommonOperations(_data)

