#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type IOperations = 
    inherit ICommonOperations
    
    /// The ISelectionTracker assaciotade with this instance
    abstract SelectionTracker : ISelectionTracker 

    /// Delete the selection and put the result into the specified register
    abstract DeleteSelection : Register -> ITextSnapshot

    /// Joins the selected lines
    abstract JoinSelection : JoinKind -> bool 

