using System;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    public static class KeyUtil
    {
        public static KeyInput ConvertToKeyInput(Key key)
        {
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            return KeyInputUtil.VirtualKeyCodeToKeyInput(virtualKey);
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

        public static KeyInput ConvertToKeyInput(Key key, ModifierKeys modifierKeys)
        {
            var modKeys = ConvertToKeyModifiers(modifierKeys);
            var original = ConvertToKeyInput(key);
            return KeyInputUtil.ChangeKeyModifiers(original, modKeys);
        }

        public static Tuple<Key, ModifierKeys> ConvertToKeyAndModifiers(KeyInput input)
        {
            var mods = ConvertToModifierKeys(input.KeyModifiers);
            var key = KeyInterop.KeyFromVirtualKey(input.VirtualKeyCode);
            return Tuple.Create(key, mods);
        }

        public static Key ConvertToKey(KeyInput input)
        {
            return ConvertToKeyAndModifiers(input).Item1;
        }
    }
}
