#light

namespace Vim

type internal KeyMap() =

    interface IKeyMap with
        member x.GetKeyMapping _= None
        member x.MapWithNoRemap _ _ _ = false