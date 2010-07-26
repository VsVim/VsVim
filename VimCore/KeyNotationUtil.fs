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

    /// Contains the tuple of (name,KeyInput) for all of the supported key notations
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
    let KeyNotationList = 
        let allManual = ManualKeyList |> Seq.append FunctionKeys
        let lowerCaseLetters = ['a'..'z'] |> Seq.map (fun ch -> (sprintf "<%c>" ch),(InputUtil.CharToKeyInput ch))
        let toModShort = 
            allManual 
            |> Seq.append FunctionKeys 
            |> Seq.filter (fun (_,ki) -> ki.KeyModifiers = KeyModifiers.None)
        let toMod = toModShort |> Seq.append lowerCaseLetters
        let doMod toMod prefix modKeys = 
            let changePrefix (name:string) = sprintf "<%s-%s" prefix (name.Substring(1))
            toMod |> Seq.map (fun (name,(ki:KeyInput)) -> (changePrefix name),(KeyInput(ki.Char, ki.Key, modKeys)))

        // Don' run the modifier on the lower case letters for Shift.  They have to be recreated with different
        // modifiers
        let withShift = doMod toModShort "S" KeyModifiers.Shift
        let withControl = doMod toMod "C" KeyModifiers.Control
        let withMeta = doMod toMod "M" KeyModifiers.Alt
        let withAlt = doMod toMod "A" KeyModifiers.Alt
        let upperCaseLetters = ['A' .. 'Z'] |> Seq.map (fun ch -> (sprintf "<S-%c>" ch),InputUtil.CharToKeyInput ch)
            
        allManual
        |> Seq.append withShift
        |> Seq.append withControl
        |> Seq.append withMeta
        |> Seq.append withAlt
        |> Seq.append upperCaseLetters
        |> List.ofSeq

    /// Break up a string into a set of key notation entries
    let SplitIntoKeyNotationEntries (data:string) =
        let rec inner (rest:char list) withData =
            match rest |> SeqUtil.tryHeadOnly with
            | None -> withData []
            | Some('<') ->
                match rest |> List.tryFindIndex (fun c -> c = '>') with
                | None -> 
                    let str = rest |> StringUtil.ofCharList
                    withData [str]
                | Some(index) ->
                    let length = index+1
                    let str = rest |> Seq.take length |> StringUtil.ofCharSeq
                    let rest = rest |> ListUtil.skip length
                    inner rest (fun next -> withData (str :: next))
            | Some(c) -> 
                let str = c |> StringUtil.ofChar
                inner (rest |> ListUtil.skip 1) (fun next -> withData (str :: next))
        inner (data |> List.ofSeq) (fun all -> all)

    /// Try to convert the passed in string into a single KeyInput value according to the
    /// guidelines specified in :help key-notation.  
    let TryStringToKeyInput data = 
        
        match StringUtil.charAtOption 0 data with
        | None -> None
        | Some('<') -> 
            match KeyNotationList |> Seq.tryFind (fun (name,_) -> StringUtil.isEqualIgnoreCase name data) with
            | None -> None
            | Some(_,ki) -> Some(ki)
        | Some(c) -> 
            // If it doesn't start with a < then it must be a single character value
            if StringUtil.length data = 1 then c |> InputUtil.CharToKeyInput |> Some
            else None

    let StringToKeyInput data = 
        match TryStringToKeyInput data with 
        | Some(ki) -> ki
        | None -> invalidOp Resources.KeyNotationUtil_InvalidNotation

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

