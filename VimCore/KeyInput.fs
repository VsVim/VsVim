#light

namespace Vim

/// Provides values for the well known key values used by Vim 
type VimKey =
    | NotWellKnown = 0
    | Back = 1
    | Tab = 2
    | Enter = 3
    | Escape = 4 
    | Left = 5
    | Up = 6
    | Right = 7
    | Down = 8
    | Delete = 9
    | Help = 10
    | End = 11
    | PageUp = 12
    | PageDown = 13
    | Insert = 14
    | Home = 15
    | Break = 16
    | F1 = 17
    | F2 = 18
    | F3 = 19
    | F4 = 20
    | F5 = 21
    | F6 = 22
    | F7 = 23
    | F8 = 24
    | F9 = 25
    | F10 = 26
    | F11 = 27
    | F12 = 28
    | Multiply = 29
    | Divide = 30
    | Separator = 31
    | Subtract = 32
    | Add = 33
    | Decimal = 34

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

    member x.Char = _literal
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = System.Char.IsDigit(x.Char)

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member x.IsNewLine = 
        match _key with
            | VimKey.Enter -> true
            | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match _key with
        | VimKey.Left -> true
        | VimKey.Right -> true
        | VimKey.Up -> true
        | VimKey.Down -> true
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
        
