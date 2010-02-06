#light

namespace Vim

type internal JumpList () =  

    interface IJumpList with
        member x.Count = 0
        member x.PreviousJump = None
        member x.NextJump = None
        member x.AllJumps = Seq.empty

