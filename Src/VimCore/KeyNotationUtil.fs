#light

namespace Vim
open System
open System.Collections.Generic

/// Utility class for converting Key notations into KeyInput and vice versa
/// :help key-notation for all of the key codes
module KeyNotationUtil =

    let ManualKeyList = 
        [
            ("<Nul>",KeyInputUtil.VimKeyToKeyInput VimKey.Null)
            ("<BS>", KeyInputUtil.VimKeyToKeyInput VimKey.Back)
            ("<Tab>",KeyInputUtil.TabKey)
            ("<NL>", KeyInputUtil.LineFeedKey)
            ("<FF>", KeyInputUtil.VimKeyToKeyInput VimKey.FormFeed)
            ("<CR>", KeyInputUtil.EnterKey)
            ("<Return>", KeyInputUtil.EnterKey)
            ("<Enter>", KeyInputUtil.EnterKey)
            ("<Esc>", KeyInputUtil.EscapeKey)
            ("<Space>", KeyInputUtil.CharToKeyInput ' ')
            ("<lt>", KeyInputUtil.CharToKeyInput '<')
            ("<Bslash>", KeyInputUtil.CharToKeyInput '\\' )
            ("<Bar>", KeyInputUtil.CharToKeyInput '|')
            ("<Del>", KeyInputUtil.VimKeyToKeyInput VimKey.Delete)
            ("<Up>", KeyInputUtil.VimKeyToKeyInput VimKey.Up)
            ("<Down>", KeyInputUtil.VimKeyToKeyInput VimKey.Down)
            ("<Left>", KeyInputUtil.VimKeyToKeyInput VimKey.Left)
            ("<Right>", KeyInputUtil.VimKeyToKeyInput VimKey.Right)
            ("<Help>", KeyInputUtil.VimKeyToKeyInput VimKey.Help)
            ("<Insert>", KeyInputUtil.VimKeyToKeyInput VimKey.Insert)
            ("<Home>", KeyInputUtil.VimKeyToKeyInput VimKey.Home)
            ("<End>", KeyInputUtil.VimKeyToKeyInput VimKey.End)
            ("<PageUp>", KeyInputUtil.VimKeyToKeyInput VimKey.PageUp)
            ("<PageDown>", KeyInputUtil.VimKeyToKeyInput VimKey.PageDown)
            ("<kHome>", KeyInputUtil.VimKeyToKeyInput VimKey.Home)
            ("<kEnd>", KeyInputUtil.VimKeyToKeyInput VimKey.End)
            ("<kPageUp>", KeyInputUtil.VimKeyToKeyInput VimKey.PageUp)
            ("<kPageDown>", KeyInputUtil.VimKeyToKeyInput VimKey.PageDown)
            ("<kMultiply>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadMultiply)
            ("<kPlus>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadPlus)
            ("<kMinus>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadMinus)
            ("<kDivide>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadDivide)
            ("<kPoint>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadDecimal)
            ("<kEnter>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadEnter)
            ("<k0>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad0)
            ("<k1>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad1)
            ("<k2>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad2)
            ("<k3>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad3)
            ("<k4>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad4)
            ("<k5>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad5)
            ("<k6>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad6)
            ("<k7>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad7)
            ("<k8>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad8)
            ("<k9>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad9)
            ("<nop>", KeyInputUtil.VimKeyToKeyInput VimKey.Nop)
            ("<LeftMouse>", KeyInputUtil.VimKeyToKeyInput VimKey.LeftMouse)
            ("<LeftDrag>", KeyInputUtil.VimKeyToKeyInput VimKey.LeftDrag)
            ("<LeftRelease>", KeyInputUtil.VimKeyToKeyInput VimKey.LeftRelease)
            ("<MiddleMouse>", KeyInputUtil.VimKeyToKeyInput VimKey.MiddleMouse)
            ("<MiddleDrag>", KeyInputUtil.VimKeyToKeyInput VimKey.MiddleDrag)
            ("<MiddleRelease>", KeyInputUtil.VimKeyToKeyInput VimKey.MiddleRelease)
            ("<RightMouse>", KeyInputUtil.VimKeyToKeyInput VimKey.RightMouse)
            ("<RightDrag>", KeyInputUtil.VimKeyToKeyInput VimKey.RightDrag)
            ("<RightRelease>", KeyInputUtil.VimKeyToKeyInput VimKey.RightRelease)
            ("<X1Mouse>", KeyInputUtil.VimKeyToKeyInput VimKey.X1Mouse)
            ("<X1Drag>", KeyInputUtil.VimKeyToKeyInput VimKey.X1Drag)
            ("<X1Release>", KeyInputUtil.VimKeyToKeyInput VimKey.X1Release)
            ("<X2Mouse>", KeyInputUtil.VimKeyToKeyInput VimKey.X2Mouse)
            ("<X2Drag>", KeyInputUtil.VimKeyToKeyInput VimKey.X2Drag)
            ("<X2Release>", KeyInputUtil.VimKeyToKeyInput VimKey.X2Release)
        ]

    let FunctionKeys = 
        [VimKey.F1;VimKey.F2;VimKey.F3;VimKey.F4;VimKey.F5;VimKey.F6;VimKey.F7;VimKey.F8;VimKey.F9;VimKey.F10;VimKey.F11;VimKey.F12]
            |> Seq.mapi (fun i k -> (i+1),k)
            |> Seq.map (fun (number,key) -> (sprintf "<F%d>" number),KeyInputUtil.VimKeyToKeyInput key)
            |> List.ofSeq

    /// Map of the special key names to their KeyInput representation.  Does not include the <> bookends
    /// Not supported
    /// TODO: Support these
    /// <CSI>		command sequence intro  ALT-Esc 155	*<CSI>*
    /// <xCSI>		CSI when typed in the GUI		*<xCSI>*
    /// <Undo>		undo key
    /// <EOL>
    let SpecialKeyMap = 
        ManualKeyList
        |> Seq.append FunctionKeys
        |> Seq.map (fun (tagName, value) -> 
            let key = CharSpan(tagName, 1, tagName.Length - 2, CharComparer.IgnoreCase)
            (key, value))
        |> Map.ofSeq

    /// Convert the given special key name + modifier into a KeyInput value 
    let ConvertSpecialKeyName (dataCharSpan : CharSpan) modifier = 

        let keyInput = 
            match Map.tryFind dataCharSpan SpecialKeyMap with
            | Some keyInput -> Some keyInput
            | None ->
                if dataCharSpan.Length = 1 then
                    // Happens when you have a modifier + single letter inside a tag.  Just use the
                    // character conversion here 
                    KeyInputUtil.CharToKeyInput (dataCharSpan.CharAt 0) |> Some
                else
                    None

        // Convert the string into a KeyInput value by consulting the 
        // SpecialKeyMap 
        match keyInput with
        | None -> None
        | Some keyInput -> 

            // This is one place where we don't want to always smooth out the modifiers with
            // ApplyModifiers.  Even though it doesn't make any sense to create a mapping
            // for say Shift + # it's completely legal to do so.  It produces a KeyInput which
            // can't be matched on keyboards.  But it will appear in the map list.  
            //
            // The exception to this rule is letters.  They are still smoothed out and this can
            // be verified experimentally
            if Option.isSome keyInput.RawChar && CharUtil.IsLetterOrDigit keyInput.Char then
                KeyInputUtil.ApplyModifiers keyInput modifier |> Some
            elif Util.IsFlagSet modifier KeyModifiers.Shift then
                KeyInputUtil.ChangeKeyModifiersDangerous keyInput modifier |> Some
            else
                KeyInputUtil.ApplyModifiers keyInput modifier |> Some

    /// Try and convert a <char-...> notation into a KeyInput value 
    let TryCharNotationToKeyInput (dataCharSpan : CharSpan) = 
        Contract.Assert(dataCharSpan.CharAt 0 = '<')
        Contract.Assert(dataCharSpan.CharAt (dataCharSpan.Length - 1) = '>')

        // The actual number string will begin immediately following the - in the 
        // notation and end at the >
        let prefixLength = 6
        let length = dataCharSpan.Length - (prefixLength + 1)
        let numberCharSpan = dataCharSpan.GetSubSpan 6 length
        let numberString = numberCharSpan.ToString()

        // TODO: Need to unify the handling of number parsing in this code base.  It's done here,
        // in CommandUtil for the <C-A> command and in SearchDataOffset
        let numberBase = 
            if numberString.StartsWith("0x") then
                16
            elif numberString.StartsWith("0") then
                8
            else
                10

        try
            let number = System.Convert.ToByte(numberString, numberBase)
            let c = CharUtil.OfAsciiValue number 
            KeyInputUtil.CharToKeyInput c |> Some
        with
            | _ -> None

    /// Try and convert a key notation that is bracketed with < and > to a KeyInput value
    let TryKeyNotationToKeyInput (dataCharSpan : CharSpan) = 
        Contract.Assert(dataCharSpan.CharAt 0 = '<')
        Contract.Assert(dataCharSpan.CharAt (dataCharSpan.Length - 1) = '>')

        let isCharLiteral = 
            if dataCharSpan.Length > 6 then
                let charSpan = dataCharSpan.GetSubSpan 0 6
                charSpan.EqualsString "<char-"
            else
                false

        if isCharLiteral then
            TryCharNotationToKeyInput dataCharSpan
        else
            let lastDashIndex = dataCharSpan.LastIndexOf '-'
            let rec inner index modifier = 
                if index >= dataCharSpan.Length then
                    None
                else if lastDashIndex >= 0 && index <= lastDashIndex then
                    let modifier = 
                        match dataCharSpan.CharAt index |> CharUtil.ToLower with
                        | 'c' -> KeyModifiers.Control ||| modifier |> Some
                        | 's' -> KeyModifiers.Shift ||| modifier |> Some
                        | 'a' -> KeyModifiers.Alt ||| modifier |> Some
                        | 'm' -> KeyModifiers.Alt ||| modifier |> Some
                        | 'd' -> KeyModifiers.Command ||| modifier |> Some
                        | '-' -> Some modifier
                        | _ -> None
                    match modifier with
                    | Some modifier -> inner (index + 1) modifier
                    | None -> None
                else 
                    // Need to remove the final > before converting.  
                    let length = (dataCharSpan.Length - index) - 1 
                    let tagCharSpan = dataCharSpan.GetSubSpan index length
                    ConvertSpecialKeyName tagCharSpan modifier
            inner 1 KeyModifiers.None

    /// Try and convert the given string value into a KeyInput.  If the value is wrapped in 
    /// < and > then it will be interpreted as a special key modifier as defined by 
    /// :help key-notation
    let TryStringToKeyInputCore (dataCharSpan : CharSpan) =
        if dataCharSpan.Length = 0 then
            None
        elif dataCharSpan.CharAt 0 = '<' then
            let closeIndex = dataCharSpan.IndexOf '>'
            if closeIndex < 0 then
                (KeyInputUtil.CharToKeyInput '<', 1) |> Some
            else 
                let tag = dataCharSpan.GetSubSpan 0 (closeIndex + 1)
                match TryKeyNotationToKeyInput tag with
                | Some keyInput -> (keyInput, tag.Length) |> Some
                | None -> (KeyInputUtil.CharToKeyInput '<', 1) |> Some
        else
            let keyInput = KeyInputUtil.CharToKeyInput (dataCharSpan.CharAt 0)
            (keyInput, 1) |> Some

    /// Try and convert the given string value into a single KeyInput value.  If the conversion
    /// fails or is more than 1 KeyInput in length then None will be returned
    let TryStringToKeyInput (data : string) = 
        let dataCharSpan = CharSpan(data, CharComparer.IgnoreCase)
        match TryStringToKeyInputCore dataCharSpan with
        | Some (keyInput, length) ->
            if length = data.Length then
                Some keyInput
            else
                None
        | None -> None

    let StringToKeyInput data = 
        match TryStringToKeyInput data with 
        | Some ki -> ki
        | None -> invalidArg "data" (Resources.KeyNotationUtil_InvalidNotation data)

    let TryStringToKeyInputSet data = 
        let rec inner index acc =
            let dataCharSpan = CharSpan(data, index, data.Length - index, CharComparer.IgnoreCase)
            match TryStringToKeyInputCore dataCharSpan with
            | None -> acc [] 
            | Some (keyInput, length) ->
                if index + length = data.Length then
                    acc [keyInput]
                else
                    let index = index + length
                    inner index (fun tail -> acc (keyInput :: tail))

        match inner 0 (fun x -> x) with
        | [] -> None
        | list -> Some (KeyInputSetUtil.OfList list) 

    /// Convert the String to a KeyInputSet 
    let StringToKeyInputSet data = 
        match TryStringToKeyInputSet data with 
        | Some list -> list
        | None -> invalidArg "data" (Resources.KeyNotationUtil_InvalidNotation data)

    let TryGetSpecialKeyName (keyInput : KeyInput) = 
        let found = 
            SpecialKeyMap
            |> Seq.tryFind (fun pair -> 
                let specialInput = pair.Value
                specialInput.Key = keyInput.Key && 
                specialInput.Char = keyInput.Char &&
                specialInput.KeyModifiers = (specialInput.KeyModifiers &&& keyInput.KeyModifiers))

        match found with 
        | None -> None
        | Some pair ->
            let extra : KeyModifiers = Util.UnsetFlag keyInput.KeyModifiers pair.Value.KeyModifiers
            Some (pair.Key.ToString(), extra)

    /// Get the display name for the specified KeyInput value.
    let GetDisplayName (keyInput : KeyInput) = 

        let inner name keyModifiers forceBookend = 
            let rec getPrefix current keyModifiers = 
                if Util.IsFlagSet keyModifiers KeyModifiers.Control then
                    let current = current + "C-"
                    let keyModifiers = Util.UnsetFlag keyModifiers KeyModifiers.Control
                    getPrefix current keyModifiers
                elif Util.IsFlagSet keyModifiers KeyModifiers.Alt then
                    let current = current + "A-"
                    let keyModifiers = Util.UnsetFlag keyModifiers KeyModifiers.Alt
                    getPrefix current keyModifiers
                elif Util.IsFlagSet keyModifiers KeyModifiers.Shift then
                    let current = current + "S-"
                    let keyModifiers = Util.UnsetFlag keyModifiers KeyModifiers.Shift
                    getPrefix current keyModifiers
                elif Util.IsFlagSet keyModifiers KeyModifiers.Command then
                    let current = current + "D-"
                    let keyModifiers = Util.UnsetFlag keyModifiers KeyModifiers.Command
                    getPrefix current keyModifiers
                else 
                    current

            let prefix = getPrefix "" keyModifiers
            if StringUtil.isNullOrEmpty prefix then
                if forceBookend then
                    sprintf "<%s>" name
                else
                    name
            else
                sprintf "<%s%s>" prefix name

        // Check to see if this is one of the keys shifted by control for which we provide a better
        // display
        let checkForSpecialControl () = 
            match keyInput.RawChar with
            | None -> inner "" keyInput.KeyModifiers false
            | Some c ->
                let value = int c;
                if value >= 1 && value <= 26 then
                    let baseCode = value - 1 
                    let name = char ((int 'A') + baseCode) |> StringUtil.ofChar
                    let keyModifiers = keyInput.KeyModifiers ||| KeyModifiers.Control
                    inner name keyModifiers false
                else
                    inner (c |> StringUtil.ofChar) keyInput.KeyModifiers false

        match TryGetSpecialKeyName keyInput with 
        | Some (name, modifiers) -> inner name modifiers true
        | None -> checkForSpecialControl ()



