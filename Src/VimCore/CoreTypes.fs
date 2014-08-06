namespace Vim
open EditorUtils
open Microsoft.VisualStudio.Text
open System.Diagnostics

[<RequireQualifiedAccess>]
[<NoComparison>]
type WordKind = 
    | NormalWord
    | BigWord

/// Modes for a key remapping
[<RequireQualifiedAccess>]
[<DebuggerDisplay("{ToString(),nq}")>]
[<StructuralEquality>]
[<StructuralComparison>]
type KeyRemapMode =
    | None
    | Normal 
    | Visual 
    | Select 
    | OperatorPending 
    | Insert 
    | Command 
    | Language 

    with 

    static member All = 
        seq {
            yield None
            yield Normal
            yield Visual 
            yield Select
            yield OperatorPending
            yield Insert
            yield Command
            yield Language }

    override x.ToString() =
        match x with 
        | None -> "None"
        | Normal -> "Normal"
        | Visual -> "Visual"
        | Select -> "Select"
        | OperatorPending -> "OperatorPending"
        | Insert -> "Insert"
        | Command -> "Command"
        | Language -> "Language"

[<RequireQualifiedAccess>]
type JoinKind = 
    | RemoveEmptySpaces
    | KeepEmptySpaces

/// Flags for the substitute command
[<System.Flags>]
type SubstituteFlags = 
    | None = 0

    /// Replace all occurrences on the line
    | ReplaceAll = 0x1

    /// Ignore case for the search pattern
    | IgnoreCase = 0x2

    /// Report only 
    | ReportOnly = 0x4
    | Confirm = 0x8
    | UsePreviousFlags = 0x10
    | UsePreviousSearchPattern = 0x20
    | SuppressError = 0x40
    | OrdinalCase = 0x80
    | Magic = 0x100
    | Nomagic = 0x200

    /// The p option.  Print the last replaced line
    | PrintLast = 0x400

    /// The # option.  Print the last replaced line with the line number prepended
    | PrintLastWithNumber = 0x800

    /// Print the last line as if :list was used
    | PrintLastWithList = 0x1000

type SubstituteData = {
    SearchPattern : string
    Substitute : string
    Flags : SubstituteFlags
}

/// Represents the different type of operations that are available for Motions
[<RequireQualifiedAccess>]
[<DebuggerDisplay("{ToString(),nq}")>]
[<NoComparison>]
type OperationKind = 
    | CharacterWise 
    | LineWise 

    with

    override x.ToString() =
        match x with 
        | CharacterWise -> "CharacterWise"
        | LineWise -> "LineWise"

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
type NumberMark = 
    | Item0
    | Item1
    | Item2
    | Item3
    | Item4
    | Item5
    | Item6
    | Item7
    | Item8
    | Item9

    with

    member x.Char =
        match x with
        | Item0 -> '0'
        | Item1 -> '1'
        | Item2 -> '2'
        | Item3 -> '3'
        | Item4 -> '4'
        | Item5 -> '5'
        | Item6 -> '6'
        | Item7 -> '7'
        | Item8 -> '8'
        | Item9 -> '9'

    static member All = 
        seq { 
            yield Item0
            yield Item1
            yield Item2
            yield Item3
            yield Item4
            yield Item5
            yield Item6
            yield Item7
            yield Item8
            yield Item9
        }

    static member OfChar c =
        match c with
        | '0' -> Some Item0
        | '1' -> Some Item1
        | '2' -> Some Item2
        | '3' -> Some Item3
        | '4' -> Some Item4
        | '5' -> Some Item5
        | '6' -> Some Item6
        | '7' -> Some Item7
        | '8' -> Some Item8
        | '9' -> Some Item9
        | _ -> None

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type LocalMark =
    | Letter of Letter
    | Number of NumberMark
    | LastInsertExit
    | LastSelectionStart
    | LastSelectionEnd
    | LastEdit

    with

    member x.Char =
        match x with 
        | Letter letter -> letter.Char
        | Number number -> number.Char
        | LastSelectionStart -> '<'
        | LastSelectionEnd -> '>'
        | LastInsertExit -> '^'
        | LastEdit -> '.'

    static member All =
        seq {
            for letter in Letter.All do
                yield LocalMark.Letter letter
            for number in NumberMark.All do
                yield LocalMark.Number number
            yield LocalMark.LastSelectionStart
            yield LocalMark.LastSelectionEnd
            yield LocalMark.LastEdit
        }

    static member OfChar c =
        match Letter.OfChar c with
        | Some letter -> Some (LocalMark.Letter letter)
        | None ->
            match NumberMark.OfChar c with
            | Some number -> Some (LocalMark.Number number)
            | None -> 
                match c with 
                | '<' -> Some LocalMark.LastSelectionStart
                | '>' -> Some LocalMark.LastSelectionEnd
                | '^' -> Some LocalMark.LastInsertExit
                | '.' -> Some LocalMark.LastEdit
                | _ -> None

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Mark =

    /// Marks which are local to the IVimTextBuffer
    | LocalMark of LocalMark

    /// Marks which are global to vim
    | GlobalMark of Letter

    /// The last jump which is specific to a window
    | LastJump 

    with

    member x.Char =
        match x with 
        | LocalMark localMark -> localMark.Char
        | GlobalMark letter -> CharUtil.ToUpper letter.Char
        | LastJump -> '\''

    static member OfChar c =
        if CharUtil.IsUpper c then 
            c |> CharUtil.ToLower |> Letter.OfChar |> Option.map GlobalMark
        elif c = '\'' || c = '`' then
            Some LastJump
        else
            LocalMark.OfChar c |> Option.map LocalMark
    
type Direction =
    | Up        = 1
    | Down      = 2
    | Left      = 3
    | Right     = 4

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Path =
    | Forward
    | Backward

    with

    member x.IsPathForward = 
        match x with
        | Path.Forward -> true
        | Path.Backward -> false

    member x.IsPathBackward = not x.IsPathForward

    static member Reverse path = 
        match path with
        | Path.Forward -> Path.Backward
        | Path.Backward -> Path.Forward

    static member Create isForward = 
        if isForward then Path.Forward 
        else Path.Backward

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
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

