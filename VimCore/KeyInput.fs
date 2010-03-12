#light

namespace Vim

/// Provides values for the well known key values used by Vim 
type VimKey =
    | NotWellKnownKey = 0
    | BackKey = 1
    | TabKey = 2
    | EnterKey = 3
    | EscapeKey = 4 
    | LeftKey = 5
    | UpKey = 6
    | RightKey = 7
    | DownKey = 8
    | LineFeedKey = 9
    | HelpKey = 10
    | EndKey = 11
    | PageUpKey = 12
    | PageDownKey = 13
    | InsertKey = 14
    | HomeKey = 15
    | BreakKey = 16
    | F1Key = 17
    | F2Key = 18
    | F3Key = 19
    | F4Key = 20
    | F5Key = 21
    | F6Key = 22
    | F7Key = 23
    | F8Key = 24
    | F9Key = 25
    | F10Key = 26
    | F11Key = 27
    | F12Key = 28
    | DeleteKey = 29

[<System.Flags>]
type KeyModifiers = 
    | None = 0x0
    | Alt = 0x1
    | Control = 0x2
    | Shift = 0x4

type KeyInput
    (
        _literal:char,
        _key:VimKey,
        _modKey:KeyModifiers) =

    new (c) = KeyInput(c,VimKey.NotWellKnownKey,KeyModifiers.None)
    new (c,modKey) = KeyInput(c,VimKey.NotWellKnownKey,modKey)

    member x.Char = _literal
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = System.Char.IsDigit(x.Char)

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member x.IsNewLine = 
        match _key with
            | VimKey.EnterKey -> true
            | VimKey.LineFeedKey -> true
            | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match _key with
        | VimKey.LeftKey -> true
        | VimKey.RightKey -> true
        | VimKey.UpKey -> true
        | VimKey.DownKey -> true
        | _ -> false

    member private x.CompareTo (other:KeyInput) =
        let comp = compare x.Char other.Char
        if comp <> 0 then 
            comp
        else
            let comp = compare x.Key other.Key
            if comp <> 0 then 
                comp
            else
                compare x.KeyModifiers other.KeyModifiers
                    
    override x.GetHashCode() = int32 _literal
    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other ->
            0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.KeyModifiers);
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? KeyInput as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  
        
