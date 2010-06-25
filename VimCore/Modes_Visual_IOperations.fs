#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type IOperations = 
    inherit ICommonOperations

    /// Returns the span of the first selection.  If no selection is present, returns an empty span
    /// at the caret position
    abstract SelectedSpan : SnapshotSpan 
    
    /// Delete the selection and put the result into the specified register
    abstract DeleteSelection : Register -> unit

    /// Delete the selected lines
    abstract DeleteSelectedLines : Register -> ITextSnapshot

    /// Joins the selected lines
    abstract JoinSelection : JoinKind -> bool 

    /// Paste the specified text over the selection and put the previous text into the specified 
    /// register
    abstract PasteOverSelection : string -> Register -> unit

