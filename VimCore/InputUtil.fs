#light

namespace Vim
open System.Windows.Input
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
            let shiftMod = if 0 <> (state &&& 0x1) then ModifierKeys.Shift else ModifierKeys.None
            let controlMod = if 0 <> (state &&& 0x2) then ModifierKeys.Control else ModifierKeys.None
            let altMod = if 0 <> (state &&& 0x4) then ModifierKeys.Alt else ModifierKeys.None
            let modKeys = shiftMod ||| controlMod ||| altMod
            Some (virtualKey,modKeys)

    /// This is the core set of characters used in Vim
    let private CoreChars = 
        ['a' .. 'z' ] @
        ['A' .. 'Z' ] @
        ['0' .. '9' ] @
        ("!@#$%^&*()[]{}-_=+\\|'\",<>./?\t\b:;" |> List.ofSeq)

    /// List of the core Vim Characters mapped into the VirtualKey Code and the corresponding ModifierKeys
    let private MappedCoreChars = 
        CoreChars 
        |> Seq.ofList 
        |> Seq.map (fun c -> c,TryCharToVirtualKeyAndModifiers c)
        |> Seq.choose (fun (c,opt) -> 
            match opt with 
            | Some(virtualKey,modKeys) -> Some(c,virtualKey,modKeys) 
            | None -> None )
        |> List.ofSeq

    let TryCharToKeyInput ch =
        match TryCharToVirtualKeyAndModifiers ch with
        | None -> None
        | Some(virtualKey,modKeys) ->
            let key = KeyInterop.KeyFromVirtualKey(virtualKey)
            KeyInput(ch, key, modKeys) |> Some

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
    
    let private TryKeyToKeyInputAndVirtualKey key = 
        let virtualKey = KeyInterop.VirtualKeyFromKey(key)
        match TryVirtualKeyCodeToChar virtualKey with
        | None -> None
        | Some(ch) -> (KeyInput(ch,key,ModifierKeys.None),virtualKey) |> Some

    let TryKeyToKeyInput key = 
        match TryKeyToKeyInputAndVirtualKey key with
        | None -> None
        | Some(ki,_) -> Some(ki)

    let KeyToKeyInput key =
        match TryKeyToKeyInput key with
        | Some(ki) -> ki
        | None -> KeyInput(System.Char.MinValue, key, ModifierKeys.None)

    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None ->
            // In some cases we don't have the correct Key enumeration available and 
            // have to rely on the char value to be correct
            KeyInput(c, Key.None)

    let KeyAndModifierToKeyInput key modKeys = 
        match TryKeyToKeyInputAndVirtualKey key with
        | None -> KeyInput(System.Char.MinValue, key, modKeys)
        | Some(ki,virtualKey) ->
            
            if Utils.IsFlagSet modKeys ModifierKeys.Shift then 

                // The shift flag is tricky.  There is no good API available to translate a virtualKey 
                // with an additional modifier.  Instead we define the core set of keys we care about,
                // map them to a virtualKey + ModifierKeys tuple.  We then consult this map here to see
                // if we can appropriately "shift" the KeyInput value
                //
                // This feels like a very hackish solution and I'm actively seeking a better, more thorough
                // one

                let opt =  MappedCoreChars  |> Seq.tryFind (fun (_,vk,modKeys) -> virtualKey = vk && modKeys = ModifierKeys.Shift)
                match opt with 
                | None -> ki
                | Some(ch,_,_) -> KeyInput(ch,key,modKeys)
            else KeyInput(ki.Char, ki.Key, modKeys)

    let VirtualKeyCodeToKeyInput virtualKey = 
        let key = KeyInterop.KeyFromVirtualKey(virtualKey)
        let ch = 
            match TryVirtualKeyCodeToChar virtualKey with
            | None -> System.Char.MinValue
            | Some(ch) -> ch
        KeyInput(ch,key)

    ///
    /// All constant values derived from the list at the following 
    /// location
    ///   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
    let WellKnownKeyToKeyInput wellKnownKey = 
        match wellKnownKey with 
        | BackKey ->  VirtualKeyCodeToKeyInput 0x8
        | TabKey ->  VirtualKeyCodeToKeyInput 0x9
        | ReturnKey ->  VirtualKeyCodeToKeyInput 0xD
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
        | NotWellKnownKey -> CharToKeyInput System.Char.MinValue
        
    let SetModifiers modKeys (ki:KeyInput) = KeyInput(ki.Char,ki.Key, modKeys)
        


    
