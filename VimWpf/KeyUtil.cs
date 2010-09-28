using System;
using System.Collections.Generic;
using System.Windows.Input;
using Vim.Extensions;

namespace Vim.UI.Wpf
{
    public static class KeyUtil
    {
        private static IntPtr? _keyboardId;
        private static Dictionary<Tuple<Key, ModifierKeys>, KeyInput> _cache;

        private static Dictionary<Tuple<Key, ModifierKeys>, KeyInput> GetOrCreateCache()
        {
            if (_cache == null
                || !_keyboardId.HasValue
                || _keyboardId.Value != NativeMethods.GetKeyboardLayout(0))
            {
                CreateCache();
            }

            return _cache;
        }

        private static void CreateCache()
        {
            var id = NativeMethods.GetKeyboardLayout(0);
            var cache = new Dictionary<Tuple<Key, ModifierKeys>, KeyInput>();

            foreach (var current in KeyInputUtil.CoreKeyInputList)
            {
                // TODO: Need to fix for non-chars
                if (current.RawChar.IsNone())
                {
                    continue;
                }

                int virtualKeyCode;
                ModifierKeys modKeys;
                if (!TryMapCharToVirtualKeyAndModifiers(id, current.Char, out virtualKeyCode, out modKeys))
                {
                    continue;
                }

                // Only processing items which can map to acual keys
                var key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
                if (Key.None == key)
                {
                    continue;
                }

                var tuple = Tuple.Create(key, modKeys);
                cache[tuple] = current;
            }

            _keyboardId = id;
            _cache = cache;
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

        /// <summary>
        /// Is this the AltGr key combination.  This is not directly representable in WPF
        /// logic but the best that can be done is to check for Alt + Control
        /// </summary>
        public static bool IsAltGr(ModifierKeys modifierKeys)
        {
            return modifierKeys == (ModifierKeys.Alt | ModifierKeys.Control);
        }

        /// <summary>
        /// When the user is typing we get events for every single key press.  This means that 
        /// typing something like an upper case character will cause at least 2 events to be
        /// generated.  
        ///  1) LeftShift 
        ///  2) LeftShift + b
        /// This helps us filter out items like #1 which we don't want to process
        /// </summar>
        public static bool IsNonInputKey(Key k)
        {
            switch (k)
            {
                case Key.LeftAlt:
                case Key.LeftCtrl:
                case Key.LeftShift:
                case Key.RightAlt:
                case Key.RightCtrl:
                case Key.RightShift:
                case Key.System:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsInputKey(Key k)
        {
            return !IsNonInputKey(k);
        }

        public static KeyModifiers ConvertToKeyModifiers(ModifierKeys keys)
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

        public static ModifierKeys ConvertToModifierKeys(KeyModifiers keys)
        {
            var res = ModifierKeys.None;
            if (0 != (keys & KeyModifiers.Shift))
            {
                res |= ModifierKeys.Shift;
            }
            if (0 != (keys & KeyModifiers.Control))
            {
                res |= ModifierKeys.Control;
            }
            if (0 != (keys & KeyModifiers.Alt))
            {
                res |= ModifierKeys.Alt;
            }
            return res;
        }

        public static bool TryConvertToKeyInput(Key key, out KeyInput keyInput)
        {
            return TryConvertToKeyInput(key, ModifierKeys.None, out keyInput);
        }

        public static bool TryConvertToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            // Only consider the Shift modifier key when doing the lookup.  The cache only contains the 
            // KeyInput's with no and shift modifiers.  
            var tuple = Tuple.Create(key, modifierKeys & ModifierKeys.Shift);
            if (GetOrCreateCache().TryGetValue(tuple, out keyInput))
            {
                // Reapply the modifiers
                keyInput = KeyInputUtil.ChangeKeyModifiers(keyInput, ConvertToKeyModifiers(modifierKeys));
                return true;
            }

            return false;
        }

        public static bool TryConvertToKey(VimKey vimKey, out Key key)
        {
            // TODO: Code this 
            key = Key.None;
            return false;
        }
    }
}
