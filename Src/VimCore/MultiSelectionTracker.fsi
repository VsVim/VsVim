#light

namespace Vim

type internal MultiSelectionTracker =
    new: vimHost: IVimBuffer * commonOperations: ICommonOperations * mouseDevice: IMouseDevice -> MultiSelectionTracker
