namespace Vim
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

[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString(),nq}")>]
type SelectedSpan =

    val private _caretPoint: VirtualSnapshotPoint
    val private _anchorPoint: VirtualSnapshotPoint
    val private _activePoint: VirtualSnapshotPoint

    new (caretPoint: VirtualSnapshotPoint) =
        {
            _caretPoint = caretPoint
            _anchorPoint = caretPoint
            _activePoint = caretPoint
        }

    new (caretPoint: VirtualSnapshotPoint, anchorPoint: VirtualSnapshotPoint, activePoint: VirtualSnapshotPoint) =
        {
            _caretPoint = caretPoint
            _anchorPoint = anchorPoint
            _activePoint = activePoint
        }

    member x.CaretPoint = x._caretPoint
    member x.AnchorPoint = x._anchorPoint
    member x.ActivePoint = x._activePoint
    member x.IsReversed = x._anchorPoint.Position.Position > x._activePoint.Position.Position
    member x.Span =
        if not x.IsReversed then
            VirtualSnapshotSpan(x._anchorPoint, x._activePoint)
        else
            VirtualSnapshotSpan(x._activePoint, x._anchorPoint)
    member x.Start = x.Span.Start
    member x.End = x.Span.End
    member x.Length = x.Span.Length
    member x.IsEmpty = x.Length = 0

    static member FromSpan (caretPoint: SnapshotPoint) (span: SnapshotSpan) (isReversed: bool) =
        SelectedSpan.FromVirtualSpan (VirtualSnapshotPoint(caretPoint)) (VirtualSnapshotSpan(span)) isReversed

    static member FromVirtualSpan (caretPoint: VirtualSnapshotPoint) (span: VirtualSnapshotSpan) (isReversed: bool) =
        if not isReversed then
            SelectedSpan(caretPoint, span.Start, span.End)
        else
            SelectedSpan(caretPoint, span.End, span.Start)

    override x.ToString() =
        let reversedString =
            if x.IsReversed then " (reversed)" else ""
        let displayString =
            let point = x.CaretPoint.Position
            let span = x.Span.SnapshotSpan
            let text =
                span.GetText()
                |> StringUtil.GetDisplayString
            if span.Contains(point) || span.End = point then
                let offset = point.Position - span.Start.Position
                text.Substring(0, offset) + "|" + text.Substring(offset)
            else
                text
        let displayString =
            if x.ActivePoint = x.End then
                displayString + "*"
            elif x.ActivePoint = x.Start then
                "*" + displayString
            else
                displayString
        System.String.Format("{0}: [{1}-{2}){3} '{4}'",
            x._caretPoint.Position.Position,
            x.Span.Start.Position.Position,
            x.Span.End.Position.Position,
            reversedString,
            displayString)

type NavigationKind =
    | First = 0
    | Last = 1
    | Next = 2
    | Previous = 3

type ListKind =
    | Error = 0
    | Location = 1

/// One-based list item
type ListItem
    (
        _itemNumber: int,
        _listLength: int,
        _message: string
    ) =
    member x.ItemNumber = _itemNumber
    member x.ListLength = _listLength
    member x.Message = _message

/// Flags for the sort command
[<System.Flags>]
type SortFlags = 
    | None = 0

    /// Ignore case [i]
    | IgnoreCase = 0x1

    /// Decimal [n]
    | Decimal = 0x2

    /// Float [f]
    | Float = 0x4

    /// Hexidecimal [h]
    | Hexidecimal = 0x8

    /// Octal [o]
    | Octal = 0x10

    /// Binary [b]
    | Binary = 0x20

    // Unique [u]
    | Unique = 0x40

    /// Match pattern [r]
    | MatchPattern = 0x80

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

    /// Perform a literal replacement (not expanding special characters)
    | LiteralReplacement = 0x2000

/// Flags for the vimgrep command
[<System.Flags>]
type VimGrepFlags = 
    | None = 0

    /// AllMatchesPerFile [g]
    | AllMatchesPerFile = 0x1

    /// NoJumpToFirst [j]
    | NoJumpToFirst = 0x2

type SubstituteData = {
    SearchPattern: string
    Substitute: string
    Flags: SubstituteFlags
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
    | Letter of Letter: Letter
    | Number of NumberMark: NumberMark
    | LastInsertExit
    | LastSelectionStart
    | LastSelectionEnd
    | LastEdit
    | LastChangeOrYankStart
    | LastChangeOrYankEnd

    with

    member x.Char =
        match x with 
        | Letter letter -> letter.Char
        | Number number -> number.Char
        | LastSelectionStart -> '<'
        | LastSelectionEnd -> '>'
        | LastInsertExit -> '^'
        | LastEdit -> '.'
        | LastChangeOrYankStart -> '['
        | LastChangeOrYankEnd -> ']'

    static member All =
        seq {
            for letter in Letter.All do
                yield LocalMark.Letter letter
            for number in NumberMark.All do
                yield LocalMark.Number number
            yield LocalMark.LastInsertExit
            yield LocalMark.LastEdit
            yield LocalMark.LastSelectionStart
            yield LocalMark.LastSelectionEnd
            yield LocalMark.LastChangeOrYankStart
            yield LocalMark.LastChangeOrYankEnd
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
                | '[' -> Some LocalMark.LastChangeOrYankStart
                | ']' -> Some LocalMark.LastChangeOrYankEnd
                | _ -> None

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Mark =

    /// Marks which are local to the IVimTextBuffer
    | LocalMark of LocalMark: LocalMark

    /// Marks which are global to vim
    | GlobalMark of Letter: Letter

    /// The last jump which is specific to a window
    | LastJump 

    // The position when the current buffer was last exited
    | LastExitedPosition

    with

    member x.Char =
        match x with 
        | LocalMark localMark -> localMark.Char
        | GlobalMark letter -> CharUtil.ToUpper letter.Char
        | LastJump -> '\''
        | LastExitedPosition -> '"'

    static member OfChar c =
        if CharUtil.IsUpper c then 
            c |> CharUtil.ToLower |> Letter.OfChar |> Option.map GlobalMark
        elif c = '\'' || c = '`' then
            Some LastJump
        elif c = '"' then
            Some LastExitedPosition
        else
            LocalMark.OfChar c |> Option.map LocalMark

type Direction =
    | Up        = 0
    | Down      = 1
    | Left      = 2
    | Right     = 3

type WindowKind =
    | Up           = 0
    | Down         = 1
    | Left         = 2
    | Right        = 3
    | FarUp        = 4
    | FarDown      = 5
    | FarLeft      = 6
    | FarRight     = 7
    | Next         = 8
    | Previous     = 9
    | Recent       = 10
    | Top          = 11
    | Bottom       = 12

[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type SearchPath =
    | Forward
    | Backward

    with

    member x.IsSearchPathForward = 
        match x with
        | SearchPath.Forward -> true
        | SearchPath.Backward -> false

    member x.IsSearchPathBackward = not x.IsSearchPathForward

    static member Reverse path = 
        match path with
        | SearchPath.Forward -> SearchPath.Backward
        | SearchPath.Backward -> SearchPath.Forward

    static member Create isForward = 
        if isForward then SearchPath.Forward 
        else SearchPath.Backward

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
        | SearchKind.Forward -> SearchPath.Forward
        | SearchKind.ForwardWithWrap -> SearchPath.Forward
        | SearchKind.Backward -> SearchPath.Backward
        | SearchKind.BackwardWithWrap -> SearchPath.Backward

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
        | SearchPath.Forward -> SearchKind.Forward
        | SearchPath.Backward -> SearchKind.Backward

    static member OfPathAndWrap path wrap =
        match path with
        | SearchPath.Forward -> if wrap then SearchKind.ForwardWithWrap else SearchKind.Forward
        | SearchPath.Backward -> if wrap then SearchKind.BackwardWithWrap else SearchKind.Backward

