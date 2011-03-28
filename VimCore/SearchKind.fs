#light

namespace Vim

type Direction =
    | Up
    | Down
    | Left
    | Right

type Path = 
    | Forward
    | Backward

    with

    static member Reverse path = 
        match path with
        | Path.Forward -> Path.Backward
        | Path.Backward -> Path.Forward

type SearchKind = 
     | Forward
     | ForwardWithWrap
     | Backward
     | BackwardWithWrap

    with

    member x.IsAnyForward =
        match x with 
            | SearchKind.Forward -> true
            | SearchKind.ForwardWithWrap -> true
            | _ -> false

    member x.IsAnyBackward = not (x.IsAnyForward)

    member x.IsWrap =
        match x with 
        | SearchKind.BackwardWithWrap -> true
        | SearchKind.ForwardWithWrap -> true
        | _ -> false

    /// Get the Path value for this SearchKind
    member x.Path = 
        match x with 
        | SearchKind.Forward -> Path.Forward
        | SearchKind.ForwardWithWrap -> Path.Forward
        | SearchKind.Backward -> Path.Backward
        | SearchKind.BackwardWithWrap -> Path.Backward

    /// Reverse the direction of the given SearchKind
    static member Reverse x =
        match x with
        | SearchKind.Forward -> SearchKind.Backward
        | SearchKind.ForwardWithWrap -> SearchKind.BackwardWithWrap
        | SearchKind.Backward -> SearchKind.Forward
        | SearchKind.BackwardWithWrap -> SearchKind.ForwardWithWrap

    /// Remove any wrap which map be associated with this
    static member RemoveWrap x =
        match x with
        | SearchKind.Forward -> SearchKind.Forward
        | SearchKind.ForwardWithWrap -> SearchKind.Forward
        | SearchKind.Backward -> SearchKind.Backward
        | SearchKind.BackwardWithWrap -> SearchKind.Backward

    static member OfPath path = 
        match path with 
        | Path.Forward -> SearchKind.Forward
        | Path.Backward -> SearchKind.Backward

    static member OfPathAndWrap path wrap =
        match path with
        | Path.Forward -> if wrap then SearchKind.ForwardWithWrap else SearchKind.Forward
        | Path.Backward -> if wrap then SearchKind.BackwardWithWrap else SearchKind.Backward

