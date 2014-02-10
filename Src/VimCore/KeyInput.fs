#light

namespace Vim
open System.Runtime.InteropServices

[<Sealed>]
type KeyInput
    (
        _key : VimKey,
        _modKey : KeyModifiers, 
        _literal : char option 
    ) =

    member x.Char = _literal |> OptionUtil.getOrDefault CharUtil.MinValue
    member x.RawChar = _literal
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = 
        match _literal with
        | Some c -> CharUtil.IsDigit c
        | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = VimKeyUtil.IsArrowKey _key

    /// Is this a function key
    member x.IsFunctionKey = VimKeyUtil.IsFunctionKey _key

    /// Is this a mouse key
    member x.IsMouseKey = VimKeyUtil.IsMouseKey _key

    /// In general Vim keys compare ordinally.  The one exception is when the control
    /// modifier is applied to a letter key.  In that case the keys compare in a case 
    /// insensitive fashion.  
    ///
    /// This is demonstratable in a couple of areas.  One simple one is using the 
    /// the CTRL-F command (scroll down).  It has the same behavior with capital 
    /// or lower case F.
    member x.CompareTo (right : KeyInput) =
        if obj.ReferenceEquals(right, null) then
            1
        else 
            let left = x
            let comp = compare left.KeyModifiers right.KeyModifiers
            if comp <> 0 then 
                comp
            else
                let comp = compare left.Char right.Char
                if comp <> 0 then comp
                else compare left.Key right.Key
                    
    override x.GetHashCode() = 
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

    [<Literal>]
    let CharLettersLower = "abcdefghijklmnopqrstuvwxyz"

    [<Literal>]
    let CharLettersUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"

    [<Literal>]
    let CharLettersExtra = " !@#$%^&*()[]{}-_=+\\|'\",<>./?:;`~1234567890"

    let RawCharData = CharLettersLower + CharLettersUpper + CharLettersExtra

    /// Mapping of all VimKey instances with their associated char if one exists.  
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
        (VimKey.KeypadEnter, None)
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
        (VimKey.LineFeed, Some (CharUtil.OfAsciiValue 10uy))
        (VimKey.Null, Some (CharUtil.OfAsciiValue 0uy))
        (VimKey.Tab, Some '\t')
        (VimKey.Nop, None)
        (VimKey.LeftMouse, None)
        (VimKey.LeftDrag, None)
        (VimKey.LeftRelease, None)
        (VimKey.MiddleMouse, None)
        (VimKey.MiddleDrag, None)
        (VimKey.MiddleRelease, None)
        (VimKey.RightMouse, None)
        (VimKey.RightDrag, None)
        (VimKey.RightRelease, None)
        (VimKey.X1Mouse, None)
        (VimKey.X1Drag, None)
        (VimKey.X1Release, None)
        (VimKey.X2Mouse, None)
        (VimKey.X2Drag, None)
        (VimKey.X2Release, None)]

    /// This is a mapping of the supported control character mappings in vim.  The list of
    /// supported items is described here
    ///
    /// http://vimhelp.appspot.com/vim_faq.txt.html#faq-20.5
    ///
    /// This map is useful when applying a Control modifier to char value.  It produces the 
    /// Vim character expected for such an application
    let ControlCharToKeyInputMap = 

        // First build up the alpha characters
        let alpha = 
            seq {
                let baseChar = int 'a'
                let baseCode = 0x1
                for i = 0 to 25 do

                    let letter = char (baseChar + i)
                    let vimKey = 
                        match letter with
                        | 'j' -> VimKey.LineFeed
                        | 'l' -> VimKey.FormFeed
                        | 'm' -> VimKey.Enter
                        | 'i' -> VimKey.Tab
                        | _ -> VimKey.RawCharacter

                    let c = 
                        let code = baseCode + i
                        char code

                    yield (letter, KeyInput(vimKey, KeyModifiers.None, Some c))
            }

        let other = 
            [
                ('@', VimKey.Null, 0x00)    // <Null>
                ('[', VimKey.Escape, 0x1B)  // <Escape>
                (']', VimKey.RawCharacter, 0x1D)
                ('^', VimKey.RawCharacter, 0x1E)
                ('_', VimKey.RawCharacter, 0x1F)
                ('?', VimKey.RawCharacter, 0x7F)
            ]
            |> Seq.map (fun (c, key, code) -> 
                let controlChar = CharUtil.OfAsciiValue (byte code)
                let keyInput = KeyInput(key, KeyModifiers.None, Some controlChar)
                (c, keyInput))

        Seq.append alpha other
        |> Map.ofSeq

    /// This is the set of predefined KeyInput values that Vim chars about 
    let VimKeyInputList = 

        let rawSeq = 
            RawCharData
            |> Seq.map (fun c -> KeyInput(VimKey.RawCharacter, KeyModifiers.None, Some c))

        let standardSeq = 
            VimKeyRawData 
            |> Seq.map (fun (key,charOpt) -> KeyInput(key, KeyModifiers.None, charOpt)) 

        // When mapping the control keys only take the primary keys.  Don't take any alternates because their
        // character is owned by a combination in the standard sequence. 
        //
        // Only map RawCharacter values here.  Anything which isn't a RawCharacter is already present in the 
        // VimKeyRawData list
        let controlSeq =
            ControlCharToKeyInputMap 
            |> Seq.map (fun pair -> pair.Value)
            |> Seq.filter (fun keyInput -> keyInput.Key = VimKey.RawCharacter)

        rawSeq
        |> Seq.append standardSeq
        |> Seq.append controlSeq
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
    ///
    /// Even though key-notation lists <Del> and <Backslash> as being equivalents for
    /// chars they aren't in practice.  When mapping by char we don't want to bind to
    /// these values and instead want the char version instead.  Should only hit these
    /// when binding by name
    let CharToKeyInputMap = 
        let inputs = 
            VimKeyInputList
            |> Seq.map (fun ki -> OptionUtil.combine ki.RawChar ki )
            |> SeqUtil.filterToSome
            |> Seq.filter (fun (_,ki) -> 
                match ki.Key with
                | VimKey.Back -> false
                | VimKey.Delete -> false
                | _ -> not (VimKeyUtil.IsKeypadKey ki.Key))
            |> List.ofSeq

#if DEBUG
        let mutable debugMap : Map<char, KeyInput> = Map.empty
        for tuple in inputs do
            let c = fst tuple
            let found = Map.tryFind c debugMap
            if Option.isSome found then
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

    let NullKey = VimKeyToKeyInput VimKey.Null
    let LineFeedKey = VimKeyToKeyInput VimKey.LineFeed
    let FormFeedKey = VimKeyToKeyInput VimKey.FormFeed
    let EscapeKey = VimKeyToKeyInput VimKey.Escape
    let EnterKey = VimKeyToKeyInput VimKey.Enter
    let TabKey = CharToKeyInput '\t'

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
                elif (int c) < 0x100 && not (CharUtil.IsControl c) && c <> ' ' then
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
                match keyInput.RawChar with
                | None -> keyInput
                | Some c ->
                    // When searching for the control keys make sure to look at the lower case
                    // letters if this is a case of the shift modifier
                    let c =
                        if CharUtil.IsUpperLetter c then
                            CharUtil.ToLower c
                        else
                            c

                    // Map the known control cases back to the Vim defined behavior.  This mapping intentionally removes any
                    // Shift modifiers.  No matter how CTRL-Q is produced the shift modifier isn't present on the vim key
                    match Map.tryFind c ControlCharToKeyInputMap with
                    | None -> keyInput
                    | Some keyInput -> 
                        let modifiers = Util.UnsetFlag keyInput.KeyModifiers KeyModifiers.Control
                        ChangeKeyModifiersDangerous keyInput modifiers

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

    let ApplyModifiersToChar c modifiers = 
        let keyInput = CharToKeyInput c
        ApplyModifiers keyInput modifiers

    let CharWithControlToKeyInput ch = 
        let keyInput = ch |> CharToKeyInput  
        ApplyModifiers keyInput KeyModifiers.Control

    let CharWithAltToKeyInput ch = 
        let keyInput = ch |> CharToKeyInput 
        ApplyModifiers keyInput KeyModifiers.Alt

    let GetNonKeypadEquivalent (keyInput : KeyInput) = 

        let apply c = 
            let keyInput = CharToKeyInput c
            ApplyModifiers keyInput keyInput.KeyModifiers |> Some

        match keyInput.Key with
        | VimKey.Keypad0 -> apply '0'
        | VimKey.Keypad1 -> apply '1'
        | VimKey.Keypad2 -> apply '2'
        | VimKey.Keypad3 -> apply '3'
        | VimKey.Keypad4 -> apply '4'
        | VimKey.Keypad5 -> apply '5'
        | VimKey.Keypad6 -> apply '6'
        | VimKey.Keypad7 -> apply '7'
        | VimKey.Keypad8 -> apply '8'
        | VimKey.Keypad9 -> apply '9'
        | VimKey.KeypadDecimal -> apply '.'
        | VimKey.KeypadDivide -> apply '/'
        | VimKey.KeypadMinus -> apply '-'
        | VimKey.KeypadMultiply -> apply '*'
        | VimKey.KeypadPlus -> apply '+'
        | VimKey.KeypadEnter -> ApplyModifiers EnterKey keyInput.KeyModifiers |> Some
        | _ -> None

