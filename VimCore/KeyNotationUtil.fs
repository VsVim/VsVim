#light

namespace Vim

/// Thin wrapper around System.String which takes a specific comparer value
/// Useful for creating a case insensitive string key for a Map
type ComparableString ( _value : string, _comparer : System.StringComparer ) = 

    member x.Value = _value
                    
    override x.GetHashCode() = _value.GetHashCode()
    override x.Equals(obj) =
        match obj with
        | :? ComparableString as other -> _comparer.Equals(_value, other.Value)
        | _ -> false

    override x.ToString() = _value

    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? ComparableString as other -> _comparer.Compare(_value, other.Value)
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  

    interface System.IEquatable<KeyInput> with
        member x.Equals other = _comparer.Equals(_value, other)

    static member CreateOrdinalIgnoreCase value = ComparableString(value, System.StringComparer.OrdinalIgnoreCase)

/// Utility class for converting Key notations into KeyInput and vice versa
/// :help key-notation for all of the key codes
///
/// TODO: Too many string allocations in this type.  Need to cut that down by passing
/// around a CharSpan style type instead of individual string entries
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
        |> Seq.map (fun (k,v) -> k.Substring(1,k.Length-2), v)
        |> Seq.map (fun (k,v) -> (ComparableString.CreateOrdinalIgnoreCase k),v)
        |> Map.ofSeq

    /// Convert the given special key name + modifier into a KeyInput value 
    let ConvertSpecialKeyName data modifier = 

        // Convert the string into a KeyInput value by consulting the 
        // SpecialKeyMap 
        let keyInput = 
            let comparableData = ComparableString.CreateOrdinalIgnoreCase data
            match Map.tryFind comparableData SpecialKeyMap with
            | Some keyInput -> Some keyInput
            | None -> 
                if StringUtil.length data = 1 then KeyInputUtil.CharToKeyInput data.[0] |> Some
                else None

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
    let TryCharNotationToKeyInput (data : string) = 
        Contract.Assert (data.[0] = '<')
        Contract.Assert (StringUtil.last data = '>')

        // The actual number string will begin immediately following the - in the 
        // notation and end at the >
        let prefixLength = 6
        let length = data.Length - (prefixLength + 1)
        let numberString = data.Substring(prefixLength, length)

        // TODO: Need to unify the handling of number parsing in this code base.  It's done here,
        // in CommandUtil for the <C-A> command and in the parser 
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
    let TryKeyNotationToKeyInput (data : string) = 
        Contract.Assert (data.[0] = '<')
        Contract.Assert (StringUtil.last data = '>')

        let isCharLiteral = 
            let comparer = CharComparer.IgnoreCase
            StringUtil.isSubstringAt data "char-" 1 CharComparer.IgnoreCase

        if isCharLiteral then
            TryCharNotationToKeyInput data
        else
            let lastDashIndex = data.LastIndexOf('-')
            let rec inner index modifier = 
                if index >= String.length data then 
                    None
                else if lastDashIndex >= 0 && index <= lastDashIndex then
                    let modifier = 
                        match data.[index] |> CharUtil.ToLower with
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
                    let length = ((String.length data) - index) - 1 
                    let rest = data.Substring(index, length)
                    ConvertSpecialKeyName rest modifier
            inner 1 KeyModifiers.None

    /// Try and convert the given string value into a KeyInput.  If the value is wrapped in 
    /// < and > then it will be interpreted as a special key modifier as defined by 
    /// :help key-notation
    let TryStringToKeyInputCore (data : string) = 
        if data.Length = 0 then
            None
        elif data.[0] = '<' then
            let closeIndex = data.IndexOf('>')
            if closeIndex < 0 then
                (KeyInputUtil.CharToKeyInput '<', 1) |> Some
            else 
                let tag = data.Substring(0, closeIndex + 1)
                match TryKeyNotationToKeyInput tag with
                | Some keyInput -> (keyInput, tag.Length) |> Some
                | None -> (KeyInputUtil.CharToKeyInput '<', 1) |> Some
        else
            let keyInput = KeyInputUtil.CharToKeyInput data.[0]
            (keyInput, 1) |> Some

    /// Try and convert the given string value into a single KeyInput value.  If the conversion
    /// fails or is more than 1 KeyInput in length then None will be returned
    let TryStringToKeyInput (data : string) = 
        match TryStringToKeyInputCore data with
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
        let rec inner rest acc =
            match TryStringToKeyInputCore rest with
            | None -> acc [] 
            | Some (keyInput, length) ->
                if length = rest.Length then
                    acc [keyInput]
                else
                    let rest = rest.Substring(length)
                    inner rest (fun tail -> acc (keyInput :: tail))
        match inner data (fun x -> x) with
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
            Some (pair.Key.Value, extra)

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



