#light

namespace Vim

type internal SearchKind = 
     | Forward = 1
     | ForwardWithWrap = 2
     | Backward = 3
     | BackwardWithWrap = 4 

module internal SearchKindUtil =
    let IsForward x =
        match x with 
            | SearchKind.Forward -> true
            | SearchKind.ForwardWithWrap -> true
            | _ -> false
    let IsBackward x = not (IsForward x)
    
