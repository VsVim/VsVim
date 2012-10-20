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

    /// Break up a string into a set of key notation entries
    let SplitIntoKeyNotationEntries (data : string) =

        let rec inner (rest : char list) withData =

            // Check and see if a list starts with a given prefix 
            let rec startsWith c = 
                match rest with
                | [] -> false
                | h :: _ -> c =h 

            // Just finishes the continuation.  Consider throwing here in the future
            let error() = 
                let str = rest |> StringUtil.ofCharList
                withData [str]

            if startsWith '<' then

                // When a '<' char there are a couple of possibilities to consider. 
                //
                //  1. Modifier combination <C-a>, <S-b>, <CS-A>, etc ...
                //  2. Named combination <lt>, <Home>, etc ...
                //  3. Invalid combo <b-a>, <foo
                //
                // The first 2 need to be treated as a single entry while the third need 
                // te be treaed as separate key strokes
                match rest |> List.tryFindIndex (fun c -> c = '>') with
                | None -> 
                    // Easiest case of #3.  Treat them all as separate entries
                    inner rest.Tail (fun next -> withData ("<" :: next))
                | Some closeIndex ->
                    let isModifier = rest.Length >= 3 && List.nth rest 2 = '-'
                    let processGroup() = 
                        let length = closeIndex + 1
                        let str = rest |> Seq.take length |> StringUtil.ofCharSeq
                        let rest = rest |> ListUtil.skip length
                        inner rest (fun next -> withData (str :: next))

                    if isModifier then
                        // Need to determine if it's a valid modfier or not.  
                        let isValid = 
                            let c = List.nth rest 1
                            match CharUtil.ToLower c with 
                            | 'c' -> true
                            | 's' -> true
                            | 'm' -> true
                            | 'a' -> true
                            | 'd' -> true
                            | _ -> false
                        if isValid then 
                            // It's a valid grouping
                            processGroup()
                        else
                            // Invalid modifier is another case of #3.  Treat them separately
                            inner rest.Tail (fun next -> withData ("<" :: next))
                    else 
                        // It's a word in a <> string 
                        processGroup()
            else 
                match ListUtil.tryHead rest with
                | None -> withData []
                | Some (h, t) -> 
                    let str = h |> StringUtil.ofChar
                    inner t (fun next -> withData (str :: next))

        inner (data |> List.ofSeq) (fun all -> all)

    let TryStringToKeyInput (data : string) = 

        // Convert the string into a keyinput 
        let convertToRaw data =
            let comparableData = ComparableString.CreateOrdinalIgnoreCase data
            match Map.tryFind comparableData SpecialKeyMap with
            | Some keyInput -> Some keyInput
            | None -> 
                if StringUtil.length data = 1 then KeyInputUtil.CharToKeyInput data.[0] |> Some
                else None

        // Convert and then apply the modifier
        let convertAndApply data modifier = 
            let keyInput = convertToRaw data 
            match keyInput with 
            | None -> None
            | Some keyInput -> 

                // This is one place where we don't want to always smooth out the modifiers with
                // ApplyModifiers.  Even though it doesn't make any sense to create a mappping
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

        // Inside the <
        let rec insideLessThanGreaterThan (data : string) index modifier = 
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
                    convertAndApply rest modifier
            inner index modifier

        match StringUtil.charAtOption 0 data with
        | None -> None
        | Some '<' -> 
            if String.length data = 1 then '<' |> KeyInputUtil.CharToKeyInput |> Some
            elif StringUtil.last data <> '>' then None
            else insideLessThanGreaterThan data 1 KeyModifiers.None
        | Some c -> 
            if data.Length = 1 then KeyInputUtil.CharToKeyInput data.[0] |> Some
            else None

    let StringToKeyInput data = 
        match TryStringToKeyInput data with 
        | Some ki -> ki
        | None -> invalidArg "data" (Resources.KeyNotationUtil_InvalidNotation data)

    let TryStringToKeyInputSet data = 
        match data |> SplitIntoKeyNotationEntries |> List.map TryStringToKeyInput |> SeqUtil.allOrNone with
        | Some list -> list |> KeyInputSetUtil.OfList |> Some
        | None -> None

    /// Convert tho String to a KeyInputSet 
    let StringToKeyInputSet data = 
        data
        |> SplitIntoKeyNotationEntries
        |> Seq.map StringToKeyInput
        |> KeyInputSetUtil.OfSeq

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



