#light

namespace Vim

type internal JumpList () =  

    interface IJumpList with
        member x.Current = None
        member x.AllJumps = Seq.empty
        member x.MovePrevious() = false
        member x.MoveNext() = false
        member x.Add _ = ()

