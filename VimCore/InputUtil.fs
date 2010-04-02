#light

namespace Vim
open System.Runtime.InteropServices


module InputUtil = 

    /// Convert the passed in char to the corresponding Virtual Key Code 
    [<DllImport("user32.dll")>]    
    extern System.Int16 VkKeyScan(System.Char ch)

    [<DllImport("user32.dll")>]
    extern uint32 MapVirtualKey(uint32 code, uint32 mapType)

    let CoreCharacters =
        let lowerLetters = "abcdefghijklmnopqrstuvwxyz" :> char seq
        let upperLetters = lowerLetters |> Seq.map (fun x -> System.Char.ToUpper(x))
        let other = "!@#$%^&*()[]{}-_=+\\|'\",<>./?\t\b:;\n\r`" :> char seq
        let digits = "1234567890"
        lowerLetters
        |> Seq.append upperLetters
        |> Seq.append other
        |> Seq.append digits

    let CoreCharactersSet = CoreCharacters |> Set.ofSeq

    let TryCharToVirtualKeyAndModifiers ch =
        let res = VkKeyScan ch
        let res = int res

        // The virtual key code is the low byte and the shift state is the high byte
        let virtualKey = res &&& 0xff 
        let state = ((res >>> 8) &&& 0xff) 

        if virtualKey = -1 && state = -1 then None
        else
            let shiftMod = if 0 <> (state &&& 0x1) then KeyModifiers.Shift else KeyModifiers.None
            let controlMod = if 0 <> (state &&& 0x2) then KeyModifiers.Control else KeyModifiers.None
            let altMod = if 0 <> (state &&& 0x4) then KeyModifiers.Alt else KeyModifiers.None
            let modKeys = shiftMod ||| controlMod ||| altMod
            Some (virtualKey,modKeys)

    let TryVirtualKeyCodeToChar virtualKey = 
        if 0 = virtualKey then None
        else   
            // Mode to map a 
            let MAPVK_VK_TO_CHAR = 0x02u 
            let mapped = MapVirtualKey(uint32 virtualKey, MAPVK_VK_TO_CHAR)
            if 0u = mapped then None
            else 
                let c = char mapped 
                let c = if System.Char.IsLetter(c) then System.Char.ToLower(c) else c
                Some(c)
    ///
    /// All constant values derived from the list at the following 
    /// location
    ///   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
    let TryVimKeyToVirtualKeyCode vimKey = 
        if vimKey = VimKey.NotWellKnownKey then None
        else
            let code = 
                match vimKey with 
                | VimKey.BackKey ->  0x8
                | VimKey.TabKey ->  0x9
                | VimKey.EnterKey ->  0xD
                | VimKey.EscapeKey ->  0x1B
                | VimKey.DeleteKey ->  0x2E
                | VimKey.LeftKey ->  0x25
                | VimKey.UpKey ->  0x26
                | VimKey.RightKey ->  0x27
                | VimKey.DownKey ->  0x28
                | VimKey.HelpKey ->  0x2F
                | VimKey.InsertKey ->  0x2D
                | VimKey.HomeKey ->  0x24
                | VimKey.EndKey ->  0x23
                | VimKey.PageUpKey ->  0x21
                | VimKey.PageDownKey ->  0x22
                | VimKey.BreakKey -> 0x03
                | VimKey.F1Key -> 0x70
                | VimKey.F2Key -> 0x71
                | VimKey.F3Key -> 0x72
                | VimKey.F4Key -> 0x73
                | VimKey.F5Key -> 0x74
                | VimKey.F6Key -> 0x75
                | VimKey.F7Key -> 0x76
                | VimKey.F8Key -> 0x77
                | VimKey.F9Key -> 0x78
                | VimKey.F10Key -> 0x79
                | VimKey.F11Key -> 0x7a
                | VimKey.F12Key -> 0x7b
                | VimKey.MultiplyKey -> 0x6A
                | VimKey.AddKey -> 0x6B
                | VimKey.SeparatorKey -> 0x6C
                | VimKey.SubtractKey -> 0x6D
                | VimKey.DecimalKey -> 0x6E
                | VimKey.DivideKey -> 0x6F
                | _ -> failwith "Invalid Enum value"
            Some(code)
        
    /// This is a tuple of the VimKey values listing their char,virtualKey,VimKey values
    let VimKeyMap = 
        let vimKeyToTuple vimKey = 
            match TryVimKeyToVirtualKeyCode vimKey with
            | None -> None
            | Some(virtualKey) ->
                match TryVirtualKeyCodeToChar virtualKey with
                | None -> Some(virtualKey,System.Char.MinValue,vimKey)
                | Some(ch) -> Some(virtualKey,ch,vimKey)

        System.Enum.GetValues(typeof<VimKey>) 
        |> Seq.cast<VimKey>
        |> Seq.map vimKeyToTuple
        |> Seq.choose (fun x -> x)
        |> Seq.map (fun (virtualKey,ch,vimKey) -> virtualKey,(ch,vimKey))
        |> Map.ofSeq

    let TryCharToKeyInput ch = 
        match TryCharToVirtualKeyAndModifiers ch with
        | None -> None
        | Some(virtualKey,modKeys) -> 
            match Map.tryFind virtualKey VimKeyMap with
            | None -> KeyInput(ch,modKeys) |> Some
            | Some(_,vimKey) -> KeyInput(ch, vimKey, modKeys) |> Some

    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None -> KeyInput(c, KeyModifiers.None)

    let TryVirtualKeyCodeToKeyInput virtualKey = 
        match Map.tryFind virtualKey VimKeyMap with
        | Some(ch,vimKey) -> KeyInput(ch,vimKey,KeyModifiers.None) |> Some
        | None -> 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> None
            | Some(ch) -> KeyInput(ch) |> Some

    let VirtualKeyCodeToKeyInput virtualKey = 
        let ch = 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> System.Char.MinValue
            | Some(ch) -> ch
        KeyInput(ch)

    let VimKeyToKeyInput vimKey = 
        let bad = KeyInput(System.Char.MinValue)
        match TryVimKeyToVirtualKeyCode vimKey with
        | None -> bad
        | Some(virtualKey) -> 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> KeyInput(System.Char.MinValue, vimKey, KeyModifiers.None)
            | Some(ch) -> KeyInput(ch, vimKey, KeyModifiers.None)
        
    let SetModifiers modKeys (ki:KeyInput) = KeyInput(ki.Char,ki.Key, modKeys)
        
    let VimKeyAndModifiersToKeyInput vimKey modKeys = vimKey |> VimKeyToKeyInput |> SetModifiers modKeys

    let CharAndModifiersToKeyInput ch modKeys = ch |> CharToKeyInput |> SetModifiers modKeys

    
