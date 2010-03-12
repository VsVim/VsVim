#light

namespace Vim
open System.Windows.Input

/// Provides values for the well known key values used by Vim 
type WellKnownKey =
    | NotWellKnownKey
    | BackKey
    | TabKey
    | EnterKey
    | ReturnKey
    | EscapeKey
    | DeleteKey
    | LeftKey
    | UpKey
    | RightKey
    | DownKey
    | LineFeedKey
    | HelpKey
    | EndKey
    | PageUpKey
    | PageDownKey
    | InsertKey
    | HomeKey
    | BreakKey
    | F1Key
    | F2Key
    | F3Key
    | F4Key
    | F5Key
    | F6Key
    | F7Key
    | F8Key
    | F9Key
    | F10Key
    | F11Key
    | F12Key

[<System.Flags>]
type KeyModifiers = 
    | None = 0x0
    | Alt = 0x1
    | Control = 0x2
    | Shift = 0x4

type KeyInput
    (
        _literal:char,
        _key:WellKnownKey,
        _modKey:KeyModifiers) =

    new (c) = KeyInput(c,WellKnownKey.NotWellKnownKey,KeyModifiers.None)
    new (c,modKey) = KeyInput(c,WellKnownKey.NotWellKnownKey,modKey)

    member x.Char = _literal
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = System.Char.IsDigit(x.Char)

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member x.IsNewLine = 
        match _key with
            | WellKnownKey.EnterKey -> true
            | WellKnownKey.ReturnKey -> true
            | WellKnownKey.LineFeedKey -> true
            | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match _key with
        | LeftKey -> true
        | RightKey -> true
        | UpKey -> true
        | DownKey -> true
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
        
