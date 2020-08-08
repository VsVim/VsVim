namespace Vim


type internal CaretRegisterMap =
    new: registerMap:IRegisterMap -> CaretRegisterMap

    interface ICaretRegisterMap
