using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Class responsible for handling the low level details for mapping WPF's 
    /// view of the Keyboard to Vim's understanding
    /// </summary>
    internal sealed class KeyboardMap
    {
        private struct KeyType
        {
            internal readonly Key Key;
            internal readonly ModifierKeys ModifierKeys;
            internal KeyType(Key key, ModifierKeys modKeys)
            {
                Key = key;
                ModifierKeys = modKeys;
            }
        }

        /// <summary>
        /// The Id of the Keyboard 
        /// </summary>
        private readonly IntPtr _keyboardId;

        /// <summary>
        /// Cache of Key + Modifiers to a KeyInput for the current keyboard layout
        /// </summary>
        private readonly Dictionary<KeyType, KeyInput> _keyTypeToKeyInputMap;

        /// <summary>
        /// Cache of the char and the modifiers needed to build the char 
        /// </summary>
        private readonly Dictionary<char, ModifierKeys> _charToModifierMap;

        internal IntPtr KeyboardId
        {
            get { return _keyboardId; }
        }

        /// <summary>
        /// Language Identifier of the keyboard
        /// </summary>
        internal int LanguageIdentifier
        {
            get { return NativeMethods.LoWord(_keyboardId.ToInt32()); }
        }

        internal KeyboardMap(IntPtr keyboardId)
        {
            _keyboardId = keyboardId;
            CreateCache(keyboardId, out _keyTypeToKeyInputMap, out _charToModifierMap);
        }

        /// <summary>
        /// Try and get the KeyInput for the specified char and ModifierKeys.  This will 
        /// check and see if this is a well known char.  Many well known chars are expected
        /// to come with certain modifiers (for instance on US keyboard # is expected to
        /// have Shift applied).  These will be removed from the ModifierKeys passed in and
        /// whatever is left will be applied to the resulting KeyInput.  
        /// </summary>
        internal KeyInput GetKeyInput(char c, ModifierKeys modifierKeys)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            ModifierKeys expected;
            if (_charToModifierMap.TryGetValue(c, out expected))
            {
                modifierKeys &= ~expected;
            }

            if (modifierKeys != ModifierKeys.None)
            {
                var keyModifiers = ConvertToKeyModifiers(modifierKeys);
                keyInput = KeyInputUtil.ChangeKeyModifiers(keyInput, keyModifiers);
            }

            return keyInput;
        }

        /// <summary>
        /// Try and get the KeyInput which corresponds to the given keyboard Key.  Modifiers
        /// are not considered here
        /// </summary>
        internal bool TryGetKeyInput(Key key, out KeyInput keyInput)
        {
            return TryGetKeyInput(key, ModifierKeys.None, out keyInput);
        }

        /// <summary>
        /// Try and get the KeyInput which corresponds to the given Key and modifiers
        ///
        /// Warning: Think very hard before modifying this method.  It's very important to
        /// consider non English keyboards and languages here
        /// </summary>
        internal bool TryGetKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            // First just check and see if there is a direct mapping
            var keyType = new KeyType(key, modifierKeys);
            if (_keyTypeToKeyInputMap.TryGetValue(keyType, out keyInput))
            {
                return true;
            }

            // Next consider only the shift key part of the requested modifier.  We can 
            // re-apply the original modifiers later 
            keyType = new KeyType(key, modifierKeys & ModifierKeys.Shift);
            if (_keyTypeToKeyInputMap.TryGetValue(keyType, out keyInput))
            {
                // Reapply the modifiers
                keyInput = KeyInputUtil.ChangeKeyModifiers(keyInput, ConvertToKeyModifiers(modifierKeys));
                return true;
            }

            // Last consider it without any modifiers and reapply
            keyType = new KeyType(key, ModifierKeys.None);
            if (_keyTypeToKeyInputMap.TryGetValue(keyType, out keyInput))
            {
                // Reapply the modifiers
                keyInput = KeyInputUtil.ChangeKeyModifiers(keyInput, ConvertToKeyModifiers(modifierKeys));
                return true;
            }

            return false;
        }

        internal bool TryGetKey(VimKey vimKey, out Key key)
        {
            int virtualKeyCode;
            if (!TryVimKeyToVirtualKey(vimKey, out virtualKeyCode))
            {
                key = Key.None;
                return false;
            }

            key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
            return true;
        }

        /// <summary>
        /// Under the hood we map KeyInput values into actual input by one of two mechanisms
        ///
        ///  1. Straight mapping of a VimKey to a Virtual Key Code.
        ///  2. Mapping of the character to a virtual key code and set of modifier keys
        ///
        /// This method will return true if the VimKey is mapped using method 2
        ///
        /// Generally speaking a KeyInput is mapped by character if it has an associated 
        /// char value.  This is not true for certain special cases like Enter, Tab and 
        /// the Keypad values.
        /// </summary>
        internal static bool IsMappedByCharacter(VimKey vimKey)
        {
            int virtualKey;
            return !TryVimKeyToVirtualKey(vimKey, out virtualKey);
        }

        private static KeyModifiers ConvertToKeyModifiers(ModifierKeys keys)
        {
            var res = KeyModifiers.None;
            if (0 != (keys & ModifierKeys.Shift))
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

        private static Dictionary<KeyType, KeyInput> CreateCache(
            IntPtr keyboardId,
            out Dictionary<KeyType, KeyInput> cache,
            out Dictionary<char, ModifierKeys> charToKeyModifiersMap)
        {
            cache = new Dictionary<KeyType, KeyInput>();
            charToKeyModifiersMap = new Dictionary<char, ModifierKeys>();

            foreach (var current in KeyInputUtil.VimKeyInputList)
            {
                int virtualKeyCode;
                ModifierKeys modKeys;
                if (!TryGetVirtualKeyAndModifiers(keyboardId, current, out virtualKeyCode, out modKeys))
                {
                    continue;
                }

                // If this is backed by a real character then store the modifiers which are needed
                // to produce this char.  Later we can compare the current modifiers to this value
                // and find the extra modifiers to apply to the KeyInput given to Vim
                if (current.KeyModifiers == KeyModifiers.None && IsMappedByCharacter(current.Key))
                {
                    charToKeyModifiersMap[current.Char] = modKeys;
                }

                // Only processing items which can map to actual keys
                var key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
                if (Key.None == key)
                {
                    continue;
                }

                var keyType = new KeyType(key, modKeys);
                cache[keyType] = current;
            }

            return cache;
        }

        /// <summary>
        /// Try and get the Virtual Key Code and Modifiers for the given KeyInput.  
        /// </summary>
        private static bool TryGetVirtualKeyAndModifiers(IntPtr hkl, KeyInput keyInput, out int virtualKeyCode, out ModifierKeys modKeys)
        {
            if (TryVimKeyToVirtualKey(keyInput.Key, out virtualKeyCode))
            {
                Debug.Assert(!IsMappedByCharacter(keyInput.Key));
                modKeys = ModifierKeys.None;
                return true;
            }
            else
            {
                Debug.Assert(IsMappedByCharacter(keyInput.Key));
                Debug.Assert(keyInput.KeyModifiers == KeyModifiers.None);
                return TryMapCharToVirtualKeyAndModifiers(hkl, keyInput.Char, out virtualKeyCode, out modKeys);
            }
        }

        /// <summary>
        ///
        /// All constant values derived from the list at the following 
        /// location
        ///   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
        /// 
        /// </summary>
        private static bool TryVimKeyToVirtualKey(VimKey vimKey, out int virtualKeyCode)
        {
            var found = true;
            switch (vimKey)
            {
                case VimKey.Enter: virtualKeyCode = 0xD; break;
                case VimKey.Tab: virtualKeyCode = 0x9; break;
                case VimKey.Escape: virtualKeyCode = 0x1B; break;
                case VimKey.LineFeed: virtualKeyCode = 0; break;
                case VimKey.Back: virtualKeyCode = 0x8; break;
                case VimKey.Delete: virtualKeyCode = 0x2E; break;
                case VimKey.Left: virtualKeyCode = 0x25; break;
                case VimKey.Up: virtualKeyCode = 0x26; break;
                case VimKey.Right: virtualKeyCode = 0x27; break;
                case VimKey.Down: virtualKeyCode = 0x28; break;
                case VimKey.Help: virtualKeyCode = 0x2F; break;
                case VimKey.Insert: virtualKeyCode = 0x2D; break;
                case VimKey.Home: virtualKeyCode = 0x24; break;
                case VimKey.End: virtualKeyCode = 0x23; break;
                case VimKey.PageUp: virtualKeyCode = 0x21; break;
                case VimKey.PageDown: virtualKeyCode = 0x22; break;
                case VimKey.Break: virtualKeyCode = 0x03; break;
                case VimKey.F1: virtualKeyCode = 0x70; break;
                case VimKey.F2: virtualKeyCode = 0x71; break;
                case VimKey.F3: virtualKeyCode = 0x72; break;
                case VimKey.F4: virtualKeyCode = 0x73; break;
                case VimKey.F5: virtualKeyCode = 0x74; break;
                case VimKey.F6: virtualKeyCode = 0x75; break;
                case VimKey.F7: virtualKeyCode = 0x76; break;
                case VimKey.F8: virtualKeyCode = 0x77; break;
                case VimKey.F9: virtualKeyCode = 0x78; break;
                case VimKey.F10: virtualKeyCode = 0x79; break;
                case VimKey.F11: virtualKeyCode = 0x7a; break;
                case VimKey.F12: virtualKeyCode = 0x7b; break;
                case VimKey.KeypadMultiply: virtualKeyCode = 0x6A; break;
                case VimKey.KeypadPlus: virtualKeyCode = 0x6B; break;
                case VimKey.KeypadMinus: virtualKeyCode = 0x6D; break;
                case VimKey.KeypadDecimal: virtualKeyCode = 0x6E; break;
                case VimKey.KeypadDivide: virtualKeyCode = 0x6F; break;
                case VimKey.Keypad0: virtualKeyCode = 0x60; break;
                case VimKey.Keypad1: virtualKeyCode = 0x61; break;
                case VimKey.Keypad2: virtualKeyCode = 0x62; break;
                case VimKey.Keypad3: virtualKeyCode = 0x63; break;
                case VimKey.Keypad4: virtualKeyCode = 0x64; break;
                case VimKey.Keypad5: virtualKeyCode = 0x65; break;
                case VimKey.Keypad6: virtualKeyCode = 0x66; break;
                case VimKey.Keypad7: virtualKeyCode = 0x67; break;
                case VimKey.Keypad8: virtualKeyCode = 0x68; break;
                case VimKey.Keypad9: virtualKeyCode = 0x69; break;
                default:
                    virtualKeyCode = 0;
                    found = false;
                    break;
            }

            return found;
        }

        private static bool TryMapCharToVirtualKeyAndModifiers(IntPtr hkl, char c, out int virtualKeyCode, out ModifierKeys modKeys)
        {
            var res = NativeMethods.VkKeyScanEx(c, hkl);

            // The virtual key code is the low byte and the shift state is the high byte
            var virtualKey = res & 0xff;
            var state = ((res >> 8) & 0xff);
            if (virtualKey == -1 || state == -1)
            {
                virtualKeyCode = 0;
                modKeys = ModifierKeys.None;
                return false;
            }

            var shiftMod = (state & 0x1) != 0 ? ModifierKeys.Shift : ModifierKeys.None;
            var controlMod = (state & 0x2) != 0 ? ModifierKeys.Control : ModifierKeys.None;
            var altMod = (state & 0x4) != 0 ? ModifierKeys.Alt : ModifierKeys.None;
            virtualKeyCode = virtualKey;
            modKeys = shiftMod | controlMod | altMod;
            return true;
        }
    }
}
