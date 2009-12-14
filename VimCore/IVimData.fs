#light

namespace Vim

/// Information which is global to all <seealso cref="IVimBuffer" /> instances
type IVimData =

    abstract MarkMap : MarkMap
    abstract RegisterMap : IRegisterMap
    abstract Settings : VimSettings

