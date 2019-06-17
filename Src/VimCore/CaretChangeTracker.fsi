#light

namespace Vim

type internal CaretChangeTracker =
    new: vimBuffer: IVimBuffer * ICommonOperations * IMouseDevice -> CaretChangeTracker
