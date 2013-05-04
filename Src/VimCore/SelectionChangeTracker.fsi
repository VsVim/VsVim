#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SelectionChangeTracker =
    new : vimBuffer : IVimBuffer * visualModeSelectionOverrideList : IVisualModeSelectionOverride list * mouseDevice : IMouseDevice -> SelectionChangeTracker

