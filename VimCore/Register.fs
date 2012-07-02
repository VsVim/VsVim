#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Diagnostics

/// Representation of StringData stored in a Register
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

    member x.String =
        match x with 
        | Simple(str) -> str
        | Block(l) -> l |> StringUtil.combineWith System.Environment.NewLine

    /// Append the specified string data to this StringData instance and return a 
    /// combined value.  This is used when append operations are done wit the yank 
    /// and delete register operations
    member x.Append other =
        match x, other with
        | Simple left, Simple right -> Simple (left + right)
        | Simple left, Block right -> Block (NonEmptyCollection(left + right.Head, right.Rest))
        | Block left, Simple right -> Block (NonEmptyCollectionUtil.Append [ right ] left)
        | Block left, Block right -> Block (NonEmptyCollectionUtil.Append (right.All |> List.ofSeq) left)

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

    /// Is this an append register
    member x.IsAppend = CharUtil.IsUpper x.Char

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
    /// The 4 read only registers :, ., % and #
    | ReadOnly of ReadOnlyRegister
    | Expression 
    | SelectionAndDrop of SelectionAndDropRegister
    | Blackhole
    | LastSearchPattern 
    with

    member x.Char = 
        match x with 
        | Unnamed -> Some '"'
        | SmallDelete -> Some '-'
        | Blackhole -> Some '_'
        | LastSearchPattern -> Some '/'
        | Expression -> Some '='
        | Numbered r -> Some r.Char
        | Named r -> Some r.Char
        | ReadOnly r -> Some r.Char
        | SelectionAndDrop r -> Some r.Char

    /// Is this one of the named append registers
    member x.IsAppend =
        match x with 
        | Unnamed -> false
        | SmallDelete -> false
        | Blackhole -> false
        | LastSearchPattern -> false
        | Expression -> false
        | Numbered _ -> false
        | Named r -> r.IsAppend
        | ReadOnly r -> false
        | SelectionAndDrop _ -> false

    static member OfChar c = 
        match NumberedRegister.OfChar c with
        | Some r -> Some (Numbered r)
        | None ->
            match NamedRegister.OfChar c with
            | Some r -> Some (Named r)
            | None ->
                match ReadOnlyRegister.OfChar c with
                | Some r -> Some (ReadOnly r)
                | None ->
                    match SelectionAndDropRegister.OfChar c with
                    | Some r -> Some (SelectionAndDrop r)
                    | None -> 
                        match c with
                        | '=' -> Some Expression
                        | '_' -> Some Blackhole
                        | '/' -> Some LastSearchPattern
                        | '-' -> Some SmallDelete
                        | '"' -> Some Unnamed
                        | _ -> None

    static member All = 
        NamedRegister.All |> Seq.map (fun n -> Named n)
        |> Seq.append (NumberedRegister.All |> Seq.map (fun n -> Numbered n))
        |> Seq.append (ReadOnlyRegister.All |> Seq.map (fun n -> ReadOnly n))
        |> Seq.append (SelectionAndDropRegister.All |> Seq.map (fun n -> SelectionAndDrop n))
        |> Seq.append [Unnamed; Expression; Blackhole; LastSearchPattern; SmallDelete]

module RegisterNameUtil = 

    /// All of the char values which represent register names
    let RegisterNameChars = 
        RegisterName.All
        |> Seq.map (fun n -> n.Char)
        |> SeqUtil.filterToSome
        |> List.ofSeq

    /// Mapping of the available char's to the appropriate RegisterName
    let RegisterMap = 
        RegisterName.All
        |> Seq.map (fun r -> (r.Char),r)
        |> Seq.map OptionUtil.combine2
        |> SeqUtil.filterToSome
        |> Map.ofSeq

    let CharToRegister c = Map.tryFind c RegisterMap 

[<DebuggerDisplay("{ToString(),nq}")>]
[<RequireQualifiedAccess>]
type RegisterOperation = 
    | Delete
    | Yank

    with

    override x.ToString() =
        match x with
        | Delete -> "Delete"
        | Yank -> "Yank"

/// Represents the data stored in a Register.  Registers need to store both string values 
/// for cut and paste operations and KeyInput sequences for Macro recording.  There is not 
/// 100% fidelity between these formats and hence we have to have this intermediate state
/// to deal with.
///
/// Quick Example:  There is no way to store <Left> or any arrow key as a char value (they
/// simple don't have one associated and you can check ':help key-notation' for verification
/// of this).  Trying to store '<Left>' in the register causes playback to be interpreted 
/// not as the <Left> key but as the key sequence <, L, e, f, t, >.  
///
/// Unfortunately we are required to convert back and forth in different circumstances.  There
/// will be fidelity loss in some
[<RequireQualifiedAccess>]
type RegisterValue =
    /// Backing is a String type.  This is the type of lower fidelity.  Prefer KeyInput over String
    | String of StringData * OperationKind

    /// Backing is a KeyInput list.  This is the type of highest fidelity.  Prefer this over String
    | KeyInput of KeyInput list * OperationKind

    with

    /// Get the RegisterData as a StringData instance
    member x.StringData =
        match x with 
        | String (stringData, _) -> stringData
        | KeyInput (list, _) -> list |> Seq.map (fun ki -> ki.Char) |> StringUtil.ofCharSeq |> StringData.Simple

    /// Get the RegisterData as a KeyInput list instance
    member x.KeyInputList =
        match x with
        | String (stringData, _) ->
            match stringData with
            | StringData.Simple str -> 
                // Just map every character to a KeyInput
                str |> Seq.map KeyInputUtil.CharToKeyInput |> List.ofSeq

            | StringData.Block col -> 
                // Map every character in every string into a KeyInput and add a <Enter> at the
                // end of every String
                col
                |> Seq.map (fun s -> s |> Seq.map KeyInputUtil.CharToKeyInput |> List.ofSeq)
                |> Seq.map (fun list -> list @ [ KeyInputUtil.EnterKey ])
                |> List.concat
        | KeyInput (list, _) -> 
            // Identity mapping 
            list

    /// Get the string which represents this RegisterValue.  This is an inherently lossy 
    /// operation (information is loss on converting a StringData.Block into a raw string
    /// value).  This function should be avoided for operations other than display purposes
    member x.StringValue = 
        match x with
        | String (stringData, _) -> stringData.String
        | KeyInput (list, _) -> list |> Seq.map (fun keyInput -> keyInput.Char) |> StringUtil.ofCharSeq

    /// The OperationKind which produced this value
    member x.OperationKind = 
        match x with 
        | String (_, kind) -> kind
        | KeyInput (_, kind) -> kind

    /// Append the provided RegisterValue to this one.  Used for append register operations (yank, 
    /// delete, etc ... with an upper case register)
    member x.Append value =
        match x, value with
        | String (leftData, _), String (rightData, _) -> String (leftData.Append rightData, x.OperationKind)
        | String (leftData, _), KeyInput _ -> String (leftData.Append value.StringData, x.OperationKind)
        | KeyInput (list, _), String (rightData, _) -> KeyInput (list @ (rightData.String |> Seq.map KeyInputUtil.CharToKeyInput |> List.ofSeq), x.OperationKind)
        | KeyInput (left, _), KeyInput (right, _) -> KeyInput (left @ right, x.OperationKind)

    /// Create a RegisterValue from a simple string
    static member OfString str kind = 
        let stringData = StringData.Simple str
        String (stringData, kind)

/// Backing of a register value
type internal IRegisterValueBacking = 

    /// The RegisterValue to use
    abstract RegisterValue : RegisterValue with get, set

/// Default implementation of IRegisterValueBacking.  Just holds the RegisterValue
/// in a mutable field
type internal DefaultRegisterValueBacking() = 
    let mutable _value = RegisterValue.OfString StringUtil.empty OperationKind.CharacterWise
    interface IRegisterValueBacking with
        member x.RegisterValue
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
    member x.OperationKind = _valueBacking.RegisterValue.OperationKind
    member x.StringValue = _valueBacking.RegisterValue.StringValue
    member x.StringData = _valueBacking.RegisterValue.StringData
    member x.RegisterValue 
        with get () =  _valueBacking.RegisterValue
        and set value = _valueBacking.RegisterValue <- value

