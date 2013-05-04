using System;
using System.Collections.Generic;
using System.Windows.Input;
using Vim.UI.Wpf.Implementation.Keyboard;

namespace Vim.UI.Wpf.UnitTest
{
    internal class MockVirtualKeyboard : IVirtualKeyboard
    {
        private readonly Dictionary<KeyState, string> _keyMap = new Dictionary<KeyState,string>();
        private readonly KeyboardState _keyboardState = new KeyboardState();

        internal KeyboardState KeyboardState { get { return _keyboardState; } }
        internal Dictionary<KeyState, string> KeyMap { get { return _keyMap; } }
        internal bool UsesExtendedModifiers { get; set; }
        internal bool IsCapsLockToggled { get; set; }
        internal VirtualKeyModifiers VirtualKeyModifiersExtended { get; set; }
        internal uint? Oem1Modifier { get; set; }
        internal uint? Oem2Modifier { get; set; }

        internal MockVirtualKeyboard()
        {
            // Build up a simple subset of a QWERTY keyboard 
            int start = (int)Key.A;
            int end = (int)Key.Z;
            for (int i = start; i <= end; i++)
            {
                var key = (Key)i;

                // Lower case 
                var keyState = new KeyState(key, VirtualKeyModifiers.None);
                _keyMap[keyState] = key.ToString().ToLower();

                // Upper case
                var upper = key.ToString().ToUpper();
                _keyMap[new KeyState(key, VirtualKeyModifiers.Shift)] = upper;
                _keyMap[new KeyState(key, VirtualKeyModifiers.CapsLock)] = upper;
            }

            // Add in some keys which don't change on caps lock for good measure
            start = (int)Key.D0;
            end = (int)Key.D9;
            for (int i = start; i <= end; i++)
            {
                var number = i - start;
                var key = (Key)i;
                var text = number.ToString();
                _keyMap[new KeyState(key, VirtualKeyModifiers.None)] = text;
                _keyMap[new KeyState(key, VirtualKeyModifiers.CapsLock)] = text;
            }

            // Throw in some keys which have the same character mapping for the keypad
            // and non-keypad combinations
            _keyMap[new KeyState(Key.Multiply, VirtualKeyModifiers.None)] = "*";
            _keyMap[new KeyState(Key.D8, VirtualKeyModifiers.Shift)] = "*";
        }

        #region IVirtualKeyboard

        KeyboardState IVirtualKeyboard.KeyboardState
        {
            get { return KeyboardState; }
        }

        bool IVirtualKeyboard.UsesExtendedModifiers
        {
            get { return UsesExtendedModifiers; }
        }

        bool IVirtualKeyboard.IsCapsLockToggled
        {
            get { return IsCapsLockToggled; }
        }

        VirtualKeyModifiers IVirtualKeyboard.VirtualKeyModifiersExtended
        {
            get { return VirtualKeyModifiersExtended; }
        }

        bool IVirtualKeyboard.TryMapChar(char c, out uint virtualKey, out VirtualKeyModifiers virtualKeyModifiers)
        {
            foreach (var pair in _keyMap)
            {
                if (pair.Value[0] == c)
                {
                    virtualKey = (uint)KeyInterop.VirtualKeyFromKey(pair.Key.Key);
                    virtualKeyModifiers = pair.Key.Modifiers;
                    return true;
                }
            }

            virtualKey = 0;
            virtualKeyModifiers = VirtualKeyModifiers.None;
            return false;
        }

        bool IVirtualKeyboard.TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text, out bool isDeadKey)
        {
            try
            {
                if (VirtualKeyModifiers.Oem1 == (virtualKeyModifiers & VirtualKeyModifiers.Oem1))
                {
                    if (!Oem1Modifier.HasValue)
                    {
                        text = String.Empty;
                        isDeadKey = false;
                        return false;
                    }
                }

                if (Oem1Modifier.HasValue && KeyboardState.IsKeySet(Oem1Modifier.Value))
                {
                    virtualKeyModifiers |= VirtualKeyModifiers.Oem1;
                }

                if (VirtualKeyModifiers.Oem2 == (virtualKeyModifiers & VirtualKeyModifiers.Oem2))
                {
                    if (!Oem2Modifier.HasValue)
                    {
                        text = String.Empty;
                        isDeadKey = false;
                        return false;
                    }
                }

                if (Oem2Modifier.HasValue && KeyboardState.IsKeySet(Oem2Modifier.Value))
                {
                    virtualKeyModifiers |= VirtualKeyModifiers.Oem2;
                }

                var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
                var keyState = new KeyState(key, virtualKeyModifiers);
                isDeadKey = false;
                return _keyMap.TryGetValue(keyState, out text);
            }
            finally
            {
                _keyboardState.Clear();
            }
        }

        #endregion
    }
}
