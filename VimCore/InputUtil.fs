#light

namespace Vim
open System.Runtime.InteropServices


module InputUtil = 

    /// Convert the passed in char to the corresponding Virtual Key Code 
    [<DllImport("user32.dll")>]    
    extern System.Int16 VkKeyScan(System.Char ch)

    [<DllImport("user32.dll")>]
    extern uint32 MapVirtualKey(uint32 code, uint32 mapType)

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

    let TryCharToKeyInput ch = 
        match TryCharToVirtualKeyAndModifiers ch with
        | None -> None
        | Some(_,modKeys) -> KeyInput(ch,modKeys) |> Some

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
    
    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None -> KeyInput(c, KeyModifiers.None)

    let VirtualKeyCodeToKeyInput virtualKey = 
        let ch = 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> System.Char.MinValue
            | Some(ch) -> ch
        KeyInput(ch)

    ///
    /// All constant values derived from the list at the following 
    /// location
    ///   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
    let WellKnownKeyToKeyInput wellKnownKey = 
        match wellKnownKey with 
        | BackKey ->  VirtualKeyCodeToKeyInput 0x8
        | TabKey ->  VirtualKeyCodeToKeyInput 0x9
        | ReturnKey ->  VirtualKeyCodeToKeyInput 0xD
        | EnterKey ->  VirtualKeyCodeToKeyInput 0xD
        | EscapeKey ->  VirtualKeyCodeToKeyInput 0x1B
        | DeleteKey ->  VirtualKeyCodeToKeyInput 0x2E
        | LeftKey ->  VirtualKeyCodeToKeyInput 0x25
        | UpKey ->  VirtualKeyCodeToKeyInput 0x26
        | RightKey ->  VirtualKeyCodeToKeyInput 0x27
        | DownKey ->  VirtualKeyCodeToKeyInput 0x28
        | LineFeedKey ->  CharToKeyInput '\r'
        | HelpKey ->  VirtualKeyCodeToKeyInput 0x2F
        | InsertKey ->  VirtualKeyCodeToKeyInput 0x2D
        | HomeKey ->  VirtualKeyCodeToKeyInput 0x24
        | EndKey ->  VirtualKeyCodeToKeyInput 0x23
        | PageUpKey ->  VirtualKeyCodeToKeyInput 0x21
        | PageDownKey ->  VirtualKeyCodeToKeyInput 0x22
        | BreakKey -> VirtualKeyCodeToKeyInput 0x03
        | NotWellKnownKey -> CharToKeyInput System.Char.MinValue
        | F1Key -> VirtualKeyCodeToKeyInput 0x70
        | F2Key -> VirtualKeyCodeToKeyInput 0x71
        | F3Key -> VirtualKeyCodeToKeyInput 0x72
        | F4Key -> VirtualKeyCodeToKeyInput 0x73
        | F5Key -> VirtualKeyCodeToKeyInput 0x74
        | F6Key -> VirtualKeyCodeToKeyInput 0x75
        | F7Key -> VirtualKeyCodeToKeyInput 0x76
        | F8Key -> VirtualKeyCodeToKeyInput 0x77
        | F9Key -> VirtualKeyCodeToKeyInput 0x78
        | F10Key -> VirtualKeyCodeToKeyInput 0x79
        | F11Key -> VirtualKeyCodeToKeyInput 0x7a
        | F12Key -> VirtualKeyCodeToKeyInput 0x7b
        
    let SetModifiers modKeys (ki:KeyInput) = KeyInput(ki.Char,ki.Key, modKeys)
        


    
