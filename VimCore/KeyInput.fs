#light

namespace Vim
open System.Runtime.InteropServices

type VirtualKeyCode = int

[<Sealed>]
type KeyInput
    (
        _virtualKeyCode : VirtualKeyCode,
        _key:VimKey,
        _modKey:KeyModifiers, 
        _literal:char) =

    member x.Char = _literal
    member x.VirtualKeyCode = _virtualKeyCode
    member x.Key = _key
    member x.KeyModifiers = _modKey
    member x.HasShiftModifier = _modKey = KeyModifiers.Shift
    member x.IsDigit = System.Char.IsDigit(x.Char)

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member x.IsNewLine = 
        match _key with
            | VimKey.Enter -> true
            | _ -> false

    /// Is this an arrow key?
    member x.IsArrowKey = 
        match _key with
        | VimKey.Left -> true
        | VimKey.Right -> true
        | VimKey.Up -> true
        | VimKey.Down -> true
        | _ -> false

    member private x.CompareTo (other:KeyInput) =
        let comp = compare x.VirtualKeyCode other.VirtualKeyCode
        if comp <> 0 then comp
        else 
            let comp = compare x.Char other.Char
            if comp <> 0 then  comp
            else
                let comp = compare x.Key other.Key
                if comp <> 0 then  comp
                else
                    compare x.KeyModifiers other.KeyModifiers
                        
    override x.GetHashCode() = int32 _literal
    override x.Equals(obj) =
        match obj with
        | :? KeyInput as other ->
            0 = x.CompareTo other
        | _ -> false

    override x.ToString() = System.String.Format("{0}:{1}:{2}", x.Char, x.Key, x.KeyModifiers);

    static member DefaultValue = KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, System.Char.MinValue)
    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<KeyInput>.Default.Equals(this,other))
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? KeyInput as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  

    interface System.IEquatable<KeyInput> with
        member x.Equals other = 0 = x.CompareTo other

module internal NativeMethods =

    /// Convert the passed in char to the corresponding Virtual Key Code 
    [<DllImport("user32.dll")>]    
    extern System.Int16 VkKeyScan(System.Char ch)

    [<DllImport("user32.dll")>]
    extern uint32 MapVirtualKey(uint32 code, uint32 mapType)

    /// Convert the virtual key + scan code into a unicode character
    [<DllImport("user32.dll")>]
    extern int32 ToUnicode(
        uint32 virtualKeyCode,
        uint32 scanCode,
        [<In>] byte[] keyStateBuffer,
        [<Out>] System.Text.StringBuilder buffer,
        int32 bufferLength,
        int32 flags)

    /// Map a virtual key to a scan code
    let MAPVK_VK_TO_VSC = 0u

    /// Map a virtual key to a char
    let MAPVK_VK_TO_CHAR = 0x02u 

    /// Left shift virtual key
    let VK_LSHIFT = 0xA0

    /// Shift virtual key
    let VK_SHIFT = 0x10

    /// Control virtual key
    let VK_CONTROL = 0x11

    /// Alt virtual key
    let VK_MENU = 0x12  

module KeyInputUtil = 

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
        let res = NativeMethods.VkKeyScan ch
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
                | VimKey.KeypadMultiply -> 0x6A
                | VimKey.KeypadPlus -> 0x6B
                | VimKey.KeypadMinus -> 0x6D
                | VimKey.KeypadDecimal -> 0x6E
                | VimKey.KeypadDivide -> 0x6F
                | VimKey.Keypad0 -> 0x60
                | VimKey.Keypad1 -> 0x61
                | VimKey.Keypad2 -> 0x62
                | VimKey.Keypad3 -> 0x63
                | VimKey.Keypad4 -> 0x64
                | VimKey.Keypad5 -> 0x65
                | VimKey.Keypad6 -> 0x66
                | VimKey.Keypad7 -> 0x67
                | VimKey.Keypad8 -> 0x68
                | VimKey.Keypad9 -> 0x69
                | _ -> failwith "Invalid Enum value"
            Some(code)

    /// Mapping of virtual key code -> VimKey 
    let VirtualKeyCodeToVimKeyMap =
        System.Enum.GetValues(typeof<VimKey>)
        |> Seq.cast<VimKey>
        |> Seq.map (fun vimKey -> ((TryVimKeyToVirtualKeyCode vimKey),vimKey) )
        |> Seq.map OptionUtil.combine2
        |> SeqUtil.filterToSome 
        |> Map.ofSeq

    let TryVirtualKeyCodeToChar virtualKey = 
        if 0 = virtualKey then None
        else   
            // Mode to map a 
            let MAPVK_VK_TO_CHAR = 0x02u 
            let mapped = NativeMethods.MapVirtualKey(uint32 virtualKey, MAPVK_VK_TO_CHAR)
            if 0u = mapped then None
            else 
                let c = char mapped 
                let c = if System.Char.IsLetter(c) then System.Char.ToLower(c) else c
                Some(c)

    let VirtualKeyCodeToChar virtualKey =
        match TryVirtualKeyCodeToChar virtualKey with
        | Some(c) -> c
        | None -> System.Char.MinValue

    let TryCharToKeyInput ch = 
        match TryCharToVirtualKeyAndModifiers ch with
        | None -> None
        | Some(virtualKey,modKeys) -> 
            match Map.tryFind virtualKey VirtualKeyCodeToVimKeyMap with
            | None -> KeyInput(virtualKey, VimKey.NotWellKnown, modKeys, ch) |> Some
            | Some(vimKey) -> KeyInput(virtualKey, vimKey, modKeys, ch) |> Some

    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None -> KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, c)

    /// Map of Virtual Key Code * Key Modifiers -> KeyInput for all of the core
    /// characters
    let CoreVirtualKeyAndModifiersMap = 
        CoreCharacters
        |> Seq.map TryCharToKeyInput
        |> SeqUtil.filterToSome
        |> Seq.map (fun ki -> (ki.VirtualKeyCode,ki.KeyModifiers),ki)
        |> Map.ofSeq

    let VirtualKeyCodeAndModifiersToKeyInput virtualKey keyModifiers =

        // First consult the prebuilt map.  
        let fromMap = 
            match Map.tryFind (virtualKey,keyModifiers) CoreVirtualKeyAndModifiersMap with
            | Some(ki) -> Some ki
            | None -> 
                let checkShift = 
                    if Utils.IsFlagSet keyModifiers KeyModifiers.Shift then 
        
                        // The shift flag is tricky.  There is no good API available to translate a virtualKey 
                        // with an additional modifier.  Instead we define the core set of keys we care about,
                        // map them to a virtualKey + ModifierKeys tuple.  We then consult this map here to see
                        // if we can appropriately "shift" the KeyInput value
                        //
                        // This feels like a very hackish solution and I'm actively seeking a better, more thorough
                        // one.  ToUnicodeEx is likely the best bet but getting it and the dead keys working is 
                        // very involved.  Will take care of this in a future release
                        match Map.tryFind (virtualKey,KeyModifiers.Shift) CoreVirtualKeyAndModifiersMap with 
                        | Some(ki) -> KeyInput(virtualKey, ki.Key, ki.KeyModifiers ||| keyModifiers, ki.Char) |> Some
                        | None -> None
                    else None
                match checkShift with
                | Some(ki) -> checkShift
                | None -> 
                    // Making a bad assumption that the Control and Alt keys don't affect
                    // the char.  Will take care of in a future release
                    match Map.tryFind (virtualKey,KeyModifiers.None) CoreVirtualKeyAndModifiersMap with 
                    | Some(ki) -> KeyInput(virtualKey, ki.Key, keyModifiers, ki.Char) |> Some
                    | None -> None

        match fromMap with
        | Some(ki) -> ki
        | None -> 
            // If we couldn't find a prebuilt one then build manually 
            let c = VirtualKeyCodeToChar virtualKey
            let vimKey = 
                match Map.tryFind virtualKey VirtualKeyCodeToVimKeyMap with
                | Some(k) -> k
                | None -> VimKey.NotWellKnown
            KeyInput(virtualKey, vimKey, keyModifiers, c)

    let VirtualKeyCodeToKeyInput virtualKey = VirtualKeyCodeAndModifiersToKeyInput virtualKey KeyModifiers.None
    let ChangeKeyModifiers (ki:KeyInput) keyModifiers = VirtualKeyCodeAndModifiersToKeyInput ki.VirtualKeyCode keyModifiers

    let VimKeyAndModifiersToKeyInput vimKey modKeys = 
        match TryVimKeyToVirtualKeyCode vimKey with
        | None -> KeyInput(0, VimKey.NotWellKnown, KeyModifiers.None, System.Char.MinValue)
        | Some(virtualKey) -> VirtualKeyCodeAndModifiersToKeyInput virtualKey modKeys

    let VimKeyToKeyInput vimKey = VimKeyAndModifiersToKeyInput vimKey KeyModifiers.None

    let CharWithControlToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        KeyInput(ki.VirtualKeyCode, ki.Key, ki.KeyModifiers ||| KeyModifiers.Control, ki.Char)

    let CharWithAltToKeyInput ch = 
        let ki = ch |> CharToKeyInput 
        KeyInput(ki.VirtualKeyCode, ki.Key, ki.KeyModifiers ||| KeyModifiers.Alt, ki.Char)
