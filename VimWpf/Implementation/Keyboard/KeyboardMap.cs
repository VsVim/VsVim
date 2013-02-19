using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    /// <summary>
    /// Class responsible for handling the low level details for mapping WPF's 
    /// view of the Keyboard to Vim's understanding
    /// </summary>
    internal sealed partial class KeyboardMap
    {
        /// <summary>
        /// The Id of the Keyboard 
        /// </summary>
        private readonly IntPtr _keyboardId;

        /// <summary>
        /// Cache of Key + Modifiers to Vim key information
        /// </summary>
        private readonly Dictionary<KeyState, VimKeyData> _keyStateToVimKeyDataMap;

        /// <summary>
        /// Cache of KeyInput to WPF key information
        /// </summary>
        private readonly Dictionary<KeyInput, FrugalList<KeyState>> _keyInputToWpfKeyDataMap;

        /// <summary>
        /// The IVirtualKeyboard for the current layout 
        /// </summary>
        private readonly IVirtualKeyboard _virtualKeyboard;

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

        internal KeyboardMap(IntPtr keyboardId) : this(keyboardId, new StandardVirtualKeyboard(keyboardId))
        {

        }

        internal KeyboardMap(IntPtr keyboardId, IVirtualKeyboard virtualKeyboard)
        {
            _keyboardId = keyboardId;
            _virtualKeyboard = virtualKeyboard;

            var builder = new KeyboardMapBuilder(_virtualKeyboard);
            builder.Create(out _keyStateToVimKeyDataMap, out _keyInputToWpfKeyDataMap);
        }

        /// <summary>
        /// Get the KeyInput for the specified character and Modifier keys.  This will properly
        /// unify the WPF modifiers with the expected Vim ones
        /// </summary>
        internal static KeyInput GetKeyInput(char c, ModifierKeys modifierKeys)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            var keyModifiers = ConvertToKeyModifiers(modifierKeys);
            return KeyInputUtil.ApplyModifiers(keyInput, keyModifiers);
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
            VimKeyData vimKeyData;
            if (TryGetKeyInput(key, modifierKeys, out vimKeyData) && vimKeyData.KeyInputOptional != null)
            {
                keyInput = vimKeyData.KeyInputOptional;
                return true;
            }

            keyInput = null;
            return false;
        }

        /// <summary>
        /// Is the specified key information a dead key
        /// </summary>
        internal bool IsDeadKey(Key key, ModifierKeys modifierKeys)
        {
            var keyState = new KeyState(key, modifierKeys);
            VimKeyData vimKeyData;
            if (!_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData))
            {
                return false;
            }

            return vimKeyData.IsDeadKey;
        }

        private bool TryGetKeyInput(Key key, ModifierKeys modifierKeys, out VimKeyData vimKeyData)
        {
            var virtualKeyModifiers = KeyState.GetVirtualKeyModifiers(modifierKeys);
            if (_virtualKeyboard.UsesExtendedModifiers)
            {
                virtualKeyModifiers |= _virtualKeyboard.VirtualKeyModifiersExtended;
            }

            if (_virtualKeyboard.IsCapsLockToggled)
            {
                virtualKeyModifiers |= VirtualKeyModifiers.CapsLock;
            }

            // First just check and see if there is a direct mapping
            var keyState = new KeyState(key, virtualKeyModifiers);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData))
            {
                return true;
            }

            // Next consider only the shift key part of the requested modifier.  We can 
            // re-apply the original modifiers later 
            keyState = new KeyState(key, virtualKeyModifiers & VirtualKeyModifiers.Shift);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData) &&
                vimKeyData.KeyInputOptional != null)
            {
                // Reapply the modifiers
                var keyInput = KeyInputUtil.ApplyModifiers(vimKeyData.KeyInputOptional, ConvertToKeyModifiers(modifierKeys));
                vimKeyData = new VimKeyData(keyInput, vimKeyData.TextOptional);
                return true;
            }

            // Last consider it without any modifiers and reapply
            keyState = new KeyState(key, VirtualKeyModifiers.None);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData) &&
                vimKeyData.KeyInputOptional != null)
            {
                // Reapply the modifiers
                var keyInput = KeyInputUtil.ApplyModifiers(vimKeyData.KeyInputOptional, ConvertToKeyModifiers(modifierKeys));
                vimKeyData = new VimKeyData(keyInput, vimKeyData.TextOptional);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try and get the WPF keys which are associated with the given VimKey value.  There
        /// can be multiple as several key combinations can map to a single WPF key
        /// </summary>
        internal bool TryGetKey(VimKey vimKey, out IEnumerable<KeyState> keys)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);

            FrugalList<KeyState> frugalList;
            if (_keyInputToWpfKeyDataMap.TryGetValue(keyInput, out frugalList))
            {
                keys = frugalList.GetValues();
                return true;
            }

            keys = null;
            return false;
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
    }
}
