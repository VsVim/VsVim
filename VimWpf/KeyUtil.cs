using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace Vim.UI.Wpf
{
    public static class KeyUtil
    {
        private static ReadOnlyCollection<char> s_coreChars = null;
        private static ReadOnlyCollection<Tuple<char, int, KeyModifiers>> s_mappedCoreChars;

        private static ReadOnlyCollection<char> CoreChars
        {
            get
            {
                if (s_coreChars == null)
                {
                    s_coreChars = CreateCoreChars();
                }
                return s_coreChars;
            }
        }

        private static ReadOnlyCollection<Tuple<char, int, KeyModifiers>> MappedCoreChars
        {
            get
            {
                if (s_mappedCoreChars == null)
                {
                    s_mappedCoreChars = CreateMappedCoreChars();
                }
                return s_mappedCoreChars;
            }
        }

        private static Tuple<KeyInput,int> TryConvertToKeyInput(Key key)
        {
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            var opt = InputUtil.TryVirtualKeyCodeToChar(virtualKey);
            return opt.HasValue()
                ? Tuple.Create(new KeyInput(opt.Value),virtualKey)
                : null;
        }

        public static KeyModifiers ConvertToKeyModifiers(ModifierKeys keys)
        {
            var res = KeyModifiers.None;
            if ( 0 != (keys & ModifierKeys.Shift))
            {
                res = res | KeyModifiers.Shift;
            }
            if (0 != (keys & ModifierKeys.Alt))
            {
                res = res | KeyModifiers.Alt;
            }
            if (0 != (keys & ModifierKeys.Control))
            {
                res = res | KeyModifiers.Control;
            }
            return res;
        }

        public static KeyInput ConvertToKeyInput(Key key)
        {
            var tuple = TryConvertToKeyInput(key);
            return tuple != null
                ? tuple.Item1
                : new KeyInput(Char.MinValue);
        }

        public static KeyInput ConvertToKeyInput(Key key, ModifierKeys modifierKeys)
        {
            var modKeys = ConvertToKeyModifiers(modifierKeys);
            var tuple = TryConvertToKeyInput(key);
            if (tuple == null)
            {
                return new KeyInput(Char.MinValue, modKeys);
            }

            if ((modKeys & KeyModifiers.Shift) == 0)
            {
                var temp = tuple.Item1;
                return new KeyInput(temp.Char, modKeys);
            }
            
            // The shift flag is tricky.  There is no good API available to translate a virtualKey 
            // with an additional modifier.  Instead we define the core set of keys we care about,
            // map them to a virtualKey + ModifierKeys tuple.  We then consult this map here to see
            // if we can appropriately "shift" the KeyInput value
            //
            // This feels like a very hackish solution and I'm actively seeking a better, more thorough
            // one

            var ki = tuple.Item1;
            var virtualKey = tuple.Item2;
            var found = MappedCoreChars.FirstOrDefault(x => x.Item2 == virtualKey && KeyModifiers.Shift == x.Item3);
            if (found == null)
            {
                return new KeyInput(ki.Char, modKeys);
            }
            else
            {
                return new KeyInput(found.Item1, modKeys);
            }
        }

        private static ReadOnlyCollection<char> CreateCoreChars()
        {
            IEnumerable<char> lowerLetters = "abcdefghijklmnopqrstuvwxyz";
            var upperLetters = lowerLetters.Select(Char.ToUpper);
            var other = "!@#$%^&*()[]{}-_=+\\|'\",<>./?\t\b:;";
            var list = lowerLetters.Concat(upperLetters).Concat(other).ToList();
            return new ReadOnlyCollection<char>(list);
        }

        private static ReadOnlyCollection<Tuple<char, int, KeyModifiers>> CreateMappedCoreChars()
        {
            var list = CoreChars
                .Select(x => Tuple.Create(x, InputUtil.TryCharToVirtualKeyAndModifiers(x)))
                .Where(x => x.Item2.HasValue())
                .Select(x => Tuple.Create(x.Item1, x.Item2.Value.Item1, x.Item2.Value.Item2))
                .ToList();
            return new ReadOnlyCollection<Tuple<char, int, KeyModifiers>>(list);
        }
        
    //    CoreChars 
    //    |> Seq.ofList 
    //    |> Seq.map (fun c -> c,TryCharToVirtualKeyAndModifiers c)
    //    |> Seq.choose (fun (c,opt) -> 
    //        match opt with 
    //        | Some(virtualKey,modKeys) -> Some(c,virtualKey,modKeys) 
    //        | None -> None )
    //    |> List.ofSeq

    //let private TryCharToVirtualKeyAndModifiers ch =
    //    let res = VkKeyScan ch
    //    let res = int res

    //    // The virtual key code is the low byte and the shift state is the high byte
    //    let virtualKey = res &&& 0xff 
    //    let state = ((res >>> 8) &&& 0xff) 

    //    if virtualKey = -1 && state = -1 then None
    //    else
    //        let shiftMod = if 0 <> (state &&& 0x1) then ModifierKeys.Shift else ModifierKeys.None
    //        let controlMod = if 0 <> (state &&& 0x2) then ModifierKeys.Control else ModifierKeys.None
    //        let altMod = if 0 <> (state &&& 0x4) then ModifierKeys.Alt else ModifierKeys.None
    //        let modKeys = shiftMod ||| controlMod ||| altMod
    //        Some (virtualKey,modKeys)

    //let TryCharToKeyInput ch =
    //    match TryCharToVirtualKeyAndModifiers ch with
    //    | None -> None
    //    | Some(virtualKey,modKeys) ->
    //        let key = KeyInterop.KeyFromVirtualKey(virtualKey)
    //        KeyInput(ch, key, modKeys) |> Some

    //let private TryVirtualKeyCodeToChar virtualKey = 
    //    if 0 = virtualKey then None
    //    else   
    //        // Mode to map a 
    //        let MAPVK_VK_TO_CHAR = 0x02u 
    //        let mapped = MapVirtualKey(uint32 virtualKey, MAPVK_VK_TO_CHAR)
    //        if 0u = mapped then None
    //        else 
    //            let c = char mapped 
    //            let c = if System.Char.IsLetter(c) then System.Char.ToLower(c) else c
    //            (c,virtualKey) |> Some
    
    //let private TryKeyToKeyInputAndVirtualKey key = 
    //    let virtualKey = KeyInterop.VirtualKeyFromKey(key)
    //    match TryVirtualKeyCodeToChar virtualKey with
    //    | None -> None
    //    | Some(ch,virtualKey) -> (KeyInput(ch,key,ModifierKeys.None),virtualKey) |> Some

    //let TryKeyToKeyInput key = 
    //    match TryKeyToKeyInputAndVirtualKey key with
    //    | None -> None
    //    | Some(ki,_) -> Some(ki)

    //let KeyToKeyInput key =
    //    match TryKeyToKeyInput key with
    //    | Some(ki) -> ki
    //    | None -> KeyInput(System.Char.MinValue, key, ModifierKeys.None)

    //let CharToKeyInput c = 
    //    match TryCharToKeyInput c with
    //    | Some ki -> ki
    //    | None ->
    //        // In some cases we don't have the correct Key enumeration available and 
    //        // have to rely on the char value to be correct
    //        KeyInput(c, Key.None)



    //let VirtualKeyCodeToKeyInput virtualKey = 
    //    let key = KeyInterop.KeyFromVirtualKey(virtualKey)
    //    let ch = 
    //        match TryVirtualKeyCodeToChar virtualKey with
    //        | None -> System.Char.MinValue
    //        | Some(ch,_) -> ch
    //    KeyInput(ch,key)

    /////
    ///// All constant values derived from the list at the following 
    ///// location
    /////   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
    //let VimKeyToKeyInput wellKnownKey = 
    //    match wellKnownKey with 
    //    | BackKey ->  VirtualKeyCodeToKeyInput 0x8
    //    | TabKey ->  VirtualKeyCodeToKeyInput 0x9
    //    | ReturnKey ->  VirtualKeyCodeToKeyInput 0xD
    //    | EscapeKey ->  VirtualKeyCodeToKeyInput 0x1B
    //    | DeleteKey ->  VirtualKeyCodeToKeyInput 0x2E
    //    | LeftKey ->  VirtualKeyCodeToKeyInput 0x25
    //    | UpKey ->  VirtualKeyCodeToKeyInput 0x26
    //    | RightKey ->  VirtualKeyCodeToKeyInput 0x27
    //    | DownKey ->  VirtualKeyCodeToKeyInput 0x28
    //    | LineFeedKey ->  CharToKeyInput '\r'
    //    | HelpKey ->  VirtualKeyCodeToKeyInput 0x2F
    //    | InsertKey ->  VirtualKeyCodeToKeyInput 0x2D
    //    | HomeKey ->  VirtualKeyCodeToKeyInput 0x24
    //    | EndKey ->  VirtualKeyCodeToKeyInput 0x23
    //    | PageUpKey ->  VirtualKeyCodeToKeyInput 0x21
    //    | PageDownKey ->  VirtualKeyCodeToKeyInput 0x22
    //    | NotWellKnownKey -> CharToKeyInput System.Char.MinValue
        
    //let SetModifiers modKeys (ki:KeyInput) = KeyInput(ki.Char,ki.Key, modKeys)
    }
}
