#light

namespace Vim

type SearchKind = 
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
    let IsWrap x = 
        match x with 
        | SearchKind.BackwardWithWrap -> true
        | SearchKind.ForwardWithWrap -> true
        | _ -> false

    /// Reverse the direction of the given SearchKind
    let Reverse x =
        match x with
        | SearchKind.Forward -> SearchKind.Backward
        | SearchKind.ForwardWithWrap -> SearchKind.BackwardWithWrap
        | SearchKind.Backward -> SearchKind.Forward
        | SearchKind.BackwardWithWrap -> SearchKind.ForwardWithWrap
        | _ -> failwith "Invalid enum value"

    /// Remove any wrap which map be associated with this
    let RemoveWrap x =
        match x with
        | SearchKind.Forward -> SearchKind.Forward
        | SearchKind.ForwardWithWrap -> SearchKind.Forward
        | SearchKind.Backward -> SearchKind.Backward
        | SearchKind.BackwardWithWrap -> SearchKind.Backward
        | _ -> failwith "Invalid enum value"
