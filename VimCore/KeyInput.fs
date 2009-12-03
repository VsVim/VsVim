#light

namespace VimCore
open System.Windows.Input
    
type KeyInput(literal:char,key:Key,modKey:ModifierKeys) =
    new (c,k) = KeyInput(c,k, ModifierKeys.None)

    member x.Char = literal
    member x.Key = key
    member x.ModifierKeys = modKey
    member x.HasShiftModifier = modKey = ModifierKeys.Shift
    member x.IsDigit = 
        if not x.HasShiftModifier then
            match key with 
                | Key.D0 -> true
                | Key.D1 -> true
                | Key.D2 -> true
                | Key.D3 -> true
                | Key.D4 -> true
                | Key.D5 -> true
                | Key.D6 -> true
                | Key.D7 -> true
                | Key.D8 -> true
                | Key.D9 -> true
                | _ -> false
        else false

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
        
