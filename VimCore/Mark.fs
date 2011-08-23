#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<StructuralComparison>]
type Letter =
    | A
    | B
    | C
    | D
    | E
    | F
    | G
    | H
    | I
    | J
    | K
    | L
    | M
    | N
    | O
    | P
    | Q
    | R
    | S
    | T
    | U
    | V
    | W
    | X
    | Y
    | Z

    with

    member x.Char =
        match x with
        | A -> 'a'
        | B -> 'b'
        | C -> 'c'
        | D -> 'd'
        | E -> 'e'
        | F -> 'f'
        | G -> 'g'
        | H -> 'h'
        | I -> 'i'
        | J -> 'j'
        | K -> 'k'
        | L -> 'l'
        | M -> 'm'
        | N -> 'n'
        | O -> 'o'
        | P -> 'p'
        | Q -> 'q'
        | R -> 'r'
        | S -> 's'
        | T -> 't'
        | U -> 'u'
        | V -> 'v'
        | W -> 'w'
        | X -> 'x'
        | Y -> 'y'
        | Z -> 'z'

    static member All =
        seq {
            yield Letter.A
            yield Letter.B
            yield Letter.C
            yield Letter.D
            yield Letter.E
            yield Letter.F
            yield Letter.G
            yield Letter.H
            yield Letter.I
            yield Letter.J
            yield Letter.K
            yield Letter.L
            yield Letter.M
            yield Letter.N
            yield Letter.O
            yield Letter.P
            yield Letter.Q
            yield Letter.R
            yield Letter.S
            yield Letter.T
            yield Letter.U
            yield Letter.V
            yield Letter.W
            yield Letter.X
            yield Letter.Y
            yield Letter.Z
        }

    static member OfChar c = 
        match c with 
        | 'a' -> Letter.A |> Some
        | 'b' -> Letter.B |> Some
        | 'c' -> Letter.C |> Some
        | 'd' -> Letter.D |> Some
        | 'e' -> Letter.E |> Some
        | 'f' -> Letter.F |> Some
        | 'g' -> Letter.G |> Some
        | 'h' -> Letter.H |> Some
        | 'i' -> Letter.I |> Some
        | 'j' -> Letter.J |> Some
        | 'k' -> Letter.K |> Some
        | 'l' -> Letter.L |> Some
        | 'm' -> Letter.M |> Some
        | 'n' -> Letter.N |> Some
        | 'o' -> Letter.O |> Some
        | 'p' -> Letter.P |> Some
        | 'q' -> Letter.Q |> Some
        | 'r' -> Letter.R |> Some
        | 's' -> Letter.S |> Some
        | 't' -> Letter.T |> Some
        | 'u' -> Letter.U |> Some
        | 'v' -> Letter.V |> Some
        | 'w' -> Letter.W |> Some
        | 'x' -> Letter.X |> Some
        | 'y' -> Letter.Y |> Some
        | 'z' -> Letter.Z |> Some
        | _ -> None

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type LocalMark =
    | Letter of Letter
    | LastSelectionStart
    | LastSelectionEnd

    with

    member x.Char =
        match x with 
        | Letter letter -> letter.Char
        | LastSelectionStart -> '<'
        | LastSelectionEnd -> '>'

    static member All =
        seq {
            for letter in Letter.All do
                yield LocalMark.Letter letter
            yield LocalMark.LastSelectionStart
            yield LocalMark.LastSelectionEnd
        }

    static member OfChar c =
        match Letter.OfChar c with
        | Some letter ->
            Some (LocalMark.Letter letter)
        | None ->
            match c with 
            | '<' -> Some LocalMark.LastSelectionStart
            | '>' -> Some LocalMark.LastSelectionEnd
            | _ -> None

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Mark =
    | LocalMark of LocalMark
    | GlobalMark of Letter

    with

    member x.Char =
        match x with 
        | LocalMark localMark -> localMark.Char
        | GlobalMark letter -> letter.Char

    static member OfChar c =
        if CharUtil.IsUpper c then 
            c |> CharUtil.ToLower |> Letter.OfChar |> Option.map GlobalMark
        else
            LocalMark.OfChar c |> Option.map LocalMark

    