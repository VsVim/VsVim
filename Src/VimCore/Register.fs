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
        | Simple str -> str
        | Block l -> l |> StringUtil.combineWith System.Environment.NewLine

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
        | EditSpan.Block col -> col |> NonEmptyCollectionUtil.Map (fun span -> span.GetText()) |> StringData.Block 

[<RequireQualifiedAccess>]
type NumberedRegister = 
    | Number0
    | Number1
    | Number2
    | Number3
    | Number4
    | Number5
    | Number6
    | Number7
    | Number8
    | Number9
    with

    member x.Char = 
        match x with 
        | Number0 -> '0'
        | Number1 -> '1'
        | Number2 -> '2'
        | Number3 -> '3'
        | Number4 -> '4'
        | Number5 -> '5'
        | Number6 -> '6'
        | Number7 -> '7'
        | Number8 -> '8'
        | Number9 -> '9'

    static member OfChar c = 
        match c with
        | '0' -> Some Number0
        | '1' -> Some Number1
        | '2' -> Some Number2
        | '3' -> Some Number3
        | '4' -> Some Number4
        | '5' -> Some Number5
        | '6' -> Some Number6
        | '7' -> Some Number7
        | '8' -> Some Number8
        | '9' -> Some Number9
        | _ -> None

    static member All = 
        ['0'..'9']
        |> Seq.map NumberedRegister.OfChar
        |> Seq.map Option.get

/// The 52 named registers
[<RequireQualifiedAccess>]
type NamedRegister = 
    | Namea
    | Nameb
    | Namec
    | Named
    | Namee
    | Namef
    | Nameg
    | Nameh
    | Namei
    | Namej
    | Namek
    | Namel
    | Namem
    | Namen
    | Nameo
    | Namep
    | Nameq
    | Namer
    | Names
    | Namet
    | Nameu
    | Namev
    | Namew
    | Namex
    | Namey
    | Namez
    | NameA
    | NameB
    | NameC
    | NameD
    | NameE
    | NameF
    | NameG
    | NameH
    | NameI
    | NameJ
    | NameK
    | NameL
    | NameM
    | NameN
    | NameO
    | NameP
    | NameQ
    | NameR
    | NameS
    | NameT
    | NameU
    | NameV
    | NameW
    | NameX
    | NameY
    | NameZ
    with 

    /// Is this an append register
    member x.IsAppend = CharUtil.IsUpper x.Char

    member x.Char =
        match x with 
        | Namea -> 'a'
        | Nameb -> 'b'
        | Namec -> 'c'
        | Named -> 'd'
        | Namee -> 'e'
        | Namef -> 'f'
        | Nameg -> 'g'
        | Nameh -> 'h'
        | Namei -> 'i'
        | Namej -> 'j'
        | Namek -> 'k'
        | Namel -> 'l'
        | Namem -> 'm'
        | Namen -> 'n'
        | Nameo -> 'o'
        | Namep -> 'p'
        | Nameq -> 'q'
        | Namer -> 'r'
        | Names -> 's'
        | Namet -> 't'
        | Nameu -> 'u'
        | Namev -> 'v'
        | Namew -> 'w'
        | Namex -> 'x'
        | Namey -> 'y'
        | Namez -> 'z'
        | NameA -> 'A'
        | NameB -> 'B'
        | NameC -> 'C'
        | NameD -> 'D'
        | NameE -> 'E'
        | NameF -> 'F'
        | NameG -> 'G'
        | NameH -> 'H'
        | NameI -> 'I'
        | NameJ -> 'J'
        | NameK -> 'K'
        | NameL -> 'L'
        | NameM -> 'M'
        | NameN -> 'N'
        | NameO -> 'O'
        | NameP -> 'P'
        | NameQ -> 'Q'
        | NameR -> 'R'
        | NameS -> 'S'
        | NameT -> 'T'
        | NameU -> 'U'
        | NameV -> 'V'
        | NameW -> 'W'
        | NameX -> 'X'
        | NameY -> 'Y'
        | NameZ -> 'Z'

    static member OfChar c = 
        match c with 
        | 'a' -> Some Namea
        | 'b' -> Some Nameb
        | 'c' -> Some Namec
        | 'd' -> Some Named
        | 'e' -> Some Namee
        | 'f' -> Some Namef
        | 'g' -> Some Nameg
        | 'h' -> Some Nameh
        | 'i' -> Some Namei
        | 'j' -> Some Namej
        | 'k' -> Some Namek
        | 'l' -> Some Namel
        | 'm' -> Some Namem
        | 'n' -> Some Namen
        | 'o' -> Some Nameo
        | 'p' -> Some Namep
        | 'q' -> Some Nameq
        | 'r' -> Some Namer
        | 's' -> Some Names
        | 't' -> Some Namet
        | 'u' -> Some Nameu
        | 'v' -> Some Namev
        | 'w' -> Some Namew
        | 'x' -> Some Namex
        | 'y' -> Some Namey
        | 'z' -> Some Namez
        | 'A' -> Some NameA
        | 'B' -> Some NameB
        | 'C' -> Some NameC
        | 'D' -> Some NameD
        | 'E' -> Some NameE
        | 'F' -> Some NameF
        | 'G' -> Some NameG
        | 'H' -> Some NameH
        | 'I' -> Some NameI
        | 'J' -> Some NameJ
        | 'K' -> Some NameK
        | 'L' -> Some NameL
        | 'M' -> Some NameM
        | 'N' -> Some NameN
        | 'O' -> Some NameO
        | 'P' -> Some NameP
        | 'Q' -> Some NameQ
        | 'R' -> Some NameR
        | 'S' -> Some NameS
        | 'T' -> Some NameT
        | 'U' -> Some NameU
        | 'V' -> Some NameV
        | 'W' -> Some NameW
        | 'X' -> Some NameX
        | 'Y' -> Some NameY
        | 'Z' -> Some NameZ
        | _ -> None

    static member All = 
        ['a'..'z']
        |> Seq.append ['A'..'Z']
        |> Seq.map NamedRegister.OfChar
        |> Seq.map Option.get

[<RequireQualifiedAccess>]
type ReadOnlyRegister =
    | Percent
    | Dot
    | Colon
    | Pound
    with

    member x.Char = 
        match x with
        | Percent -> '%' 
        | Dot -> '.'
        | Colon -> ':'
        | Pound -> '#'

    static member OfChar c = 
        match c with
        | '%' -> Some Percent
        | '.' -> Some Dot
        | ':' -> Some Colon
        | '#' -> Some Pound
        | _ -> None

    static member All = 
        "%.:#"
        |> Seq.map ReadOnlyRegister.OfChar
        |> Seq.map Option.get

[<RequireQualifiedAccess>]
type SelectionAndDropRegister =
    | Star 
    | Plus
    | Tilde
    with

    member x.Char = 
        match x with 
        | Star -> '*'
        | Plus -> '+'
        | Tilde -> '~'

    static member OfChar c =
        match c with 
        | '*' -> Some Star 
        | '+' -> Some Plus
        | '~' -> Some Tilde
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
[<NoComparison>]
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
type RegisterValue
    (
        _isString : bool,
        _stringData : StringData,
        _keyInputs : KeyInput list,
        _operationKind : OperationKind
    ) =

    /// Once a line wise value is in a string form it should always end with a new line.  It's
    /// hard to guarantee this at all of the sites which produce register values though because
    /// of quirks in the editor.  This is a check to make sure we got it right
    static let IsNormalized stringData operationKind = 
        match stringData, operationKind with
        | StringData.Simple str, OperationKind.LineWise -> EditUtil.EndsWithNewLine str
        | _ -> true
        
    new (value : string, operationKind : OperationKind) =
        RegisterValue(StringData.Simple value, operationKind)

    new (stringData : StringData, operationKind : OperationKind) =
        // TODO: Why do we allow an OperationKind here?  Can you ever have a line wise block StringData?
        Debug.Assert(IsNormalized stringData operationKind)
        RegisterValue(true, stringData, List.empty, operationKind)

    new (keyInputs : KeyInput list) =
        RegisterValue(false, StringData.Simple "", keyInputs, OperationKind.CharacterWise)

    /// Get the RegisterData as a StringData instance
    member x.StringData =
        if _isString then
            _stringData
        else
            _keyInputs |> Seq.map (fun ki -> ki.Char) |> StringUtil.ofCharSeq |> StringData.Simple

    /// Get the RegisterData as a KeyInput list instance
    member x.KeyInputs =
        if _isString then
            match _stringData with
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
        else
            _keyInputs

    /// Get the string which represents this RegisterValue.  This is an inherently lossy 
    /// operation (information is loss on converting a StringData.Block into a raw string
    /// value).  This function should be avoided for operations other than display purposes
    member x.StringValue = 
        if _isString then
            _stringData.String
        else
            _keyInputs |> Seq.map (fun keyInput -> keyInput.Char) |> StringUtil.ofCharSeq

    /// The OperationKind which produced this value
    member x.OperationKind = _operationKind 

    /// Append the provided RegisterValue to this one.  Used for append register operations (yank, 
    /// delete, etc ... with an upper case register)
    member x.Append (value : RegisterValue) =
        if _isString then
            let stringData = x.StringData.Append value.StringData
            RegisterValue(stringData, x.OperationKind)
        else
            let keyInputs = _keyInputs @ value.KeyInputs
            RegisterValue(keyInputs)

/// Backing of a register value
type internal IRegisterValueBacking = 

    /// The RegisterValue to use
    abstract RegisterValue : RegisterValue with get, set

/// Default implementation of IRegisterValueBacking.  Just holds the RegisterValue
/// in a mutable field
type internal DefaultRegisterValueBacking() = 
    let mutable _value = RegisterValue(StringUtil.empty, OperationKind.CharacterWise)
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

