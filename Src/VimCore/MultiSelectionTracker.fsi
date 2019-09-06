#light

namespace Vim

type internal MultiSelectionTracker =
    new: vimBuffer: IVimBuffer * commonOperations: ICommonOperations * mouseDevice: IMouseDevice -> MultiSelectionTracker
