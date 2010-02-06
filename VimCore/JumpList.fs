#light

namespace Vim

type internal JumpList () =  

    interface IJumpList with
        member x.Count = 0
        member x.Current = None
        member x.MovePrevious() = false
        member x.MoveNext() = false
        member x.AllJumps = Seq.empty

