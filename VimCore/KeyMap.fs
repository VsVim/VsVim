#light

namespace Vim
open System.Windows.Input

// :help key-notation for all of the key codes

module internal KeyMapUtil =

    let private ManualKeyList = 
        [
            ("<Nul>",InputUtil.CharToKeyInput '@' |> InputUtil.SetModifiers ModifierKeys.Control);
            ("<Bs>",InputUtil.KeyToKeyInput Key.Back);
            ("<Tab>",InputUtil.KeyToKeyInput Key.Tab);
            ("<NL>", InputUtil.KeyToKeyInput Key.LineFeed);
            ("<FF>", InputUtil.CharToKeyInput 'l' |> InputUtil.SetModifiers ModifierKeys.Control);
            ("<CR>", InputUtil.KeyToKeyInput Key.Return);
            ("<Return>", InputUtil.KeyToKeyInput Key.Return);
            ("<Enter>", InputUtil.KeyToKeyInput Key.Return);
            ("<Esc>", InputUtil.KeyToKeyInput Key.Escape);
            ("<Space>", InputUtil.KeyToKeyInput Key.Space);
            ("<lt>", InputUtil.CharToKeyInput '<');
            ("<Bslash>", InputUtil.CharToKeyInput '\\' );
            ("<Bar>", InputUtil.CharToKeyInput '|');
            ("<Del>", InputUtil.KeyToKeyInput Key.Delete );
            ("<Up>", InputUtil.KeyToKeyInput Key.Up);
            ("<Down>", InputUtil.KeyToKeyInput Key.Down);
            ("<Left>", InputUtil.KeyToKeyInput Key.Left);
            ("<Right>", InputUtil.KeyToKeyInput Key.Right);
            ("<Help>", InputUtil.KeyToKeyInput Key.Help);
            ("<Insert>", InputUtil.KeyToKeyInput Key.Insert);
            ("<Home>", InputUtil.KeyToKeyInput Key.Home);
            ("<End>", InputUtil.KeyToKeyInput Key.End);
            ("<PageUp>", InputUtil.KeyToKeyInput Key.PageUp);
            ("<PageDown>", InputUtil.KeyToKeyInput Key.PageDown);
            ("<kHome>", InputUtil.KeyToKeyInput Key.Home);
            ("<kEnd>", InputUtil.KeyToKeyInput Key.End);
            ("<kPageUp>", InputUtil.KeyToKeyInput Key.PageUp);
            ("<kPageDown>", InputUtil.KeyToKeyInput Key.PageDown);
        ]

    let private FunctionKeys = 
        let keys =  
            [1..12] 
            |> Seq.map (fun number -> sprintf "F%d" number) 
            |> Seq.map (fun value -> System.Enum.Parse(typeof<Key>,value) :?> Key)
        let standard = keys |> Seq.map (fun key -> ((sprintf "<%s>" (key.ToString())),(key |> InputUtil.KeyToKeyInput)))
        standard |> List.ofSeq

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
        let toMod = allManual |> Seq.append FunctionKeys |> Seq.filter (fun (_,ki) -> ki.ModifierKeys = ModifierKeys.None)
        let doMod prefix modKeys = 
            let changePrefix (name:string) = sprintf "<%s-%s" prefix (name.Substring(1))
            toMod |> Seq.map (fun (name,ki) -> (changePrefix name),(ki |> InputUtil.SetModifiers modKeys))
        let withShift = doMod "S" ModifierKeys.Shift
        let withControl = doMod "C" ModifierKeys.Control
        let withMeta = doMod "M" ModifierKeys.Alt
        let withAlt = doMod "A" ModifierKeys.Alt
        let lettersWithShift = ['A'..'Z'] |> Seq.map (fun c -> ((sprintf "<S-%c>" c),InputUtil.CharToKeyInput c |> InputUtil.SetModifiers ModifierKeys.Shift))
        allManual
        |> Seq.append withShift
        |> Seq.append withControl
        |> Seq.append withMeta
        |> Seq.append withAlt
        |> Seq.append lettersWithShift
        |> List.ofSeq

    /// Try to convert the passed in string into a single KeyInput value according to the
    /// guidelines specified in :help key-notation.  
    let TryStringToKeyInput data = 
        
        match StringUtil.CharAtOption 0 data with
        | None -> None
        | Some('<') -> 
            match KeyNotationList |> Seq.tryFind (fun (name,_) -> StringUtil.IsEqualIgnoreCase name data) with
            | None -> None
            | Some(_,ki) -> Some(ki)
        | Some(c) -> 
            // If it doesn't start with a < then it must be a single character value
            if StringUtil.Length data = 1 then c |> InputUtil.CharToKeyInput |> Some
            else None

    /// Try to convert the passed in string to multiple KeyInput values.  Returns true only
    /// if the entire list succesfully parses
    let TryStringToKeyInputList (data:string) =
        let rec inner data withData =
            match ListUtil.tryHead data with
            | None -> withData []  |> Some
            | Some('<',_) ->
                
                match data |> List.tryFindIndex (fun c -> c = '>') with
                | None -> None
                | Some(index) ->
                    let str = data |> Seq.ofList |> Seq.take (index+1) |> StringUtil.ofCharSeq
                    match TryStringToKeyInput str with
                    | None -> None
                    | Some(ki) -> inner (data |> Seq.skip (index+1) |> List.ofSeq ) (fun rest -> withData (ki :: rest))
            | Some(head,tail) -> 
                let ki = InputUtil.CharToKeyInput head
                inner tail (fun rest -> withData (ki :: rest))
        inner (data |> List.ofSeq) (fun all -> all)
            

type internal RemapModeMap = Map<string, (KeyInput list * bool)>

type internal KeyMap() =
    
    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    /// Convert the KeyInput list back to an actual string key.  Can't use the input string because
    /// several input values map back to the same KeyInput sequence
    static member private KeyInputSequenceToKey (l:KeyInput seq) = l |> Seq.map (fun ki -> ki.Char) |> StringUtil.ofCharSeq

    static member private UserInputToKey input =
        match KeyMapUtil.TryStringToKeyInputList input with
        | None -> None
        | Some(list) -> KeyMap.KeyInputSequenceToKey list |> Some

    member x.GetKeyMapping (ki:KeyInput) mode = 
        let kiSeq,_,_ = x.GetKeyMappingCore ki mode Set.empty
        kiSeq

    member x.GetKeyMappingResult ki mode = 
        let kiSeq,isRecursive,_ = x.GetKeyMappingCore ki mode Set.empty
        if isRecursive then RecursiveMapping
        elif kiSeq |> Seq.isEmpty then NoMapping
        elif kiSeq |> Seq.length = 1 then SingleKey (kiSeq |> Seq.head)
        else KeySequence kiSeq

    member x.MapWithNoRemap lhs rhs mode = x.MapCore lhs rhs mode false
    member x.MapWithRemap lhs rhs mode = x.MapCore lhs rhs mode true

    member x.Clear mode = _map <- _map |> Map.remove mode
    member x.ClearAll () = _map <- Map.empty

    /// Main API for adding a key mapping into our storage
    member private x.MapCore (lhs:string) (rhs:string) (mode:KeyRemapMode) allowRemap = 
        if StringUtil.isNullOrEmpty lhs || StringUtil.isNullOrEmpty rhs then
            false
        else
            let key = KeyMap.UserInputToKey lhs
            let rhs = KeyMapUtil.TryStringToKeyInputList rhs
            match key,rhs with
            | Some(key),Some(rightList) ->
                let value = (rightList,allowRemap)
                let modeMap = 
                    match _map |> Map.tryFind mode with
                    | None -> Map.empty
                    | Some(modeMap) -> modeMap
                let modeMap = Map.add key value modeMap
                _map <- Map.add mode modeMap _map
                true
            | _ -> false

    member x.Unmap lhs mode = 
        if StringUtil.isNullOrEmpty lhs then false
        else
            match KeyMap.UserInputToKey lhs with
            | None -> false
            | Some(key) ->
                match _map |> Map.tryFind mode with
                | None -> false
                | Some(modeMap) -> 
                    if Map.containsKey key modeMap then
                        let modeMap = Map.remove key modeMap
                        _map <- Map.add mode modeMap _map
                        true
                    else
                        false

    /// Get the key mapping for the passed in data.  Returns a tuple of (KeyInput list,bool,Set<KeyInput>)
    /// where the bool value is true if there is a recursive mapping.  The Set parameter
    /// tracks the KeyInput values we've already seen in order to detect recursion 
    member private x.GetKeyMappingCore (ki:KeyInput) mode set = 
        if Set.contains ki set then (Seq.empty, true, set)
        else
            match _map |> Map.tryFind mode with
            | None -> (Seq.empty, false, set)
            | Some(modeMap) ->  
                let key = ki |> Seq.singleton |> KeyMap.KeyInputSequenceToKey
                match modeMap |> Map.tryFind key with
                | None -> (Seq.empty, false, set)
                | Some(kiSeq,allowRemap) -> 
                    let set = set |> Set.add ki
                    if allowRemap then
                        let mutable anyRecursive = false
                        let mutable set = set
                        let list = new System.Collections.Generic.List<KeyInput>()
                        for mappedKi in kiSeq do
                            let mappedSeq, isRecursive,newSet = x.GetKeyMappingCore mappedKi mode set
                            set <- newSet
                            if isRecursive then
                                anyRecursive <- true
                                list.Add(mappedKi)
                            elif mappedSeq |> Seq.isEmpty then
                                list.Add(mappedKi)
                            else
                                list.AddRange(mappedSeq)

                        (list :> KeyInput seq, anyRecursive, set)

                    else
                        (kiSeq |> Seq.ofList, false, set)

            

    interface IKeyMap with
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.GetKeyMappingResult ki mode = x.GetKeyMappingResult ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

