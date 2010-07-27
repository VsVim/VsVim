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
        let other = "!@#$%^&*()[]{}-_=+\\|'\",<>./?\t\b:;\n\r`~" :> char seq
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
        if vimKey = VimKey.NotWellKnown then None
        else
            let code = 
                match vimKey with 
                | VimKey.Back ->  0x8
                | VimKey.Tab ->  0x9
                | VimKey.Enter ->  0xD
                | VimKey.Escape ->  0x1B
                | VimKey.Delete ->  0x2E
                | VimKey.Left ->  0x25
                | VimKey.Up ->  0x26
                | VimKey.Right ->  0x27
                | VimKey.Down ->  0x28
                | VimKey.Help ->  0x2F
                | VimKey.Insert ->  0x2D
                | VimKey.Home ->  0x24
                | VimKey.End ->  0x23
                | VimKey.PageUp ->  0x21
                | VimKey.PageDown ->  0x22
                | VimKey.Break -> 0x03
                | VimKey.F1 -> 0x70
                | VimKey.F2 -> 0x71
                | VimKey.F3 -> 0x72
                | VimKey.F4 -> 0x73
                | VimKey.F5 -> 0x74
                | VimKey.F6 -> 0x75
                | VimKey.F7 -> 0x76
                | VimKey.F8 -> 0x77
                | VimKey.F9 -> 0x78
                | VimKey.F10 -> 0x79
                | VimKey.F11 -> 0x7a
                | VimKey.F12 -> 0x7b
                | VimKey.Multiply -> 0x6A
                | VimKey.Add -> 0x6B
                | VimKey.Separator -> 0x6C
                | VimKey.Subtract -> 0x6D
                | VimKey.Decimal -> 0x6E
                | VimKey.Divide -> 0x6F
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
            | None -> KeyInput(ch, VimKey.NotWellKnown, modKeys) |> Some
            | Some(_,vimKey) -> KeyInput(ch, vimKey, modKeys) |> Some

    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None -> KeyInput(c, VimKey.NotWellKnown, KeyModifiers.None)

    let TryVirtualKeyCodeToKeyInput virtualKey = 
        match Map.tryFind virtualKey VimKeyMap with
        | Some(ch,vimKey) -> KeyInput(ch,vimKey,KeyModifiers.None) |> Some
        | None -> 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> None
            | Some(ch) -> KeyInput(ch, VimKey.NotWellKnown, KeyModifiers.None) |> Some

    let VimKeyToKeyInput vimKey = 
        match TryVimKeyToVirtualKeyCode vimKey with
        | None -> KeyInput(System.Char.MinValue, VimKey.NotWellKnown, KeyModifiers.None)
        | Some(virtualKey) -> 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> KeyInput(System.Char.MinValue, vimKey, KeyModifiers.None)
            | Some(ch) -> KeyInput(ch, vimKey, KeyModifiers.None)
        
    let SetModifiers modKeys (ki:KeyInput) = KeyInput(ki.Char,ki.Key, modKeys)
        
    let VimKeyAndModifiersToKeyInput vimKey modKeys = vimKey |> VimKeyToKeyInput |> SetModifiers modKeys

    let CharWithControlToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        KeyInput(ki.Char, ki.Key, ki.KeyModifiers ||| KeyModifiers.Control)

    let CharWithAltToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        KeyInput(ki.Char, ki.Key, ki.KeyModifiers ||| KeyModifiers.Alt)
