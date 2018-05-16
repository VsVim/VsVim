using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IKeyUtil))]
    internal sealed class AlternateKeyUtil : IKeyUtil
    {
        private static readonly Dictionary<Key, KeyInput> s_wpfKeyToKeyInputMap;
        private static readonly Dictionary<Key, KeyInput> s_wpfControlKeyToKeyInputMap;
        private static readonly Dictionary<VimKey, Key> s_vimKeyToWpfKeyMap;
        private static readonly Dictionary<KeyInput, Key> s_keyInputToWpfKeyMap;

        static AlternateKeyUtil()
        {
            s_vimKeyToWpfKeyMap = BuildVimKeyToWpfKeyMap();
            s_keyInputToWpfKeyMap = BuildKeyInputToWpfKeyMap(s_vimKeyToWpfKeyMap);
            s_wpfKeyToKeyInputMap = new Dictionary<Key, KeyInput>();
            foreach (var pair in s_keyInputToWpfKeyMap)
            {
                s_wpfKeyToKeyInputMap[pair.Value] = pair.Key;
            }
            s_wpfControlKeyToKeyInputMap = BuildWpfControlKeyToKeyInputMap();
        }

        private static Dictionary<Key, KeyInput> BuildWpfControlKeyToKeyInputMap()
        {
            var map = new Dictionary<Key, KeyInput>
            {
                [Key.D2] = KeyInputUtil.VimKeyToKeyInput(VimKey.Null), // <C-@>
                [Key.D6] = KeyInputUtil.CharToKeyInput((char)0x1E), // <C-^>
                [Key.OemMinus] = KeyInputUtil.CharToKeyInput((char)0x1F), // <C-_>
                [Key.OemQuestion] = KeyInputUtil.CharToKeyInput((char)0x7F), // <C-?>
            };

            return map;
        }

        internal static Dictionary<VimKey, Key> BuildVimKeyToWpfKeyMap()
        {
            var map = new Dictionary<VimKey, Key>
            {
                [VimKey.Enter] = Key.Enter,
                [VimKey.Escape] = Key.Escape,
                [VimKey.Back] = Key.Back,
                [VimKey.Delete] = Key.Delete,
                [VimKey.Left] = Key.Left,
                [VimKey.Up] = Key.Up,
                [VimKey.Right] = Key.Right,
                [VimKey.Down] = Key.Down,
                [VimKey.Help] = Key.Help,
                [VimKey.Insert] = Key.Insert,
                [VimKey.Home] = Key.Home,
                [VimKey.End] = Key.End,
                [VimKey.PageUp] = Key.PageUp,
                [VimKey.PageDown] = Key.PageDown,
                [VimKey.Tab] = Key.Tab,
                [VimKey.F1] = Key.F1,
                [VimKey.F2] = Key.F2,
                [VimKey.F3] = Key.F3,
                [VimKey.F4] = Key.F4,
                [VimKey.F5] = Key.F5,
                [VimKey.F6] = Key.F6,
                [VimKey.F7] = Key.F7,
                [VimKey.F8] = Key.F8,
                [VimKey.F9] = Key.F9,
                [VimKey.F10] = Key.F10,
                [VimKey.F11] = Key.F11,
                [VimKey.F12] = Key.F12,
                [VimKey.KeypadMultiply] = Key.Multiply,
                [VimKey.KeypadPlus] = Key.Add,
                [VimKey.KeypadMinus] = Key.Subtract,
                [VimKey.KeypadDecimal] = Key.Decimal,
                [VimKey.KeypadDivide] = Key.Divide,
                [VimKey.KeypadEnter] = Key.Separator,
                [VimKey.Keypad0] = Key.NumPad0,
                [VimKey.Keypad1] = Key.NumPad1,
                [VimKey.Keypad2] = Key.NumPad2,
                [VimKey.Keypad3] = Key.NumPad3,
                [VimKey.Keypad4] = Key.NumPad4,
                [VimKey.Keypad5] = Key.NumPad5,
                [VimKey.Keypad6] = Key.NumPad6,
                [VimKey.Keypad7] = Key.NumPad7,
                [VimKey.Keypad8] = Key.NumPad8,
                [VimKey.Keypad9] = Key.NumPad9
            };

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
            return s_vimKeyToWpfKeyMap.TryGetValue(vimKey, out key);
        }

        internal static VimKeyModifiers ConvertToKeyModifiers(ModifierKeys keys)
        {
            var res = VimKeyModifiers.None;
            if (0 != (keys & ModifierKeys.Shift))
            {
                res = res | VimKeyModifiers.Shift;
            }
            if (0 != (keys & ModifierKeys.Alt))
            {
                res = res | VimKeyModifiers.Alt;
            }
            if (0 != (keys & ModifierKeys.Control))
            {
                res = res | VimKeyModifiers.Control;
            }
            return res;
        }

        internal static ModifierKeys ConvertToModifierKeys(VimKeyModifiers keys)
        {
            var res = ModifierKeys.None;
            if (0 != (keys & VimKeyModifiers.Shift))
            {
                res = res | ModifierKeys.Shift;
            }
            if (0 != (keys & VimKeyModifiers.Alt))
            {
                res = res | ModifierKeys.Alt;
            }
            if (0 != (keys & VimKeyModifiers.Control))
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

        VimKeyModifiers IKeyUtil.GetKeyModifiers(ModifierKeys modifierKeys)
        {
            return ConvertToKeyModifiers(modifierKeys);
        }

        bool IKeyUtil.TryConvertSpecialToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            if (s_wpfKeyToKeyInputMap.TryGetValue(key, out keyInput))
            {
                var keyModifiers = ConvertToKeyModifiers(modifierKeys);
                keyInput = KeyInputUtil.ApplyKeyModifiers(keyInput, keyModifiers);
                return true;
            }

            if ((modifierKeys & ModifierKeys.Control) != 0 &&
                s_wpfControlKeyToKeyInputMap.TryGetValue(key, out keyInput))
            {
                return true;
            }

            keyInput = null;
            return false;
        }

        #endregion
    }
}
