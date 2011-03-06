#light

namespace Vim
open Microsoft.VisualStudio.Text

[<RequireQualifiedAccess>]
type StringData = 
    | Simple of string
    | Block of NonEmptyCollection<string>
    with 

    member x.ApplyCount count =
        match x with 
        | Simple str -> StringUtil.repeat count str |> Simple
        | Block col -> col |> NonEmptyCollectionUtil.Map (StringUtil.repeat count) |> Block

    /// Returns the first String in the StringData instance. 
    member x.FirstString = 
        match x with
        | Simple str -> str
        | Block col -> col.Head

    // TODO: Delete this and force the use of individual values
    member x.String =
        match x with 
        | Simple(str) -> str
        | Block(l) -> l |> StringUtil.combineWith System.Environment.NewLine

    static member OfNormalizedSnasphotSpanCollection (col : NormalizedSnapshotSpanCollection) = 
        if col.Count = 0 then
            StringData.Simple StringUtil.empty
        elif col.Count = 1 then 
            col.[0] |> SnapshotSpanUtil.GetText |> StringData.Simple
        else
            col |> Seq.map SnapshotSpanUtil.GetText |> NonEmptyCollectionUtil.OfSeq |> Option.get |> StringData.Block

    static member OfSpan span = span |> SnapshotSpanUtil.GetText |> StringData.Simple

    static member OfSeq seq =  seq |> NormalizedSnapshotSpanCollectionUtil.OfSeq |> StringData.OfNormalizedSnasphotSpanCollection

    static member OfEditSpan editSpan =
        match editSpan with
        | EditSpan.Single span -> StringData.OfSpan span
        | EditSpan.Block col -> col |> NonEmptyCollectionUtil.Map SnapshotSpanUtil.GetText |> StringData.Block 

[<RequireQualifiedAccess>]
type NumberedRegister = 
    | Register_0
    | Register_1
    | Register_2
    | Register_3
    | Register_4
    | Register_5
    | Register_6
    | Register_7
    | Register_8
    | Register_9
    with

    member x.Char = 
        match x with 
        | Register_0 -> '0'
        | Register_1 -> '1'
        | Register_2 -> '2'
        | Register_3 -> '3'
        | Register_4 -> '4'
        | Register_5 -> '5'
        | Register_6 -> '6'
        | Register_7 -> '7'
        | Register_8 -> '8'
        | Register_9 -> '9'

    static member OfChar c = 
        match c with
        | '0' -> Some Register_0
        | '1' -> Some Register_1
        | '2' -> Some Register_2
        | '3' -> Some Register_3
        | '4' -> Some Register_4
        | '5' -> Some Register_5
        | '6' -> Some Register_6
        | '7' -> Some Register_7
        | '8' -> Some Register_8
        | '9' -> Some Register_9
        | _ -> None

    static member All = 
        ['0'..'9']
        |> Seq.map NumberedRegister.OfChar
        |> Seq.map Option.get

/// The 52 named registers
[<RequireQualifiedAccess>]
type NamedRegister = 
    | Register_a
    | Register_b
    | Register_c
    | Register_d
    | Register_e
    | Register_f
    | Register_g
    | Register_h
    | Register_i
    | Register_j
    | Register_k
    | Register_l
    | Register_m
    | Register_n
    | Register_o
    | Register_p
    | Register_q
    | Register_r
    | Register_s
    | Register_t
    | Register_u
    | Register_v
    | Register_w
    | Register_x
    | Register_y
    | Register_z
    | Register_A
    | Register_B
    | Register_C
    | Register_D
    | Register_E
    | Register_F
    | Register_G
    | Register_H
    | Register_I
    | Register_J
    | Register_K
    | Register_L
    | Register_M
    | Register_N
    | Register_O
    | Register_P
    | Register_Q
    | Register_R
    | Register_S
    | Register_T
    | Register_U
    | Register_V
    | Register_W
    | Register_X
    | Register_Y
    | Register_Z
    with 

    member x.Char =
        match x with 
        | Register_a -> 'a'
        | Register_b -> 'b'
        | Register_c -> 'c'
        | Register_d -> 'd'
        | Register_e -> 'e'
        | Register_f -> 'f'
        | Register_g -> 'g'
        | Register_h -> 'h'
        | Register_i -> 'i'
        | Register_j -> 'j'
        | Register_k -> 'k'
        | Register_l -> 'l'
        | Register_m -> 'm'
        | Register_n -> 'n'
        | Register_o -> 'o'
        | Register_p -> 'p'
        | Register_q -> 'q'
        | Register_r -> 'r'
        | Register_s -> 's'
        | Register_t -> 't'
        | Register_u -> 'u'
        | Register_v -> 'v'
        | Register_w -> 'w'
        | Register_x -> 'x'
        | Register_y -> 'y'
        | Register_z -> 'z'
        | Register_A -> 'A'
        | Register_B -> 'B'
        | Register_C -> 'C'
        | Register_D -> 'D'
        | Register_E -> 'E'
        | Register_F -> 'F'
        | Register_G -> 'G'
        | Register_H -> 'H'
        | Register_I -> 'I'
        | Register_J -> 'J'
        | Register_K -> 'K'
        | Register_L -> 'L'
        | Register_M -> 'M'
        | Register_N -> 'N'
        | Register_O -> 'O'
        | Register_P -> 'P'
        | Register_Q -> 'Q'
        | Register_R -> 'R'
        | Register_S -> 'S'
        | Register_T -> 'T'
        | Register_U -> 'U'
        | Register_V -> 'V'
        | Register_W -> 'W'
        | Register_X -> 'X'
        | Register_Y -> 'Y'
        | Register_Z -> 'Z'

    static member OfChar c = 
        match c with 
        | 'a' -> Some Register_a
        | 'b' -> Some Register_b
        | 'c' -> Some Register_c
        | 'd' -> Some Register_d
        | 'e' -> Some Register_e
        | 'f' -> Some Register_f
        | 'g' -> Some Register_g
        | 'h' -> Some Register_h
        | 'i' -> Some Register_i
        | 'j' -> Some Register_j
        | 'k' -> Some Register_k
        | 'l' -> Some Register_l
        | 'm' -> Some Register_m
        | 'n' -> Some Register_n
        | 'o' -> Some Register_o
        | 'p' -> Some Register_p
        | 'q' -> Some Register_q
        | 'r' -> Some Register_r
        | 's' -> Some Register_s
        | 't' -> Some Register_t
        | 'u' -> Some Register_u
        | 'v' -> Some Register_v
        | 'w' -> Some Register_w
        | 'x' -> Some Register_x
        | 'y' -> Some Register_y
        | 'z' -> Some Register_z
        | 'A' -> Some Register_A
        | 'B' -> Some Register_B
        | 'C' -> Some Register_C
        | 'D' -> Some Register_D
        | 'E' -> Some Register_E
        | 'F' -> Some Register_F
        | 'G' -> Some Register_G
        | 'H' -> Some Register_H
        | 'I' -> Some Register_I
        | 'J' -> Some Register_J
        | 'K' -> Some Register_K
        | 'L' -> Some Register_L
        | 'M' -> Some Register_M
        | 'N' -> Some Register_N
        | 'O' -> Some Register_O
        | 'P' -> Some Register_P
        | 'Q' -> Some Register_Q
        | 'R' -> Some Register_R
        | 'S' -> Some Register_S
        | 'T' -> Some Register_T
        | 'U' -> Some Register_U
        | 'V' -> Some Register_V
        | 'W' -> Some Register_W
        | 'X' -> Some Register_X
        | 'Y' -> Some Register_Y
        | 'Z' -> Some Register_Z
        | _ -> None

    static member All = 
        ['a'..'z']
        |> Seq.append ['A'..'Z']
        |> Seq.map NamedRegister.OfChar
        |> Seq.map Option.get

[<RequireQualifiedAccess>]
type ReadOnlyRegister =
    | Register_Percent
    | Register_Dot
    | Register_Colon
    | Register_Pound
    with

    member x.Char = 
        match x with
        | Register_Percent -> '%' 
        | Register_Dot -> '.'
        | Register_Colon -> ':'
        | Register_Pound -> '#'

    static member OfChar c = 
        match c with
        | '%' -> Some Register_Percent
        | '.' -> Some Register_Dot
        | ':' -> Some Register_Colon
        | '#' -> Some Register_Pound
        | _ -> None

    static member All = 
        "%.:#"
        |> Seq.map ReadOnlyRegister.OfChar
        |> Seq.map Option.get

[<RequireQualifiedAccess>]
type SelectionAndDropRegister =
    | Register_Star 
    | Register_Plus
    | Register_Tilde
    with

    member x.Char = 
        match x with 
        | Register_Star -> '*'
        | Register_Plus -> '+'
        | Register_Tilde -> '~'

    static member OfChar c =
        match c with 
        | '*' -> Some Register_Star 
        | '+' -> Some Register_Plus
        | '~' -> Some Register_Tilde
        | _ -> None

    static member All = 
        "*+~"
        |> Seq.map SelectionAndDropRegister.OfChar
        |> Seq.map Option.get

/// The type of register that this represents.  See :help registers for
/// a full list of information
[<RequireQualifiedAccess>]
type RegisterName =
    /// The unnamed register.  This is the default register for many types of operations
    | Unnamed
    | Numbered of NumberedRegister
    | SmallDelete
    /// The A-Z and a-z registers
    | Named of NamedRegister
    /// The 4 readonly registers :, ., % and #
    | ReadOnly of ReadOnlyRegister
    | Expression 
    | SelectionAndDrop of SelectionAndDropRegister
    | Blackhole
    | LastSearchPattern 
    with

    member x.Char = 
        match x with 
        | Unnamed -> None
        | SmallDelete -> Some '-'
        | Blackhole -> Some '_'
        | LastSearchPattern -> Some '/'
        | Expression -> Some '='
        | Numbered(r) -> Some r.Char
        | Named(r) -> Some r.Char
        | ReadOnly(r) -> Some r.Char
        | SelectionAndDrop(r) -> Some r.Char

    static member OfChar c = 
        match NumberedRegister.OfChar c with
        | Some(r) -> Some (Numbered r)
        | None ->
            match NamedRegister.OfChar c with
            | Some(r) -> Some (Named r)
            | None ->
                match ReadOnlyRegister.OfChar c with
                | Some(r) -> Some (ReadOnly r)
                | None ->
                    match SelectionAndDropRegister.OfChar c with
                    | Some(r) -> Some (SelectionAndDrop r)
                    | None -> 
                        match c with
                        | '=' -> Some Expression
                        | '_' -> Some Blackhole
                        | '/' -> Some LastSearchPattern
                        | '-' -> Some SmallDelete
                        | _ -> None

    static member All = 
        NamedRegister.All |> Seq.map (fun n -> Named n)
        |> Seq.append (NumberedRegister.All |> Seq.map (fun n -> Numbered n))
        |> Seq.append (ReadOnlyRegister.All |> Seq.map (fun n -> ReadOnly n))
        |> Seq.append (SelectionAndDropRegister.All |> Seq.map (fun n -> SelectionAndDrop n))
        |> Seq.append [Unnamed; Expression; Blackhole; LastSearchPattern; SmallDelete]

module RegisterNameUtil = 

    /// All of the available register names
    let RegisterNames = RegisterName.All

    /// All of the char values which represent register names
    let RegisterNameChars = 
        RegisterNames 
        |> Seq.map (fun n -> n.Char)
        |> SeqUtil.filterToSome
        |> List.ofSeq

    /// Mapping of the available char's to the appropriate RegisterName
    let RegisterMap = 
        RegisterNames
        |> Seq.map (fun r -> (r.Char),r)
        |> Seq.map OptionUtil.combine2
        |> SeqUtil.filterToSome
        |> Map.ofSeq

    let CharToRegister c = Map.tryFind c RegisterMap 

[<RequireQualifiedAccess>]
type RegisterOperation = 
    | Delete
    | Yank

/// Value stored in the register.  Contains contextual information on how the data
/// was yanked from the buffer
type RegisterValue = {
    Value : StringData;
    OperationKind : OperationKind;
}
    with

    static member CreateLineWise d = { Value = d; OperationKind=OperationKind.LineWise }

    static member CreateFromText text = {Value=StringData.Simple text; OperationKind=OperationKind.LineWise }

/// Backing of a register value
type internal IRegisterValueBacking = 
    abstract Value : RegisterValue with get,set

type internal DefaultRegisterValueBacking() = 
    let mutable _value = { Value=StringData.Simple StringUtil.empty; OperationKind=OperationKind.LineWise }
    interface IRegisterValueBacking with
        member x.Value 
            with get() = _value
            and set value = _value <- value

/// Represents one of the register is Vim 
type Register
    internal 
    ( 
        _name : RegisterName,
        _valueBacking : IRegisterValueBacking ) = 

    new (c:char) =
        let name = RegisterNameUtil.CharToRegister c |> Option.get
        Register(name, DefaultRegisterValueBacking() :> IRegisterValueBacking )

    new (name) = Register(name, DefaultRegisterValueBacking() :> IRegisterValueBacking )

    member x.Name = _name
    member x.OperationKind = _valueBacking.Value.OperationKind
    member x.StringValue = _valueBacking.Value.Value.String
    member x.StringData = _valueBacking.Value.Value
    member x.Value 
        with get () =  _valueBacking.Value
        and set value = _valueBacking.Value <- value

