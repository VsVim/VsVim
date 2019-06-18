#light

namespace Vim

type internal MultiCaretTracker =
    new: vimHost: IVimBuffer * commonOperations: ICommonOperations * mouseDevice: IMouseDevice -> MultiCaretTracker
