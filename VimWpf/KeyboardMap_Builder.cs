using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    partial class KeyboardMap
    {
        /// <summary>
        /// This class is used to build up a KeyboardMap instance from a given keyboard layout.  My understanding
        /// of how keyboard layouts work and the proper way to use them from managed code comes almost
        /// exclusively from the blog of Michael Kaplan.  In particular the "Getting all you can out of a keyboard
        /// layout" series
        ///
        /// http://blogs.msdn.com/b/michkap/archive/2006/04/06/569632.aspx
        ///
        /// Any changes made to this logic should first consult this series.  It's invaluable
        /// </summary>
        private sealed class Builder
        {
            private const int KeyBoardArrayLength = 256;
            private const int UnicodeBufferLength = 10;

            private readonly IntPtr _keyboardId;
            private readonly StringBuilder _clearBuilder = new StringBuilder(UnicodeBufferLength);
            private readonly StringBuilder _normalBuilder = new StringBuilder(UnicodeBufferLength);
            private readonly byte[] _keyboardStateArray = new byte[KeyBoardArrayLength];
            private Dictionary<KeyState, VimKeyData> _keyStateToVimKeyDataMap;
            private Dictionary<KeyInput, WpfKeyData> _keyInputToWpfKeyDataMap;
            private Dictionary<char, ModifierKeys> _charToKeyModifiersMap;

            internal Builder(IntPtr keyboardId)
            {
                _keyboardId = keyboardId;
            }

            internal void Create(
                out Dictionary<KeyState, VimKeyData> keyStateToVimKeyDataMap,
                out Dictionary<KeyInput, WpfKeyData> keyInputToWpfKeyDataMap,
                out Dictionary<char, ModifierKeys> charToKeyModifiersMap)
            {
                _keyStateToVimKeyDataMap = new Dictionary<KeyState, VimKeyData>();
                _keyInputToWpfKeyDataMap = new Dictionary<KeyInput, WpfKeyData>();
                _charToKeyModifiersMap = new Dictionary<char, ModifierKeys>();

                BuildKeyInputData();
                BuildDeadKeyData();

                keyStateToVimKeyDataMap = _keyStateToVimKeyDataMap;
                keyInputToWpfKeyDataMap = _keyInputToWpfKeyDataMap;
                charToKeyModifiersMap = _charToKeyModifiersMap;
            }

            /// <summary>
            /// Build up the information about the known set of vim key input
            /// </summary>
            private void BuildKeyInputData()
            {
                foreach (var current in KeyInputUtil.VimKeyInputList)
                {
                    uint virtualKeyCode;
                    ModifierKeys modKeys;
                    if (!TryGetVirtualKeyAndModifiers(current, out virtualKeyCode, out modKeys))
                    {
                        continue;
                    }

                    // If this is backed by a real character then store the modifiers which are needed
                    // to produce this char.  Later we can compare the current modifiers to this value
                    // and find the extra modifiers to apply to the KeyInput given to Vim
                    if (current.KeyModifiers == KeyModifiers.None && IsMappedByCharacter(current.Key))
                    {
                        _charToKeyModifiersMap[current.Char] = modKeys;
                    }

                    // Only processing items which can map to actual keys
                    var key = KeyInterop.KeyFromVirtualKey((int)virtualKeyCode);
                    if (Key.None == key)
                    {
                        continue;
                    }

                    var keyState = new KeyState(key, modKeys);
                    _keyStateToVimKeyDataMap[keyState] = new VimKeyData(current);
                    _keyInputToWpfKeyDataMap[current] = new WpfKeyData(key, modKeys);
                }
            }

            /// <summary>
            /// Build up the set of dead keys in this keyboard layout
            /// </summary>
            void BuildDeadKeyData()
            {
                foreach (Key key in Enum.GetValues(typeof(Key)))
                {
                    var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                    var scanCode = NativeMethods.MapVirtualKeyEx((uint)virtualKey, 0, _keyboardId);
                    if (scanCode == 0)
                    {
                        continue;
                    }

                    var value = NativeMethods.ToUnicodeEx(
                        virtualKey,
                        scanCode,
                        _keyboardStateArray,
                        _normalBuilder,
                        _normalBuilder.Length,
                        0,
                        _keyboardId);
                    if (value < 0)
                    {
                        // It's a dead key.  Before logging make sure to clear out the key board state 
                        // from the dead key press
                        ClearKeyboardBuffer();

                        var keyState = new KeyState(key, ModifierKeys.None);
                        _keyStateToVimKeyDataMap[keyState] = VimKeyData.DeadKey;
                    }
                }
            }

            /// <summary>
            /// This method is used to clear the keyboard layout of any existing key states.  This 
            /// method is taken directly from Michael Kaplan's blog entry
            /// 
            /// http://blogs.msdn.com/b/michkap/archive/2007/10/27/5717859.aspx 
            /// </summary>
            private void ClearKeyboardBuffer(uint virtualKey, uint scanCode)
            {
                int value;
                do
                {
                    value = NativeMethods.ToUnicodeEx(virtualKey, scanCode, _keyboardStateArray, _clearBuilder, _clearBuilder.Capacity, 0, _keyboardId);
                } while (value < 0);
            }

            private void ClearKeyboardBuffer()
            {
                var scanCode = NativeMethods.MapVirtualKeyEx(NativeMethods.VK_DECIMAL, 0, _keyboardId);
                if (scanCode != 0)
                {
                    ClearKeyboardBuffer(NativeMethods.VK_DECIMAL, scanCode);
                }
            }

            /// <summary>
            /// Try and get the Virtual Key Code and Modifiers for the given KeyInput.  
            /// </summary>
            private bool TryGetVirtualKeyAndModifiers(KeyInput keyInput, out uint virtualKeyCode, out ModifierKeys modKeys)
            {
                if (TrySpecialVimKeyToVirtualKey(keyInput.Key, out virtualKeyCode))
                {
                    Debug.Assert(!IsMappedByCharacter(keyInput.Key));
                    modKeys = ModifierKeys.None;
                    return true;
                }
                else
                {
                    Debug.Assert(IsMappedByCharacter(keyInput.Key));
                    Debug.Assert(keyInput.KeyModifiers == KeyModifiers.None);
                    return TryMapCharToVirtualKeyAndModifiers(keyInput.Char, out virtualKeyCode, out modKeys);
                }
            }

            internal static bool IsSpecialVimKey(VimKey vimKey)
            {
                uint virtualKey;
                return !TrySpecialVimKeyToVirtualKey(vimKey, out virtualKey);
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
                    case VimKey.LineFeed: virtualKeyCode = 0; break;
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

            /// <summary>
            /// Map the given char to a virtual key code and the associated necessary modifier keys for
            /// the provided keyboard layout
            /// </summary>
            private bool TryMapCharToVirtualKeyAndModifiers(char c, out uint virtualKeyCode, out ModifierKeys modKeys)
            {
                var res = NativeMethods.VkKeyScanEx(c, _keyboardId);

                // The virtual key code is the low byte and the shift state is the high byte
                var virtualKey = res & 0xff;
                var state = ((res >> 8) & 0xff);
                if (virtualKey == -1 || state == -1)
                {
                    virtualKeyCode = 0;
                    modKeys = ModifierKeys.None;
                    return false;
                }

                var shiftMod = (state & 0x1) != 0 ? ModifierKeys.Shift : ModifierKeys.None;
                var controlMod = (state & 0x2) != 0 ? ModifierKeys.Control : ModifierKeys.None;
                var altMod = (state & 0x4) != 0 ? ModifierKeys.Alt : ModifierKeys.None;
                virtualKeyCode = (uint)virtualKey;
                modKeys = shiftMod | controlMod | altMod;
                return true;
            }
        }
    }
}
