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


type KeyModifiers = 
    | Shift = 0x1
    | Alt = 0x2
    | Control = 0x4

type KeyInput(literal:char,key:Key,modKey:ModifierKeys) =
    new (c,k) = KeyInput(c,k, ModifierKeys.None)

    member x.Char = literal
    member x.Key = key
    member x.ModifierKeys = modKey
    member x.HasShiftModifier = modKey = ModifierKeys.Shift
    member x.IsDigit = System.Char.IsDigit(x.Char)

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member x.IsNewLine = 
        match key with
            | Key.Enter -> true
            | Key.LineFeed -> true
            | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match key with
        | Key.Left -> true
        | Key.Right -> true
        | Key.Up -> true
        | Key.Down -> true
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
                compare x.ModifierKeys other.ModifierKeys
                    
    override x.GetHashCode() = int32 literal
    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other ->
            0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.ModifierKeys);
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? KeyInput as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  
        
