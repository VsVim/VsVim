#light

namespace Vim
open System.Runtime.InteropServices

type VirtualKeyCode = int

[<Sealed>]
type KeyInput
    (
        _key:VimKey,
        _modKey:KeyModifiers, 
        _literal:char option ) =

    member x.Char = _literal |> OptionUtil.getOrDefault CharUtil.MinValue
    member x.RawChar = _literal
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
        if comp <> 0 then  comp
        else
            let comp = compare x.Key other.Key
            if comp <> 0 then  comp
            else
                compare x.KeyModifiers other.KeyModifiers
                    
    override x.GetHashCode() = int32 x.Char
    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other ->
            0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.KeyModifiers);

    static member DefaultValue = KeyInput(VimKey.NotWellKnown, KeyModifiers.None, None)
    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other))
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? KeyInput as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  

    interface System.IEquatable<KeyInput> with
        member x.Equals other = 0 = x.CompareTo other
module KeyInputUtil = 

    /// Mapping of all VimKey instances with their associated char if one exists
    let VimKeyRawData = [
        (VimKey.Back, Some '\b')
        (VimKey.Tab, Some '\t')
        (VimKey.Enter, None)
        (VimKey.Escape, None)
        (VimKey.Left, None)
        (VimKey.Up, None)
        (VimKey.Right, None)
        (VimKey.Down, None)
        (VimKey.Delete, None)
        (VimKey.Help, None)
        (VimKey.End, None)
        (VimKey.PageUp, None)
        (VimKey.PageDown, None)
        (VimKey.Insert, None)
        (VimKey.Home, None)
        (VimKey.Break, None)
        (VimKey.F1, None)
        (VimKey.F2, None)
        (VimKey.F3, None)
        (VimKey.F4, None)
        (VimKey.F5, None)
        (VimKey.F6, None)
        (VimKey.F7, None)
        (VimKey.F8, None)
        (VimKey.F9, None)
        (VimKey.F10, None)
        (VimKey.F11, None)
        (VimKey.F12, None)
        (VimKey.KeypadDecimal, None)
        (VimKey.Keypad0, Some '0')
        (VimKey.Keypad1, Some '1')
        (VimKey.Keypad2, Some '2')
        (VimKey.Keypad3, Some '3')
        (VimKey.Keypad4, Some '4')
        (VimKey.Keypad5, Some '5')
        (VimKey.Keypad6, Some '6')
        (VimKey.Keypad7, Some '7')
        (VimKey.Keypad8, Some '8')
        (VimKey.Keypad9, Some '9')
        (VimKey.KeypadPlus, Some '+')
        (VimKey.KeypadMinus, Some '-')
        (VimKey.KeypadDivide, Some '/')
        (VimKey.KeypadMultiply, Some '*')
        (VimKey.A, Some 'a')
        (VimKey.B, Some 'b')
        (VimKey.C, Some 'c')
        (VimKey.D, Some 'd')
        (VimKey.E, Some 'e')
        (VimKey.F, Some 'f')
        (VimKey.G, Some 'g')
        (VimKey.H, Some 'h')
        (VimKey.I, Some 'i')
        (VimKey.J, Some 'j')
        (VimKey.K, Some 'k')
        (VimKey.L, Some 'l')
        (VimKey.M, Some 'm')
        (VimKey.N, Some 'n')
        (VimKey.O, Some 'o')
        (VimKey.P, Some 'p')
        (VimKey.Q, Some 'q')
        (VimKey.R, Some 'r')
        (VimKey.S, Some 's')
        (VimKey.T, Some 't')
        (VimKey.U, Some 'u')
        (VimKey.V, Some 'v')
        (VimKey.W, Some 'w')
        (VimKey.X, Some 'x')
        (VimKey.Y, Some 'y')
        (VimKey.Z, Some 'z')
        (VimKey.Number0, Some '0')
        (VimKey.Number1, Some '1')
        (VimKey.Number2, Some '2')
        (VimKey.Number3, Some '3')
        (VimKey.Number4, Some '4')
        (VimKey.Number5, Some '5')
        (VimKey.Number6, Some '6')
        (VimKey.Number7, Some '7')
        (VimKey.Number8, Some '8')
        (VimKey.Number9, Some '9')
        (VimKey.Bang, Some '!')
        (VimKey.AtSign, Some '@')
        (VimKey.Pound, Some '#')
        (VimKey.Percent, Some '%')
        (VimKey.Caret, Some '^')
        (VimKey.Ampersand, Some '&')
        (VimKey.Asterick, Some '*')
        (VimKey.OpenParen, Some '(')
        (VimKey.CloseParen, Some ')')
        (VimKey.OpenBracket, Some '[')
        (VimKey.CloseBracket, Some ']')
        (VimKey.OpenBrace, Some '{')
        (VimKey.CloseBrace, Some '}')
        (VimKey.Minus, Some '-')
        (VimKey.Underscore, Some '_')
        (VimKey.Equals, Some '=')
        (VimKey.Backslash, Some '\\')
        (VimKey.Forwardslash, Some '/')
        (VimKey.Plus, Some '+')
        (VimKey.Pipe, Some '|')
        (VimKey.SingleQuote, Some '\'')
        (VimKey.DoubleQuote, Some '"')
        (VimKey.Backtick, Some '`')
        (VimKey.Question, Some '?')
        (VimKey.Comma, Some ',')
        (VimKey.LessThan, Some '<')
        (VimKey.GreaterThan, Some '>')
        (VimKey.Period, Some '.')
        (VimKey.Semicolon, Some ';')
        (VimKey.Colon, Some ':')
        (VimKey.Tilde, Some '~')
        (VimKey.Space, Some ' ')
        (VimKey.Dollar, Some '$') ]

    let CoreKeyInputList  =
        let upperLetters = 
            VimKeyRawData
            |> Seq.ofList
            |> Seq.filter (fun (_,c) -> 
                match c with
                | None -> false
                | Some(c) -> CharUtil.IsLetter c)
            |> Seq.map (fun (key,c) -> KeyInput(key, KeyModifiers.Shift, c |> Option.get |> CharUtil.ToUpper |> Some))

        let rawDataInput =  VimKeyRawData  |> Seq.map (fun (key,c) -> KeyInput(key, KeyModifiers.None, c))
        Seq.append rawDataInput upperLetters 
        |> List.ofSeq

    let CoreCharacterList = 
        CoreKeyInputList
        |> Seq.map (fun ki -> ki.RawChar)
        |> SeqUtil.filterToSome
        |> Set.ofSeq
        |> Set.toList

    /// Map for core characters to the KeyInput representation.  While several keys 
    /// may map to the same character their should be a primary KeyInput for every 
    /// char.  This map holds that mapping
    let CharToKeyInputMap = 
        let inputs = 
            CoreKeyInputList
            |> Seq.map (fun ki -> OptionUtil.combine ki.RawChar ki )
            |> SeqUtil.filterToSome
            |> Seq.filter (fun (_,ki) -> not (VimKeyUtil.IsKeypadKey ki.Key))
        let map = inputs |> Map.ofSeq
        if map.Count <> Seq.length inputs then 
            failwith Resources.KeyInput_DuplicateCharRepresentation
        map

    let CharToKeyInput c = 
        match Map.tryFind c CharToKeyInputMap with
        | None -> KeyInput(VimKey.NotWellKnown, KeyModifiers.None, Some c)
        | Some(ki) -> ki

    /// Map of the VimKey + KeyModifiers to KeyInput values.  
    let VimKeyToKeyInputMap =
        CoreKeyInputList
        |> Seq.map (fun ki -> (ki.Key,ki.KeyModifiers),ki)
        |> Map.ofSeq

    let VimKeyToKeyInput vimKey = 
        match Map.tryFind (vimKey,KeyModifiers.None) VimKeyToKeyInputMap with
        | None -> invalidArg "vimKey" Resources.KeyInput_InvalidVimKey
        | Some(ki) -> ki

    let TryVimKeyAndModifiersToKeyInput vimKey keyModifiers = 
        match Map.tryFind (vimKey,keyModifiers) VimKeyToKeyInputMap with
        | Some(ki) -> Some ki
        | None -> 
            let shiftOnly = keyModifiers &&& KeyModifiers.Shift 
            match Map.tryFind (vimKey,shiftOnly) VimKeyToKeyInputMap with
            | Some(ki) -> KeyInput(ki.Key, keyModifiers, ki.RawChar) |> Some
            | None -> None

    let VimKeyAndModifiersToKeyInput vimKey keyModifiers = 
        match TryVimKeyAndModifiersToKeyInput vimKey keyModifiers with
        | Some(ki) -> ki
        | None ->
            let ki = VimKeyToKeyInput vimKey
            KeyInput(ki.Key, keyModifiers, ki.RawChar)

    let ChangeKeyModifiers (ki:KeyInput) keyModifiers = 
        match TryVimKeyAndModifiersToKeyInput ki.Key keyModifiers with
        | Some(ki) -> ki
        | None -> KeyInput(ki.Key, keyModifiers, ki.RawChar)

    let CharWithControlToKeyInput ch = 
        let ki = ch |> CharToKeyInput  
        ChangeKeyModifiers ki (ki.KeyModifiers ||| KeyModifiers.Control)

    let CharWithAltToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        ChangeKeyModifiers ki (ki.KeyModifiers ||| KeyModifiers.Alt)

