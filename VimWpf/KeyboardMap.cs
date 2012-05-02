using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Class responsible for handling the low level details for mapping WPF's 
    /// view of the Keyboard to Vim's understanding
    /// </summary>
    internal sealed partial class KeyboardMap
    {
        private struct KeyState
        {
            internal readonly Key Key;
            internal readonly ModifierKeys ModifierKeys;
            internal KeyState(Key key, ModifierKeys modKeys)
            {
                Key = key;
                ModifierKeys = modKeys;
            }
        }

        private sealed class VimKeyData
        {
            internal static readonly VimKeyData DeadKey = new VimKeyData();

            internal readonly KeyInput KeyInputOptional;
            internal readonly bool IsDeadKey;
            internal VimKeyData(KeyInput keyInput)
            {
                Contract.Assert(keyInput != null);
                KeyInputOptional = keyInput;
                IsDeadKey = false;
            }

            private VimKeyData()
            {
                IsDeadKey = true;
            }
        }

        private sealed class WpfKeyData
        {
            internal readonly Key Key;
            internal readonly ModifierKeys ModifierKeys;

            internal WpfKeyData(Key key, ModifierKeys modifierKeys)
            {
                Key = key;
                ModifierKeys = modifierKeys;
            }
        }

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
        private readonly Dictionary<KeyInput, WpfKeyData> _keyInputToWpfKeyDataMap;

        /// <summary>
        /// Cache of the char and the modifiers needed to build the char 
        /// </summary>
        private readonly Dictionary<char, ModifierKeys> _charToModifierMap;

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

        internal KeyboardMap(IntPtr keyboardId)
        {
            _keyboardId = keyboardId;

            var builder = new Builder(keyboardId);
            builder.Create(out _keyStateToVimKeyDataMap, out _keyInputToWpfKeyDataMap, out _charToModifierMap);
        }

        /// <summary>
        /// Try and get the KeyInput for the specified char and ModifierKeys.  This will 
        /// check and see if this is a well known char.  Many well known chars are expected
        /// to come with certain modifiers (for instance on US keyboard # is expected to
        /// have Shift applied).  These will be removed from the ModifierKeys passed in and
        /// whatever is left will be applied to the resulting KeyInput.  
        /// </summary>
        internal KeyInput GetKeyInput(char c, ModifierKeys modifierKeys)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            ModifierKeys expected;
            if (_charToModifierMap.TryGetValue(c, out expected))
            {
                modifierKeys &= ~expected;
            }

            if (modifierKeys != ModifierKeys.None)
            {
                var keyModifiers = ConvertToKeyModifiers(modifierKeys);
                keyInput = KeyInputUtil.ApplyModifiers(keyInput, keyModifiers);
            }

            return keyInput;
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

        private bool TryGetKeyInput(Key key, ModifierKeys modifierKeys, out VimKeyData vimKeyData)
        {
            // First just check and see if there is a direct mapping
            var keyState = new KeyState(key, modifierKeys);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData))
            {
                return true;
            }

            // Next consider only the shift key part of the requested modifier.  We can 
            // re-apply the original modifiers later 
            keyState = new KeyState(key, modifierKeys & ModifierKeys.Shift);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData) && 
                vimKeyData.KeyInputOptional != null)
            {
                // Reapply the modifiers
                var keyInput = KeyInputUtil.ApplyModifiers(vimKeyData.KeyInputOptional, ConvertToKeyModifiers(modifierKeys));
                vimKeyData = new VimKeyData(keyInput);
                return true;
            }

            // Last consider it without any modifiers and reapply
            keyState = new KeyState(key, ModifierKeys.None);
            if (_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData) &&
                vimKeyData.KeyInputOptional != null)
            {
                // Reapply the modifiers
                var keyInput = KeyInputUtil.ApplyModifiers(vimKeyData.KeyInputOptional, ConvertToKeyModifiers(modifierKeys));
                vimKeyData = new VimKeyData(keyInput);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try and get the WPF key for the given VimKey value
        /// </summary>
        internal bool TryGetKey(VimKey vimKey, out Key key, out ModifierKeys modifierKeys)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);

            WpfKeyData wpfKeyData;
            if (_keyInputToWpfKeyDataMap.TryGetValue(keyInput, out wpfKeyData))
            {
                key = wpfKeyData.Key;
                modifierKeys = wpfKeyData.ModifierKeys;
                return true;
            }


            key = Key.None;
            modifierKeys = ModifierKeys.None;
            return false;
        }

        /// <summary>
        /// Under the hood we map KeyInput values into actual input by one of two mechanisms
        ///
        ///  1. Straight mapping of a VimKey to a Virtual Key Code.
        ///  2. Mapping of the character to a virtual key code and set of modifier keys
        ///
        /// This method will return true if the VimKey is mapped using method 2
        ///
        /// Generally speaking a KeyInput is mapped by character if it has an associated 
        /// char value.  This is not true for certain special cases like Enter, Tab and 
        /// the Keypad values.
        ///
        /// TODO: Delete this method.  This is an odd heuristic to use
        /// </summary>
        internal static bool IsMappedByCharacter(VimKey vimKey)
        {
            return Builder.IsSpecialVimKey(vimKey);
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
