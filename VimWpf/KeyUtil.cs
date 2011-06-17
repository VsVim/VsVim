using System.Windows.Input;

namespace Vim.UI.Wpf
{
    public static class KeyUtil
    {
        private static KeyboardMap _keyboardMap;

        private static KeyboardMap GetOrCreateKeyboardMap()
        {
            var keyboardId = NativeMethods.GetKeyboardLayout(0);
            if (_keyboardMap == null || _keyboardMap.KeyboardId != keyboardId)
            {
                _keyboardMap = new KeyboardMap(keyboardId);
            }

            return _keyboardMap;
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
        /// </summary>
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

        public static bool IsMappedByChar(VimKey vimKey)
        {
            return KeyboardMap.IsMappedByCharacter(vimKey);
        }

        public static KeyInput CharAndModifiersToKeyInput(char c, ModifierKeys modifierKeys)
        {
            return GetOrCreateKeyboardMap().GetKeyInput(c, IsAltGr(modifierKeys) ? ModifierKeys.None : modifierKeys);
        }

        public static bool TryConvertToKeyInput(Key key, out KeyInput keyInput)
        {
            return GetOrCreateKeyboardMap().TryGetKeyInput(key, out keyInput);
        }

        public static bool TryConvertToKeyInput(Key key, ModifierKeys modifierKeys, out KeyInput keyInput)
        {
            return GetOrCreateKeyboardMap().TryGetKeyInput(key, modifierKeys, out keyInput);
        }

        public static bool TryConvertToKey(VimKey vimKey, out Key key)
        {
            return GetOrCreateKeyboardMap().TryGetKey(vimKey, out key);
        }
    }
}
