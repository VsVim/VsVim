#light

namespace Vim

type internal SelectionChangeTracker =
    new: vimBuffer: IVimBuffer * commonOperations: ICommonOperations * selectionOverrideList: IVisualModeSelectionOverride list * mouseDevice: IMouseDevice -> SelectionChangeTracker
