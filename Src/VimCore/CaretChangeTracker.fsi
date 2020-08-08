namespace Vim

type internal CaretChangeTracker =
    new: vimBuffer:IVimBuffer * commonOperations:ICommonOperations * mouseDevice:IMouseDevice -> CaretChangeTracker
