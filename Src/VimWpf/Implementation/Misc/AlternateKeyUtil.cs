using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IKeyUtil))]
    internal sealed class AlternateKeyUtil : IKeyUtil
    {
        private static readonly Dictionary<Key, KeyInput> WpfKeyToKeyInputMap;
        private static readonly Dictionary<VimKey, Key> VimKeyToWpfKeyMap;
        private static readonly Dictionary<KeyInput, Key> KeyInputToWpfKeyMap;

        static AlternateKeyUtil()
        {
            VimKeyToWpfKeyMap = BuildVimKeyToWpfKeyMap();
            KeyInputToWpfKeyMap = BuildKeyInputToWpfKeyMap(VimKeyToWpfKeyMap);
            WpfKeyToKeyInputMap = new Dictionary<Key, KeyInput>();
            foreach (var pair in KeyInputToWpfKeyMap)
            {
                WpfKeyToKeyInputMap[pair.Value] = pair.Key;
            }
        }

        internal static Dictionary<VimKey, Key> BuildVimKeyToWpfKeyMap()
        {
            var map = new Dictionary<VimKey, Key>();
            map[VimKey.Enter] = Key.Enter;
            map[VimKey.Escape] = Key.Escape;
            map[VimKey.Back] = Key.Back;
            map[VimKey.Delete] = Key.Delete;
            map[VimKey.Left] = Key.Left;
            map[VimKey.Up] = Key.Up;
            map[VimKey.Right] = Key.Right;
            map[VimKey.Down] = Key.Down;
            map[VimKey.Help] = Key.Help;
            map[VimKey.Insert] = Key.Insert;
            map[VimKey.Home] = Key.Home;
            map[VimKey.End] = Key.End;
            map[VimKey.PageUp] = Key.PageUp;
            map[VimKey.PageDown] = Key.PageDown;
            map[VimKey.Tab] = Key.Tab;
            map[VimKey.F1] = Key.F1;
            map[VimKey.F2] = Key.F2;
            map[VimKey.F3] = Key.F3;
            map[VimKey.F4] = Key.F4;
            map[VimKey.F5] = Key.F5;
            map[VimKey.F6] = Key.F6;
            map[VimKey.F7] = Key.F7;
            map[VimKey.F8] = Key.F8;
            map[VimKey.F9] = Key.F9;
            map[VimKey.F10] = Key.F10;
            map[VimKey.F11] = Key.F11;
            map[VimKey.F12] = Key.F12;
            map[VimKey.KeypadMultiply] = Key.Multiply;
            map[VimKey.KeypadPlus] = Key.Add;
            map[VimKey.KeypadMinus] = Key.Subtract;
            map[VimKey.KeypadDecimal] = Key.Decimal;
            map[VimKey.KeypadDivide] = Key.Divide;
            map[VimKey.KeypadEnter] = Key.Separator;
            map[VimKey.Keypad0] = Key.NumPad0;
            map[VimKey.Keypad1] = Key.NumPad1;
            map[VimKey.Keypad2] = Key.NumPad2;
            map[VimKey.Keypad3] = Key.NumPad3;
            map[VimKey.Keypad4] = Key.NumPad4;
            map[VimKey.Keypad5] = Key.NumPad5;
            map[VimKey.Keypad6] = Key.NumPad6;
            map[VimKey.Keypad7] = Key.NumPad7;
            map[VimKey.Keypad8] = Key.NumPad8;
            map[VimKey.Keypad9] = Key.NumPad9;

            return map;
        }

        internal static Dictionary<KeyInput, Key> BuildKeyInputToWpfKeyMap(Dictionary<VimKey, Key> vimKeyToWpfKeyMap)
        {
            var map = new Dictionary<KeyInput, Key>();
            foreach (var pair in vimKeyToWpfKeyMap)
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(pair.Key);
                map[keyInput] = pair.Value;
            }

            map[KeyInputUtil.CharToKeyInput(' ')] = Key.Space;
            map[KeyInputUtil.CharToKeyInput('\t')] = Key.Tab;

            return map;
        }

        internal static bool TrySpecialVimKeyToKey(VimKey vimKey, out Key key)
        {
            return VimKeyToWpfKeyMap.TryGetValue(vimKey, out key);

        }

        internal static KeyModifiers ConvertToKeyModifiers(ModifierKeys keys)
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

        internal static ModifierKeys ConvertToModifierKeys(KeyModifiers keys)
        {
            var res = ModifierKeys.None;
            if (0 != (keys & KeyModifiers.Shift))
            {
                res = res | ModifierKeys.Shift;
            }
            if (0 != (keys & KeyModifiers.Alt))
            {
                res = res | ModifierKeys.Alt;
            }
            if (0 != (keys & KeyModifiers.Control))
            {
                res = res | ModifierKeys.Control;
            }
            return res;
        }

        #region IKeyUtil

        bool IKeyUtil.IsAltGr(ModifierKeys modifierKeys)
        {
            return modifierKeys == (ModifierKeys.Alt | ModifierKeys.Control);
        }

        KeyModifiers IKeyUtil.GetKeyModifiers(ModifierKeys modifierKeys)
        {
            return ConvertToKeyModifiers(modifierKeys);
        }

        bool IKeyUtil.TryConvertSpecialToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            if (WpfKeyToKeyInputMap.TryGetValue(key, out keyInput))
            {
                var keyModifiers = ConvertToKeyModifiers(modifierKeys);
                keyInput = KeyInputUtil.ApplyModifiers(keyInput, keyModifiers);
                return true;
            }

            keyInput = null;
            return false;
        }

        #endregion
    }
}
