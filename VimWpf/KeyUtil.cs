using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Vim.Extensions;

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

        private static ReadOnlyCollection<char> CreateCoreChars()
        {
            return KeyInputUtil.CoreCharacters.ToList().AsReadOnly();
        }

        private static ReadOnlyCollection<Tuple<char, int, KeyModifiers>> CreateMappedCoreChars()
        {
            var list = CoreChars
                .Select(x => Tuple.Create(x, KeyInputUtil.TryCharToVirtualKeyAndModifiers(x)))
                .Where(x => x.Item2.IsSome())
                .Select(x => Tuple.Create(x.Item1, x.Item2.Value.Item1, x.Item2.Value.Item2))
                .ToList();
            return new ReadOnlyCollection<Tuple<char, int, KeyModifiers>>(list);
        }

        public static Tuple<Key, ModifierKeys> ConvertToKeyAndModifiers(KeyInput input)
        {
            var mods = ConvertToModifierKeys(input.KeyModifiers);
            var option = KeyInputUtil.TryCharToVirtualKeyAndModifiers(input.Char);
            var key = Key.None;
            if (option.IsSome())
            {
                key = KeyInterop.KeyFromVirtualKey(option.Value.Item1);
            }

            return Tuple.Create(key, mods);
        }

        public static Key ConvertToKey(KeyInput input)
        {
            return ConvertToKeyAndModifiers(input).Item1;
        }
    }
}
