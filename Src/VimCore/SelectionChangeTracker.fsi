#light

namespace Vim

type internal SelectionChangeTracker =
    new: vimBuffer: IVimBuffer * commonOperations: ICommonOperations * visualModeSelectionOverrideList: IVisualModeSelectionOverride list * mouseDevice: IMouseDevice -> SelectionChangeTracker
