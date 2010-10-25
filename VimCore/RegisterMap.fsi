
#light

namespace Vim

type internal RegisterMap = 
    new : IClipboardDevice * (unit -> string option) -> RegisterMap

    interface IRegisterMap
