using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using Vim.Extensions;

namespace Vim.UI.Wpf
{
    partial class KeyboardMap
    {
        /*
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
        private sealed class VirtualKeyboard
        {
            private const int UnicodeBufferLength = 10;

            private readonly IntPtr _keyboardId;
            private readonly StringBuilder _clearBuilder = new StringBuilder(UnicodeBufferLength);
            private readonly StringBuilder _normalBuilder = new StringBuilder(UnicodeBufferLength);
            private readonly byte[] _keyboardStateArray = new byte[KeyBoardArrayLength];
            private Dictionary<KeyState, VimKeyData> _keyStateToVimKeyDataMap;
            private Dictionary<KeyInput, KeyState> _keyInputToWpfKeyDataMap;
            private bool _hasExtendedVirtualKeyModifier;
            private Lazy<List<uint>> _possibleModifierVirtualKey;
            private uint? _oem1ModifierVirtualKey;
            private bool _lookedForOem1ModifierVirtualKey;
            private uint? _oem2ModifierVirtualKey;
            private bool _lookedForOem2ModifierVirtualKey;

            internal Builder(IntPtr keyboardId)
            {
                _keyboardId = keyboardId;
                _possibleModifierVirtualKey = new Lazy<List<uint>>(GetPossibleVirtualKeyModifiers);
            }

            internal void Create(
                out Dictionary<KeyState, VimKeyData> keyStateToVimKeyDataMap,
                out Dictionary<KeyInput, KeyState> keyInputToWpfKeyDataMap,
                out bool hasExtendedVirtualKeyModifier,
                out uint? oem1ModifierVirtualKey,
                out uint? oem2ModifierVirtualKey)
            {
                _keyStateToVimKeyDataMap = new Dictionary<KeyState, VimKeyData>();
                _keyInputToWpfKeyDataMap = new Dictionary<KeyInput, KeyState>();

                BuildKeyInputData();
                BuildDeadKeyData();

                keyStateToVimKeyDataMap = _keyStateToVimKeyDataMap;
                keyInputToWpfKeyDataMap = _keyInputToWpfKeyDataMap;
                hasExtendedVirtualKeyModifier = _hasExtendedVirtualKeyModifier;
                oem1ModifierVirtualKey = _oem1ModifierVirtualKey;
                oem2ModifierVirtualKey = _oem2ModifierVirtualKey;
            }

            /// <summary>
            /// Build up the information about the known set of vim key input
            /// </summary>
            private void BuildKeyInputData()
            {
                foreach (var current in KeyInputUtil.VimKeyInputList)
                {
                    uint virtualKey;
                    VirtualKeyModifiers virtualKeyModifiers;
                    if (!TryGetVirtualKeyAndModifiers(current, out virtualKey, out virtualKeyModifiers))
                    {
                        continue;
                    }

                    // Only processing items which can map to actual keys
                    var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
                    if (Key.None == key)
                    {
                        continue;
                    }

                    // If this mapping has any OEM specific modifiers then we need to sort them out
                    // here 
                    if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Extended) && current.RawChar.IsSome())
                    {
                        _hasExtendedVirtualKeyModifier = true;
                        LookForOemModifiers(current.Char, virtualKey, virtualKeyModifiers);
                    }

                    string text;
                    if (!TryGetText(virtualKey, VirtualKeyModifiers.None, out text))
                    {
                        text = "";
                    }

                    var keyState = new KeyState(key, virtualKeyModifiers);
                    _keyStateToVimKeyDataMap[keyState] = new VimKeyData(current, text);
                    _keyInputToWpfKeyDataMap[current] = keyState;
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
                    bool isDeadKey;
                    string unused;
                    if (!TryGetText(virtualKey, VirtualKeyModifiers.None, out unused, out isDeadKey) && isDeadKey)
                    {
                        var keyState = new KeyState(key, VirtualKeyModifiers.None);
                        _keyStateToVimKeyDataMap[keyState] = VimKeyData.DeadKey;
                    }
                }
            }

            /// <summary>
            /// Simple mechanism for getting the text for the given virtual key code and the specified
            /// modifiers
            ///
            /// This method is intended to leave the values in the keyboard state array set that are
            /// set before calling.  It will clear out the keyboard state after calling though
            /// </summary>
            private bool TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text)
            {
                bool unused;
                return TryGetText(virtualKey, virtualKeyModifiers, out text, out unused);
            }

            private bool TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text, out bool isDeadKey)
            {
                var scanCode = NativeMethods.MapVirtualKeyEx(virtualKey, 0, _keyboardId);
                if (scanCode == 0)
                {
                    text = String.Empty;
                    isDeadKey = false;
                    return false;
                }

                try
                {
                    SetKeyState(virtualKeyModifiers);
                    _normalBuilder.Length = 0;
                    var value = NativeMethods.ToUnicodeEx(
                        virtualKey,
                        scanCode,
                        _keyboardStateArray,
                        _normalBuilder,
                        _normalBuilder.Capacity,
                        0,
                        _keyboardId);
                    if (value < 0)
                    {
                        // It's a dead key. Make sure to clear out the cached state
                        ClearKeyboardBuffer();
                        isDeadKey = true;
                        text = String.Empty;
                        return false;
                    }
                    else if (value > 0)
                    {
                        isDeadKey = false;
                        text = _normalBuilder.ToString();
                        return true;
                    }
                    else
                    {
                        isDeadKey = false;
                        text = String.Empty;
                        return false;
                    }
                }
                finally
                {
                    ClearKeyState();
                }
            }

            private void SetKeyState(VirtualKeyModifiers virtualKeyModifiers, bool capslock = false)
            {
                if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Control))
                {
                    _keyboardStateArray[NativeMethods.VK_CONTROL] = KeySetValue;
                }

                if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Alt))
                {
                    _keyboardStateArray[NativeMethods.VK_MENU] = KeySetValue;
                }

                if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Shift))
                {
                    _keyboardStateArray[NativeMethods.VK_SHIFT] = KeySetValue;
                }

                if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Oem1) && _oem1ModifierVirtualKey.HasValue)
                {
                    _keyboardStateArray[_oem1ModifierVirtualKey.Value] = KeySetValue;
                }

                if (0 != (virtualKeyModifiers & VirtualKeyModifiers.Oem2) && _oem2ModifierVirtualKey.HasValue)
                {
                    _keyboardStateArray[_oem2ModifierVirtualKey.Value] = KeySetValue;
                }

                if (capslock)
                {
                    _keyboardStateArray[NativeMethods.VK_CAPITAL] = 0x1;
                }
            }

            private void ClearKeyState()
            {
                Array.Clear(_keyboardStateArray, 0, _keyboardStateArray.Length);
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
            private bool TryGetVirtualKeyAndModifiers(KeyInput keyInput, out uint virtualKeyCode, out VirtualKeyModifiers virtualKeyModifiers)
            {
                if (TrySpecialVimKeyToVirtualKey(keyInput.Key, out virtualKeyCode))
                {
                    virtualKeyModifiers = VirtualKeyModifiers.None;
                    return true;
                }
                else
                {
                    Debug.Assert(keyInput.KeyModifiers == KeyModifiers.None);
                    return TryMapCharToVirtualKeyAndModifiers(keyInput.Char, out virtualKeyCode, out virtualKeyModifiers);
                }
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
            private bool TryMapCharToVirtualKeyAndModifiers(char c, out uint virtualKeyCode, out VirtualKeyModifiers virtualKeyModifiers)
            {
                var res = NativeMethods.VkKeyScanEx(c, _keyboardId);

                // The virtual key code is the low byte and the shift state is the high byte
                var virtualKey = res & 0xff;
                var state = ((res >> 8) & 0xff);
                if (virtualKey == -1 || state == -1)
                {
                    virtualKeyCode = 0;
                    virtualKeyModifiers = VirtualKeyModifiers.None;
                    return false;
                }

                virtualKeyCode = (uint)virtualKey;
                virtualKeyModifiers = (VirtualKeyModifiers)state;
                return true;
            }

            private void LookForOemModifiers(char c, uint virtualKey, VirtualKeyModifiers virtualKeyModifiers)
            {
                // These are flags but we can only search one at a time here.  If both are present it's not
                // possible to distinguish one from the others
                var regular = virtualKeyModifiers & VirtualKeyModifiers.Regular;
                var extended = virtualKeyModifiers & VirtualKeyModifiers.Extended;
                switch (extended)
                {
                    case VirtualKeyModifiers.Oem1:
                        if (!_lookedForOem1ModifierVirtualKey)
                        {
                            _lookedForOem1ModifierVirtualKey = true;
                            LookForOemModifiers(c, virtualKey, regular, out _oem1ModifierVirtualKey);
                        }
                        break;
                    case VirtualKeyModifiers.Oem2:
                        if (!_lookedForOem2ModifierVirtualKey)
                        {
                            _lookedForOem2ModifierVirtualKey = true;
                            LookForOemModifiers(c, virtualKey, regular, out _oem2ModifierVirtualKey);
                        }
                        break;
                }
            }

            private void LookForOemModifiers(char c, uint virtualKey, VirtualKeyModifiers regularKeyModifiers, out uint? oemModifierVirtualKey)
            {
                var target = c.ToString();
                ClearKeyState();
                foreach (var code in _possibleModifierVirtualKey.Value)
                {
                    // Set the keyboard state 
                    _keyboardStateArray[code] = KeySetValue;

                    // Now try to get the text value with the previous key down
                    string text;
                    if (TryGetText(virtualKey, regularKeyModifiers, out text) && text == target)
                    {
                        oemModifierVirtualKey = code;
                        return;
                    }
                }

                oemModifierVirtualKey = null;
            }

            /// <summary>
            /// In the case where we find keys with extended virtual key modifiers we need to look for
            /// the virtual keys which could actually trigger them.  This function will make a guess
            /// at what those keys could be
            /// </summary>
            private List<uint> GetPossibleVirtualKeyModifiers()
            {
                var list = new List<uint>();
                for (uint i = 0xba; i < 0xe5; i++)
                {
                    bool isDeadKey;
                    string unused;
                    if (!TryGetText(i, VirtualKeyModifiers.None, out unused, out isDeadKey) && !isDeadKey)
                    {
                        list.Add(i);
                    }
                }

                return list;
            }
        }
        */
    }
}
