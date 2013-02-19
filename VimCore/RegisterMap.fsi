
#light

namespace Vim

type internal RegisterMap = 
    new : IVimData * IClipboardDevice * (unit -> string option) -> RegisterMap

    interface IRegisterMap
