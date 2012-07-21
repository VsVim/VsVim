using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    [Export(typeof(IKeyUtil))]
    internal sealed class AlternateKeyUtil : IKeyUtil
    {
        private static readonly Dictionary<Key, VimKey> WpfKeyToVimKeyMap;
        private static readonly Dictionary<VimKey, Key> VimKeyToWpfKeyMap;

        static AlternateKeyUtil()
        {
            WpfKeyToVimKeyMap = new Dictionary<Key, VimKey>();
            VimKeyToWpfKeyMap = new Dictionary<VimKey, Key>();
            foreach (var keyInput in KeyInputUtil.VimKeyInputList)
            {
                if (keyInput.KeyModifiers != KeyModifiers.None)
                {
                    continue;
                }

                uint virtualKeyCode;
                if (!TrySpecialVimKeyToVirtualKey(keyInput.Key, out virtualKeyCode))
                {
                    continue;
                }

                var wpfKey = KeyInterop.KeyFromVirtualKey((int)virtualKeyCode);
                if (wpfKey == Key.None)
                {
                    continue;
                }

                WpfKeyToVimKeyMap[wpfKey] = keyInput.Key;
                VimKeyToWpfKeyMap[keyInput.Key] = wpfKey;
            }
        }

        internal static bool TrySpecialVimKeyToKey(VimKey vimKey, out Key key)
        {
            return VimKeyToWpfKeyMap.TryGetValue(vimKey, out key);
        }

        /// <summary>
        /// Get the virtual key code for the provided VimKey.  This will only work for Vim keys which
        /// are meant for very specific keys.  It doesn't work for alphas
        ///
        /// All constant values derived from the list at the following 
        /// location
        ///   http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx
        ///
        /// </summary>
        private static bool TrySpecialVimKeyToVirtualKey(VimKey vimKey, out uint virtualKeyCode)
        {
            var found = true;
            switch (vimKey)
            {
                case VimKey.Enter: virtualKeyCode = 0xD; break;
                case VimKey.Tab: virtualKeyCode = 0x9; break;
                case VimKey.Escape: virtualKeyCode = 0x1B; break;
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

        #region IKeyUtil

        bool IKeyUtil.IsAltGr(ModifierKeys modifierKeys)
        {
            return modifierKeys == (ModifierKeys.Alt | ModifierKeys.Control);
        }

        KeyModifiers IKeyUtil.GetKeyModifiers(ModifierKeys modifierKeys)
        {
            return KeyboardMap.ConvertToKeyModifiers(modifierKeys);
        }

        bool IKeyUtil.TryConvertSpecialToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            VimKey vimKey;
            if (WpfKeyToVimKeyMap.TryGetValue(key, out vimKey))
            {
                var keyModifiers = KeyboardMap.ConvertToKeyModifiers(modifierKeys);
                keyInput = KeyInputUtil.ApplyModifiersToVimKey(vimKey, keyModifiers);
                return true;
            }

            keyInput = null;
            return false;
        }

        #endregion
    }
}
