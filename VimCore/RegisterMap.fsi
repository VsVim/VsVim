
#light

namespace Vim

type internal RegisterMap = 
    new : IClipboardDevice -> RegisterMap

    interface IRegisterMap
