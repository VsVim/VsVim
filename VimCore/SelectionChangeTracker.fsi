#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SelectionChangeTracker =
    new : IVimBuffer * IVisualModeSelectionOverride list -> SelectionChangeTracker

