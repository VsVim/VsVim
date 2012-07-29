#light

namespace Vim
open System.Runtime.InteropServices

type VirtualKeyCode = int

[<Sealed>]
type KeyInput
    (
        _key:VimKey,
        _modKey:KeyModifiers, 
        _literal:char option 
    ) =

    static let _alternateNullKeyInput = KeyInput(VimKey.AtSign, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 0uy))
    static let _alternateBackspaceKeyInput = KeyInput(VimKey.LowerH, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 8uy))
    static let _alternateTabKeyInput = KeyInput(VimKey.LowerI, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 9uy))
    static let _alternateLineFeedKeyInput = KeyInput(VimKey.LowerJ, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 10uy))
    static let _alternateFormFeedKeyInput = KeyInput(VimKey.LowerL, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 12uy))
    static let _alternateEnterKeyInput = KeyInput(VimKey.LowerM, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 13uy))
    static let _alternateEscapeKeyInput = KeyInput(VimKey.OpenBracket, KeyModifiers.Control, Some (CharUtil.OfAsciiValue 27uy))
    static let _alternateKeyInputList = 
        [
            _alternateNullKeyInput
            _alternateBackspaceKeyInput
            _alternateTabKeyInput
            _alternateLineFeedKeyInput
            _alternateFormFeedKeyInput
            _alternateEnterKeyInput
            _alternateEscapeKeyInput
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

    /// Is this a function key
    member x.IsFunctionKey =    
        match _key with
        | VimKey.F1 -> true
        | VimKey.F2 -> true
        | VimKey.F3 -> true
        | VimKey.F4 -> true
        | VimKey.F5 -> true
        | VimKey.F6 -> true
        | VimKey.F7 -> true
        | VimKey.F8 -> true
        | VimKey.F9 -> true
        | VimKey.F10 -> true
        | VimKey.F11 -> true
        | VimKey.F12 -> true
        | _ -> false

    member x.GetAlternate () = 
        if x.KeyModifiers = KeyModifiers.None then
            match x.Key with
            | VimKey.Null -> Some _alternateNullKeyInput
            | VimKey.Back -> Some _alternateBackspaceKeyInput
            | VimKey.Enter -> Some _alternateEnterKeyInput
            | VimKey.Escape -> Some _alternateEscapeKeyInput
            | VimKey.LineFeed -> Some _alternateLineFeedKeyInput
            | VimKey.FormFeed -> Some _alternateFormFeedKeyInput
            | VimKey.Tab -> Some _alternateTabKeyInput
            | _ -> None
        else
            None

    /// In general Vim keys compare ordinally.  The one exception is when the control
    /// modifier is applied to a letter key.  In that case the keys compare in a case 
    /// insensitive fashion.  
    ///
    /// This is demonstratable in a couple of areas.  One simple one is using the 
    /// the CTRL-F command (scroll down).  It has the same behavior with capital 
    /// or lower case F.
    member x.CompareTo (right : KeyInput) =

        let maybeGetAlternate (x : KeyInput) = 
            match x.GetAlternate() with
            | Some alternate -> alternate
            | None -> x
        let left = maybeGetAlternate x
        let right = maybeGetAlternate right

        let comp = compare left.KeyModifiers right.KeyModifiers
        if comp <> 0 then comp
        else
            let comp = compare left.Char right.Char
            if comp <> 0 then comp
            else compare left.Key right.Key
                    
    override x.GetHashCode() = 
        match x.GetAlternate() with
        | Some alternate -> 
            alternate.GetHashCode()
        | None -> 
            let c = x.Char
            int32 c

    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other -> 0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.KeyModifiers);

    static member DefaultValue = KeyInput(VimKey.None, KeyModifiers.None, None)
    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other))

    static member AlternateNullKeyInput = _alternateNullKeyInput
    static member AlternateBackspaceKeyInput = _alternateBackspaceKeyInput
    static member AlternateTabKeyInput = _alternateTabKeyInput
    static member AlternateLineFeedKeyInput = _alternateLineFeedKeyInput
    static member AlternateFormFeedKeyInput = _alternateFormFeedKeyInput
    static member AlternateEnterKeyInput = _alternateEnterKeyInput
    static member AlternateEscapeKeyInput = _alternateEscapeKeyInput
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
        (VimKey.Enter, Some (CharUtil.OfAsciiValue 13uy))
        (VimKey.Escape, Some (CharUtil.OfAsciiValue 27uy))
        (VimKey.Left, None)
        (VimKey.Up, None)
        (VimKey.Right, None)
        (VimKey.Down, None)
        (VimKey.Delete, Some (CharUtil.OfAsciiValue 127uy))
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
        (VimKey.LineFeed, Some (CharUtil.OfAsciiValue 10uy))
        (VimKey.Null, Some (CharUtil.OfAsciiValue 0uy))
        (VimKey.Nop, None)]

    /// This is a mapping of the supported control character mappings in vim.  The list of
    /// supported items is described here
    ///
    /// http://vimhelp.appspot.com/vim_faq.txt.html#faq-20.5
    let VimKeyWithControlToCharMap = 

        // First build up the alpha characters
        let alpha = 
            seq {
                let baseKey = int VimKey.LowerA
                let baseCode = 0x1
                for i = 0 to 25 do
                    let key : VimKey = 
                        let code = baseKey + i
                        enum code
                    let c = 
                        let code = baseCode + i
                        CharUtil.OfAsciiValue (byte code)

                    let isAlternate = 
                        match key with 
                        | VimKey.LowerH -> true
                        | VimKey.LowerI -> true
                        | VimKey.LowerJ -> true
                        | VimKey.LowerL -> true
                        | VimKey.LowerM -> true
                        | _ -> false

                    yield (key, (c, isAlternate))
            }

        let other = 
            [
                (VimKey.AtSign, 0x00, true)
                (VimKey.OpenBracket, 0x1B, true)
                (VimKey.Backslash, 0x1C, false)
                (VimKey.CloseBracket, 0x1D, false)
                (VimKey.Caret, 0x1E, false)
                (VimKey.Underscore, 0x1F, false)
                (VimKey.Question, 0x7F, true)
            ]
            |> Seq.map (fun (key, code, isAlternate) -> (key, ((CharUtil.OfAsciiValue (byte code)), isAlternate)))

        Seq.append alpha other
        |> Map.ofSeq

    /// This is the set of predefined KeyInput values that Vim chars about 
    let VimKeyInputList = 
        let standardSeq = 
            VimKeyRawData 
            |> Seq.map (fun (key,charOpt) -> KeyInput(key, KeyModifiers.None, charOpt)) 

        // When mapping the control keys only take the primary keys.  Don't take any alternates because their
        // character is owned by a combination in the standard sequence
        let controlSeq =
            VimKeyWithControlToCharMap
            |> Seq.filter (fun pair -> not (snd pair.Value))
            |> Seq.map (fun pair -> KeyInput(pair.Key, KeyModifiers.Control, Some (fst pair.Value)))

        Seq.append standardSeq controlSeq |> List.ofSeq

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
            |> List.ofSeq

#if DEBUG
        let mutable debugMap : Map<char, KeyInput> = Map.empty
        for tuple in inputs do
            let c = fst tuple
            let found = Map.containsKey c debugMap
            if found then
                System.Diagnostics.Debug.Fail("This is the failure")
            debugMap <- Map.add c (snd tuple) debugMap
#endif

        let map = inputs |> Map.ofSeq
        if map.Count <> inputs.Length then
            failwith Resources.KeyInput_DuplicateCharRepresentation
        map

    let CharToKeyInput c = 
        match Map.tryFind c CharToKeyInputMap with
        | None -> KeyInput(VimKey.RawCharacter, KeyModifiers.None, Some c)
        | Some(ki) -> ki

    /// Map of the VimKey to KeyInput values.  
    let VimKeyToKeyInputMap =
        VimKeyInputList
        |> Seq.filter (fun keyInput -> keyInput.KeyModifiers = KeyModifiers.None)
        |> Seq.map (fun keyInput -> keyInput.Key, keyInput)
        |> Map.ofSeq

    let VimKeyToKeyInput vimKey = 
        match Map.tryFind vimKey VimKeyToKeyInputMap with
        | None -> invalidArg "vimKey" Resources.KeyInput_InvalidVimKey
        | Some(ki) -> ki

    let ChangeKeyModifiersDangerous (ki:KeyInput) keyModifiers = 
        KeyInput(ki.Key, keyModifiers, ki.RawChar)

    let AlternateNullKeyInput = KeyInput.AlternateNullKeyInput
    let AlternateBackspaceKeyInput = KeyInput.AlternateBackspaceKeyInput
    let AlternateTabKey = KeyInput.AlternateTabKeyInput
    let AlternateLineFeedKey = KeyInput.AlternateLineFeedKeyInput
    let AlternateFormFeedKey = KeyInput.AlternateFormFeedKeyInput
    let AlternateEnterKey = KeyInput.AlternateEnterKeyInput
    let AlternateEscapeKey = KeyInput.AlternateEscapeKeyInput

    let NullKey = VimKeyToKeyInput VimKey.Null
    let BackspaceKey = VimKeyToKeyInput VimKey.Back
    let TabKey = VimKeyToKeyInput VimKey.Tab
    let LineFeedKey = VimKeyToKeyInput VimKey.LineFeed
    let FormFeedKey = VimKeyToKeyInput VimKey.FormFeed
    let EscapeKey = VimKeyToKeyInput VimKey.Escape
    let EnterKey = VimKeyToKeyInput VimKey.Enter

    let AlternateKeyInputList = KeyInput.AlternateKeyInputList

    let AlternateKeyInputPairList = 
        [
            (NullKey, AlternateNullKeyInput)
            (BackspaceKey, AlternateBackspaceKeyInput)
            (TabKey, AlternateTabKey)
            (LineFeedKey, AlternateLineFeedKey)
            (FormFeedKey, AlternateFormFeedKey)
            (EscapeKey, AlternateEscapeKey)
            (EnterKey, AlternateEnterKey)
        ]

    let GetAlternate (keyInput : KeyInput) = keyInput.GetAlternate()

    let GetPrimary (keyInput : KeyInput) = 
        AlternateKeyInputPairList 
        |> List.tryPick (fun (target, alternate) -> if alternate = keyInput then Some target else None)

    /// There is a set of characters to which the shift modifier is meaningless.  When the shift
    /// key is held down for these keys it's essentially ignored
    ///
    /// Note: This isn't documented anywhere I can find.  It's all ferreted out by experimentation
    /// particularly on international keyboards where you can get certain characters with and
    /// without the shift key.
    ///
    /// The experiments are done by mapping the character with shift in normal mode and seeing if 
    /// I can get the key mapping to hit.  For example :nmap <S->> ihit<Esc> doesn't work but
    /// :nmap <S-Del> ihit<Esc> does.  
    let ModifierSpecialCharSet = 
        VimKeyRawData
        |> Seq.filter (fun (vimKey, c) -> 
            match c with 
            | None -> false
            | Some _ ->
                // In general if there is an associated character then the shift key doesn't 
                // matter.  There are several notable exceptions.  Mostly for characters which aren't
                // considered printable
                match vimKey with
                | VimKey.Back -> false 
                | VimKey.Enter -> false
                | VimKey.FormFeed -> false
                | VimKey.Escape -> false
                | VimKey.Space -> false
                | VimKey.Delete -> false
                | VimKey.Tab -> false
                | _ -> true)
        |> Seq.map snd
        |> SeqUtil.filterToSome
        |> Set.ofSeq

    /// Apply the modifiers to the given KeyInput and determine the result.  This will
    /// not necessarily return a KeyInput with the modifier set.  It attempts to unify 
    /// certain ambiguous combinations.
    let ApplyModifiers (keyInput : KeyInput) (targetModifiers : KeyModifiers) =

        let normalizeShift (keyInput : KeyInput) =
            match keyInput.RawChar with
            | None -> keyInput
            | Some c ->
                if CharUtil.IsLetter c then
                    // The shift key and letters is ambiguous.  It can be represented equally well as 
                    // either of the following
                    //
                    //  - Lower case 'a' + shift
                    //  - Upper case 'A' with no shift
                    //
                    // Vim doesn't distinguish between these two and unifies internally.  This can be 
                    // demonstrated by playing with key mapping combinations (<S-A> and A).  It's 
                    // convenient to have upper 'A' as a stand alone VimKey hence we choose to represent
                    // that way and remove the shift modifier here
                    let modifiers = Util.UnsetFlag keyInput.KeyModifiers KeyModifiers.Shift
                    let keyInput = 
                        if CharUtil.IsLower keyInput.Char then

                            // The shift modifier should promote a letter into the upper form 
                            let c = CharUtil.ToUpper keyInput.Char
                            let upperKeyInput = CharToKeyInput c 
                            ChangeKeyModifiersDangerous upperKeyInput keyInput.KeyModifiers
                        else
                            // Ignore the shift modifier on anything which is not considered lower
                            keyInput

                    // Apply the remaining modifiers
                    ChangeKeyModifiersDangerous keyInput modifiers
                elif Set.contains keyInput.Char ModifierSpecialCharSet then
                    // There is a set of chars for which the Shift modifier has no effect.  If this is one of them then
                    // don't apply the shift modifier to the final KeyInput
                    let modifiers = Util.UnsetFlag keyInput.KeyModifiers KeyModifiers.Shift
                    ChangeKeyModifiersDangerous keyInput modifiers
                else
                    // Nothing special to do here
                    keyInput

        let normalizeControl (keyInput : KeyInput) = 
            if Util.IsFlagSet keyInput.KeyModifiers KeyModifiers.Alt then
                keyInput
            else
                // When searching for the control keys make sure to look at the lower case
                // letters if this is a case of the shift modifier
                let searchKey = 
                    if CharUtil.IsUpperLetter keyInput.Char then
                        let lower = CharUtil.ToLower keyInput.Char
                        let lowerKeyInput = CharToKeyInput lower
                        lowerKeyInput.Key
                    else
                        keyInput.Key

                // Map the known control cases back to the Vim defined behavior.  This mapping intentionally removes any
                // Shift modifiers.  No matter how CTRL-Q is produced the shift modifier isn't present on the vim key
                match Map.tryFind searchKey VimKeyWithControlToCharMap with
                | None -> keyInput
                | Some (c, isAlternate) -> 
                    // If this is an alternate for a standard key then use the primary when doing the lookup
                    if isAlternate then
                        match Map.tryFind c CharToKeyInputMap with
                        | Some keyInput -> keyInput 
                        | None -> keyInput
                    else
                        KeyInput(searchKey, KeyModifiers.Control, Some c)

        let normalizeAlt (keyInput : KeyInput) =
            match keyInput.RawChar with
            | None -> keyInput
            | Some c ->
                let number = int c
                if number < 0x80 then
                    // These keys are shifted to have the high bit set to include Alt.  At this point they don't
                    // represent the original VimKey anymore (á isn't VimKey.LowerA or VimKey.UpperA) so choose
                    // the RawCharacter instead
                    let number = number ||| 0x80
                    let c = char number
                    let modifiers = Util.UnsetFlag keyInput.KeyModifiers KeyModifiers.Alt
                    KeyInput(VimKey.RawCharacter, modifiers, Some c)
                else
                    // Nothing special to do here
                    keyInput

        let keyInput = ChangeKeyModifiersDangerous keyInput (targetModifiers ||| keyInput.KeyModifiers)

        // First normalize the shift case
        let keyInput = 
            if Util.IsFlagSet targetModifiers KeyModifiers.Shift then
                normalizeShift keyInput
            else 
                keyInput

        // Next normalize the control case
        let keyInput = 
            if Util.IsFlagSet targetModifiers KeyModifiers.Control then
                normalizeControl keyInput
            else
                keyInput

        let keyInput =
            if Util.IsFlagSet targetModifiers KeyModifiers.Alt then
                normalizeAlt keyInput
            else 
                keyInput

        keyInput

    let ApplyModifiersToVimKey vimKey modifiers = 
        let keyInput = VimKeyToKeyInput vimKey
        ApplyModifiers keyInput modifiers

    let CharWithControlToKeyInput ch = 
        let keyInput = ch |> CharToKeyInput  
        ApplyModifiers keyInput KeyModifiers.Control

    let CharWithAltToKeyInput ch = 
        let keyInput = ch |> CharToKeyInput 
        ApplyModifiers keyInput KeyModifiers.Alt

    let GetNonKeypadEquivalent (keyInput : KeyInput) = 

        let apply vimKey = ApplyModifiersToVimKey vimKey keyInput.KeyModifiers |> Some

        match keyInput.Key with
        | VimKey.Keypad0 -> apply VimKey.Number0
        | VimKey.Keypad1 -> apply VimKey.Number1
        | VimKey.Keypad2 -> apply VimKey.Number2
        | VimKey.Keypad3 -> apply VimKey.Number3
        | VimKey.Keypad4 -> apply VimKey.Number4
        | VimKey.Keypad5 -> apply VimKey.Number5
        | VimKey.Keypad6 -> apply VimKey.Number6
        | VimKey.Keypad7 -> apply VimKey.Number7
        | VimKey.Keypad8 -> apply VimKey.Number8
        | VimKey.Keypad9 -> apply VimKey.Number9
        | VimKey.KeypadDecimal -> apply VimKey.Period
        | VimKey.KeypadDivide -> apply VimKey.Forwardslash
        | VimKey.KeypadMinus -> apply VimKey.Minus
        | VimKey.KeypadMultiply -> apply VimKey.Asterick
        | VimKey.KeypadPlus -> apply VimKey.Plus
        | _ -> None

