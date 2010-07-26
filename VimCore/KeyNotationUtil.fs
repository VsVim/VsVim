#light

namespace Vim

/// Utility class for converting Key notations into KeyInput and vice versa
/// :help key-notation for all of the key codes
module internal KeyNotationUtil =

    let private ManualKeyList = 
        [
            ("<Nul>",InputUtil.CharWithControlToKeyInput '@');
            ("<Bs>", InputUtil.VimKeyToKeyInput VimKey.BackKey);
            ("<Tab>",InputUtil.VimKeyToKeyInput VimKey.TabKey);
            ("<NL>", InputUtil.VimKeyToKeyInput VimKey.EnterKey);
            ("<FF>", InputUtil.CharWithControlToKeyInput 'l');
            ("<CR>", InputUtil.VimKeyToKeyInput VimKey.EnterKey);
            ("<Return>", InputUtil.VimKeyToKeyInput VimKey.EnterKey);
            ("<Enter>", InputUtil.VimKeyToKeyInput VimKey.EnterKey);
            ("<Esc>", InputUtil.VimKeyToKeyInput VimKey.EscapeKey);
            ("<Space>", InputUtil.CharToKeyInput ' ')
            ("<lt>", InputUtil.CharToKeyInput '<');
            ("<Bslash>", InputUtil.CharToKeyInput '\\' );
            ("<Bar>", InputUtil.CharToKeyInput '|');
            ("<Del>", InputUtil.VimKeyToKeyInput VimKey.DeleteKey);
            ("<Up>", InputUtil.VimKeyToKeyInput VimKey.UpKey);
            ("<Down>", InputUtil.VimKeyToKeyInput VimKey.DownKey);
            ("<Left>", InputUtil.VimKeyToKeyInput VimKey.LeftKey);
            ("<Right>", InputUtil.VimKeyToKeyInput VimKey.RightKey);
            ("<Help>", InputUtil.VimKeyToKeyInput VimKey.HelpKey);
            ("<Insert>", InputUtil.VimKeyToKeyInput VimKey.InsertKey);
            ("<Home>", InputUtil.VimKeyToKeyInput VimKey.HomeKey);
            ("<End>", InputUtil.VimKeyToKeyInput VimKey.EndKey);
            ("<PageUp>", InputUtil.VimKeyToKeyInput VimKey.PageUpKey);
            ("<PageDown>", InputUtil.VimKeyToKeyInput VimKey.PageDownKey);
            ("<kHome>", InputUtil.VimKeyToKeyInput VimKey.HomeKey);
            ("<kEnd>", InputUtil.VimKeyToKeyInput VimKey.EndKey);
            ("<kPageUp>", InputUtil.VimKeyToKeyInput VimKey.PageUpKey);
            ("<kPageDown>", InputUtil.VimKeyToKeyInput VimKey.PageDownKey);
        ]

    let private FunctionKeys = 
        [VimKey.F1Key;VimKey.F2Key;VimKey.F3Key;VimKey.F4Key;VimKey.F5Key;VimKey.F6Key;VimKey.F7Key;VimKey.F8Key;VimKey.F9Key;VimKey.F10Key;VimKey.F11Key;VimKey.F12Key]
            |> Seq.mapi (fun i k -> (i+1),k)
            |> Seq.map (fun (number,key) -> (sprintf "<F%d>" number),InputUtil.VimKeyToKeyInput key)
            |> List.ofSeq

    /// Map of the special key names to their KeyInput representation.  Does not include the <> bookends
    /// Not supported
    /// <CSI>		command sequence intro  ALT-Esc 155	*<CSI>*
    /// <xCSI>		CSI when typed in the GUI		*<xCSI>*
    /// <Undo>		undo key
    /// <EOL>
    /// <kPlus>		keypad +			*keypad-plus*
    /// <kMinus>	keypad -			*keypad-minus*
    /// <kMultiply>	keypad *			*keypad-multiply*
    /// <kDivide>	keypad /			*keypad-divide*
    /// <kEnter>	keypad Enter			*keypad-enter*
    /// <kPoint>	keypad Decimal point		*keypad-point*
    /// <k0> - <k9>	keypad 0 to 9			*keypad-0* *keypad-9*
    let private SpecialKeyMap = 
        ManualKeyList
        |> Seq.append FunctionKeys
        |> Seq.map (fun (k,v) -> k.Substring(1,k.Length-2),v)
        |> Map.ofSeq

    /// Break up a string into a set of key notation entries
    let SplitIntoKeyNotationEntries (data:string) =
        let ctrlPrefix = "ctrl-" |> List.ofSeq

        let rec inner (rest:char list) withData =

            // Check and see if a list starts with a given prefix 
            let rec startsWith prefix target =
                match ListUtil.tryHead prefix, ListUtil.tryHead target with
                | Some(prefixHead,prefixTail),Some(targetHead,targetTail) ->
                    if CharUtil.IsEqualIgnoreCase prefixHead targetHead then startsWith prefixTail targetTail
                    else false
                | None,_ -> true
                | Some(_),None -> false

            // Just finishes the continuation.  Consider throwing here in the future
            let error() = 
                let str = rest |> StringUtil.ofCharList
                withData [str]

            // Called when a key is processed but it needs to be followed by a _
            // or be the completion of the data
            let needConnectorOrEmpty rest withData = 
                match ListUtil.tryHead rest with
                | None -> withData [] 
                | Some(h,t) ->
                    if h <> '_' then error()
                    else inner t withData 

            if startsWith ctrlPrefix rest then 
                // Handle the CTRL- prefix 
                if rest.Length = ctrlPrefix.Length then error()
                else
                    let toSkip = ctrlPrefix.Length+1
                    let str = rest |> Seq.take toSkip |> StringUtil.ofCharSeq
                    let rest = ListUtil.skip toSkip rest
                    needConnectorOrEmpty rest (fun next -> withData (str::next))
            elif startsWith ['<'] rest then 
                match rest |> List.tryFindIndex (fun c -> c = '>') with
                | None -> error()
                | Some(index) ->
                    let length = index+1
                    let str = rest |> Seq.take length |> StringUtil.ofCharSeq
                    let rest = rest |> ListUtil.skip length
                    inner rest (fun next -> withData (str :: next))
            else 
                match ListUtil.tryHead rest with
                | None -> withData []
                | Some(h,t) -> 
                    let str = h |> StringUtil.ofChar
                    inner t (fun next -> withData (str :: next))

        inner (data |> List.ofSeq) (fun all -> all)

    /// Try to convert the passed in string into a single KeyInput value according to the
    /// guidelines specified in :help key-notation.  
    let TryStringToKeyInput (data:string) = 

        let tryCharToKeyInput c = 
            if Set.contains c InputUtil.CoreCharactersSet then InputUtil.CharToKeyInput c |> Some
            else None

        // Convert the string into a keyinput 
        let convertToRaw data =
            match Map.tryFind data SpecialKeyMap with
            | Some(ki) -> Some ki
            | None -> 
                if StringUtil.length data = 1 then tryCharToKeyInput data.[0]
                else None

        // Convert and then apply the modifier
        let convertAndApply data modifier = 
            let ki = convertToRaw data 
            match modifier,ki with 
            | Some(modifier),Some(ki) -> 
                let c = 
                    if modifier = KeyModifiers.Shift && CharUtil.IsLetter ki.Char then CharUtil.ToUpper ki.Char
                    else ki.Char
                KeyInput(c, ki.Key,modifier ||| ki.KeyModifiers) |> Some
            | _ -> None

        // Inside the <
        let insideLessThanGreaterThan() = 
            if data.Length >= 3 && data.[2] = '-' then
                let modifier = 
                    match data.[1] |> CharUtil.ToLower with
                    | 'c' -> KeyModifiers.Control |> Some
                    | 's' -> KeyModifiers.Shift |> Some
                    | 'a' -> KeyModifiers.Alt |> Some
                    | _ -> None
                convertAndApply (data.Substring(3, data.Length-4)) modifier
            else 
                convertToRaw (data.Substring(1,data.Length - 2))

        match StringUtil.charAtOption 0 data with
        | None -> None
        | Some('<') -> 
            if StringUtil.last data <> '>' then None
            else insideLessThanGreaterThan()
        | Some(c) -> 
            let prefix = "CTRL-"
            if StringUtil.startsWithIgnoreCase prefix data then
                if data.Length < prefix.Length + 1 then None
                else convertAndApply (data.Substring(prefix.Length)) (Some KeyModifiers.Control)
            elif data.Length = 1 then tryCharToKeyInput data.[0]
            else None

    let StringToKeyInput data = 
        match TryStringToKeyInput data with 
        | Some(ki) -> ki
        | None -> invalidOp (Resources.KeyNotationUtil_InvalidNotation data)

    /// Try to convert the passed in string to multiple KeyInput values.  Returns true only
    /// if the entire list succesfully parses
    let TryStringToKeyInputSet data = 
        match data |> SplitIntoKeyNotationEntries |> List.map TryStringToKeyInput |> SeqUtil.allOrNone with
        | Some(list) -> list |> KeyInputSetUtil.ofList |> Some
        | None -> None

    /// Convert tho String to a KeyInputSet 
    let StringToKeyInputSet data = 
        data
        |> SplitIntoKeyNotationEntries
        |> Seq.map StringToKeyInput
        |> KeyInputSetUtil.ofSeq

