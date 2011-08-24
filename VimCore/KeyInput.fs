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

    static let _alternateEnterKeyInput = KeyInput(VimKey.LowerM, KeyModifiers.Control, Some 'm')
    static let _alternateEscapeKeyInput = KeyInput(VimKey.OpenBracket, KeyModifiers.Control, Some '[')
    static let _alternateLineFeedKeyInput = KeyInput(VimKey.LowerJ, KeyModifiers.Control, Some 'j')
    static let _alternateTabKeyInput = KeyInput(VimKey.LowerI, KeyModifiers.Control, Some 'i')
    static let _alternateKeyInputList = 
        [
            _alternateEnterKeyInput
            _alternateEscapeKeyInput
            _alternateTabKeyInput
            _alternateLineFeedKeyInput
        ]

    member x.Char = _literal |> OptionUtil.getOrDefault CharUtil.MinValue
    member x.RawChar = _literal
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = 
        match _key with
        | VimKey.Number0 -> true
        | VimKey.Number1 -> true
        | VimKey.Number2 -> true
        | VimKey.Number3 -> true
        | VimKey.Number4 -> true
        | VimKey.Number5 -> true
        | VimKey.Number6 -> true
        | VimKey.Number7 -> true
        | VimKey.Number8 -> true
        | VimKey.Number9 -> true
        | VimKey.Keypad0 -> true
        | VimKey.Keypad1 -> true
        | VimKey.Keypad2 -> true
        | VimKey.Keypad3 -> true
        | VimKey.Keypad4 -> true
        | VimKey.Keypad5 -> true
        | VimKey.Keypad6 -> true
        | VimKey.Keypad7 -> true
        | VimKey.Keypad8 -> true
        | VimKey.Keypad9 -> true
        | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match _key with
        | VimKey.Left -> true
        | VimKey.Right -> true
        | VimKey.Up -> true
        | VimKey.Down -> true
        | _ -> false

    member x.IsControlAndLetter = x.KeyModifiers = KeyModifiers.Control && CharUtil.IsLetter x.Char

    member x.GetAlternate () = 
        if x.KeyModifiers = KeyModifiers.None then
            match x.Key with
            | VimKey.Enter -> Some _alternateEnterKeyInput
            | VimKey.Escape -> Some _alternateEscapeKeyInput
            | VimKey.LineFeed -> Some _alternateLineFeedKeyInput
            | VimKey.Tab -> Some _alternateTabKeyInput
            | _ -> None
        else
            None

    /// In general Vim keys compare ordinally.  The one exception is when the control
    /// modifier is applied to a letter key.  In that case the keys compare in a case insensitive
    /// fashion
    member x.CompareTo (right : KeyInput) =

        let maybeGetAlternate (x : KeyInput) = 
            match x.GetAlternate() with
            | Some(alternate) -> alternate
            | None -> x
        let left = maybeGetAlternate x
        let right = maybeGetAlternate right

        if left.IsControlAndLetter then
            if right.IsControlAndLetter then 
                compare (CharUtil.ToLower left.Char) (CharUtil.ToLower right.Char)
            else 
                -1 
        elif right.IsControlAndLetter then 
            1
        else
            let comp = compare left.KeyModifiers right.KeyModifiers
            if comp <> 0 then comp
            else
                let comp = compare left.Char right.Char
                if comp <> 0 then comp
                else compare left.Key right.Key
                    
    override x.GetHashCode() = 
        match x.GetAlternate() with
        | Some(alternate) -> 
            alternate.GetHashCode()
        | None -> 
            let c = if x.IsControlAndLetter then CharUtil.ToLower x.Char else x.Char
            int32 c

    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other -> 0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.KeyModifiers);

    static member DefaultValue = KeyInput(VimKey.None, KeyModifiers.None, None)
    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other))

    static member AlternateEnterKey = _alternateEnterKeyInput
    static member AlternateEscapeKey = _alternateEscapeKeyInput
    static member AlternateTabKey = _alternateTabKeyInput
    static member AlternateLineFeedKey = _alternateLineFeedKeyInput
    static member AlternateKeyInputList = _alternateKeyInputList
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? KeyInput as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  

    interface System.IEquatable<KeyInput> with
        member x.Equals other = 0 = x.CompareTo other

    interface System.IComparable<KeyInput> with
        member x.CompareTo other = x.CompareTo other

module KeyInputUtil = 

    /// Mapping of all VimKey instances with their associated char if one exists
    let VimKeyRawData = [
        (VimKey.Back, Some '\b')
        (VimKey.FormFeed, Some '\f')
        (VimKey.Enter, Some '\n')
        (VimKey.Escape, 27uy |> CharUtil.OfAsciiValue |> Some)
        (VimKey.Left, None)
        (VimKey.Up, None)
        (VimKey.Right, None)
        (VimKey.Down, None)
        (VimKey.Delete, 127uy |> CharUtil.OfAsciiValue |> Some)
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
        (VimKey.LowerA, Some 'a')
        (VimKey.LowerB, Some 'b')
        (VimKey.LowerC, Some 'c')
        (VimKey.LowerD, Some 'd')
        (VimKey.LowerE, Some 'e')
        (VimKey.LowerF, Some 'f')
        (VimKey.LowerG, Some 'g')
        (VimKey.LowerH, Some 'h')
        (VimKey.LowerI, Some 'i')
        (VimKey.LowerJ, Some 'j')
        (VimKey.LowerK, Some 'k')
        (VimKey.LowerL, Some 'l')
        (VimKey.LowerM, Some 'm')
        (VimKey.LowerN, Some 'n')
        (VimKey.LowerO, Some 'o')
        (VimKey.LowerP, Some 'p')
        (VimKey.LowerQ, Some 'q')
        (VimKey.LowerR, Some 'r')
        (VimKey.LowerS, Some 's')
        (VimKey.LowerT, Some 't')
        (VimKey.LowerU, Some 'u')
        (VimKey.LowerV, Some 'v')
        (VimKey.LowerW, Some 'w')
        (VimKey.LowerX, Some 'x')
        (VimKey.LowerY, Some 'y')
        (VimKey.LowerZ, Some 'z')
        (VimKey.UpperA, Some 'A')
        (VimKey.UpperB, Some 'B')
        (VimKey.UpperC, Some 'C')
        (VimKey.UpperD, Some 'D')
        (VimKey.UpperE, Some 'E')
        (VimKey.UpperF, Some 'F')
        (VimKey.UpperG, Some 'G')
        (VimKey.UpperH, Some 'H')
        (VimKey.UpperI, Some 'I')
        (VimKey.UpperJ, Some 'J')
        (VimKey.UpperK, Some 'K')
        (VimKey.UpperL, Some 'L')
        (VimKey.UpperM, Some 'M')
        (VimKey.UpperN, Some 'N')
        (VimKey.UpperO, Some 'O')
        (VimKey.UpperP, Some 'P')
        (VimKey.UpperQ, Some 'Q')
        (VimKey.UpperR, Some 'R')
        (VimKey.UpperS, Some 'S')
        (VimKey.UpperT, Some 'T')
        (VimKey.UpperU, Some 'U')
        (VimKey.UpperV, Some 'V')
        (VimKey.UpperW, Some 'W')
        (VimKey.UpperX, Some 'X')
        (VimKey.UpperY, Some 'Y')
        (VimKey.UpperZ, Some 'Z')
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
        (VimKey.Dollar, Some '$')
        (VimKey.Tab, Some '\t')
        (VimKey.LineFeed, None) 
        (VimKey.Nop, None)]

    let VimKeyInputList  = 
        VimKeyRawData 
        |> Seq.map (fun (key,charOpt) -> KeyInput(key, KeyModifiers.None, charOpt )) 
        |> List.ofSeq

    let VimKeyCharList = 
        VimKeyInputList
        |> Seq.map (fun ki -> ki.RawChar)
        |> SeqUtil.filterToSome
        |> Set.ofSeq
        |> Set.toList

    /// Map for core characters to the KeyInput representation.  While several keys 
    /// may map to the same character their should be a primary KeyInput for every 
    /// char.  This map holds that mapping
    let CharToKeyInputMap = 

        let inputs = 
            VimKeyInputList
            |> Seq.map (fun ki -> OptionUtil.combine ki.RawChar ki )
            |> SeqUtil.filterToSome
            |> Seq.filter (fun (_,ki) -> not (VimKeyUtil.IsKeypadKey ki.Key))
        let map = inputs |> Map.ofSeq
        if map.Count <> Seq.length inputs then 
            failwith Resources.KeyInput_DuplicateCharRepresentation
        map

    let CharToKeyInput c = 
        match Map.tryFind c CharToKeyInputMap with
        | None -> KeyInput(VimKey.RawCharacter, KeyModifiers.None, Some c)
        | Some(ki) -> ki

    /// Map of the VimKey to KeyInput values.  
    let VimKeyToKeyInputMap =
        VimKeyInputList
        |> Seq.map (fun ki -> ki.Key,ki)
        |> Map.ofSeq

    let VimKeyToKeyInput vimKey = 
        match Map.tryFind vimKey VimKeyToKeyInputMap with
        | None -> invalidArg "vimKey" Resources.KeyInput_InvalidVimKey
        | Some(ki) -> ki

    let VimKeyAndModifiersToKeyInput vimKey keyModifiers = 
        let ki = VimKeyToKeyInput vimKey
        KeyInput(ki.Key, keyModifiers, ki.RawChar)

    let ChangeKeyModifiers (ki:KeyInput) keyModifiers = 
        KeyInput(ki.Key, keyModifiers, ki.RawChar)

    let CharWithControlToKeyInput ch = 
        let ki = ch |> CharToKeyInput  
        ChangeKeyModifiers ki (ki.KeyModifiers ||| KeyModifiers.Control)

    let CharWithAltToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        ChangeKeyModifiers ki (ki.KeyModifiers ||| KeyModifiers.Alt)

    let CharWithShiftToKeyInput ch = 
        let ki = ch |> CharToKeyInput  
        ChangeKeyModifiers ki (ki.KeyModifiers ||| KeyModifiers.Shift)

    let AlternateEnterKey = KeyInput.AlternateEnterKey
    let AlternateEscapeKey = KeyInput.AlternateEscapeKey
    let AlternateLineFeedKey = KeyInput.AlternateLineFeedKey
    let AlternateTabKey = KeyInput.AlternateTabKey
    let EscapeKey = VimKeyToKeyInput VimKey.Escape
    let TabKey = VimKeyToKeyInput VimKey.Tab
    let LineFeedKey = VimKeyToKeyInput VimKey.LineFeed
    let EnterKey = VimKeyToKeyInput VimKey.Enter

    let AlternateKeyInputList = KeyInput.AlternateKeyInputList

    let AlternateKeyInputPairList = 
        [
            (EscapeKey, AlternateEscapeKey)
            (TabKey, AlternateTabKey)
            (LineFeedKey, AlternateLineFeedKey)
            (EnterKey, AlternateEnterKey)
        ]

    let GetAlternate (ki : KeyInput) = ki.GetAlternate()

    let GetAlternateTarget (ki : KeyInput) = 
        AlternateKeyInputPairList 
        |> List.tryPick (fun (target, alternate) -> if alternate = ki then Some target else None)

