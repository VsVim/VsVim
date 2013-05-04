using System.Linq;
using System.Windows.Input;
using Vim.UI.Wpf.Implementation.Keyboard;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    // [Export(typeof(IKeyUtil))]
    internal sealed class KeyUtil : IKeyUtil
    {
        private KeyboardMap _keyboardMap;

        private KeyboardMap GetOrCreateKeyboardMap()
        {
            var keyboardId = NativeMethods.GetKeyboardLayout(0);
            if (_keyboardMap == null || _keyboardMap.KeyboardId != keyboardId)
            {
                _keyboardMap = new KeyboardMap(keyboardId);
            }

            return _keyboardMap;
        }

        internal static KeyModifiers GetKeyModifiers(ModifierKeys keys)
        {
            return KeyboardMap.ConvertToKeyModifiers(keys);
        }

        /// <summary>
        /// Is this a known dead key
        /// </summary>
        internal bool IsDeadKey(Key key)
        {
            return GetOrCreateKeyboardMap().IsDeadKey(key, ModifierKeys.None);
        }

        /// <summary>
        /// Is this the AltGr key combination.  This is not directly representable in WPF
        /// logic but the best that can be done is to check for Alt + Control
        /// </summary>
        internal static bool IsAltGr(ModifierKeys modifierKeys)
        {
            return modifierKeys == (ModifierKeys.Alt | ModifierKeys.Control);
        }

        internal KeyInput GetKeyInput(char c, ModifierKeys modifierKeys)
        {
            return KeyboardMap.GetKeyInput(c, IsAltGr(modifierKeys) ? ModifierKeys.None : modifierKeys);
        }

        internal bool TryConvertToKeyInput(Key key, out KeyInput keyInput)
        {
            return GetOrCreateKeyboardMap().TryGetKeyInput(key, out keyInput);
        }

        internal bool TryConvertToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            return GetOrCreateKeyboardMap().TryGetKeyInput(key, modifierKeys, out keyInput);
        }

        /// <summary>
        /// Try and convert the VimKey to a WPF key.  Note this is not a lossy conversion.  If any
        /// ModifierKeys would be necessary to produce the VimKey then the conversion will fail
        /// </summary>
        internal bool TryConvertToKeyOnly(VimKey vimKey, out Key key)
        {
            IEnumerable<KeyState> keyStates;
            if (GetOrCreateKeyboardMap().TryGetKey(vimKey, out keyStates))
            {
                var keyState = keyStates.FirstOrDefault(x => x.ModifierKeys == ModifierKeys.None);
                key = keyState.Key;
                return key != default(Key);
            }

            key = default(Key);
            return false;
        }

        /// <summary>
        /// Try and convert the VimKey to a WPF key.  Note this is a lossy conversion as modifiers
        /// will be dropped.  So for example UpperN will still map to Key.N but the fidelity of
        /// shift will be lost
        /// </summary>
        internal bool TryConvertToKey(VimKey vimKey, out IEnumerable<Key> keys)
        {
            IEnumerable<KeyState> keyStates;
            if (GetOrCreateKeyboardMap().TryGetKey(vimKey, out keyStates))
            {
                keys = keyStates.Select(x => x.Key);
                return true;
            }

            keys = null;
            return false;
        }

        #region IKeyUtil

        /*
        bool IKeyUtil.IsDeadKey(Key key)
        {
            return IsDeadKey(key);
        }
        */

        bool IKeyUtil.IsAltGr(ModifierKeys modifierKeys)
        {
            return IsAltGr(modifierKeys);
        }

        bool IKeyUtil.TryConvertSpecialToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            return TryConvertToKeyInput(key, modifierKeys, out keyInput);
        }

        /*
        KeyInput IKeyUtil.GetKeyInput(char c, ModifierKeys modifierKeys)
        {
            return GetKeyInput(c, modifierKeys);
        }
        */

        KeyModifiers IKeyUtil.GetKeyModifiers(ModifierKeys modifierKeys)
        {
            return GetKeyModifiers(modifierKeys);
        }

        #endregion
    }
}
