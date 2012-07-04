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
        private const byte KeySetValue = 0x80;
        private const byte KeyToggledValue = 0x01;

        /// <summary>
        /// Bit flags for the modifier keys that can be returned from VkKeyScanEx.  These 
        /// values *must* match up with the bits that can be returned from that function
        /// </summary>
        [Flags]
        internal enum VirtualKeyModifiers
        {
            None = 0,
            Alt = 0x1,
            Control = 0x2,
            Shift = 0x4,
            Windows = 0x8,
            Oem1 = 0x10,
            Oem2 = 0x20,

            Regular = Alt | Control | Shift,
            Extended = Windows | Oem1 | Oem2,
        }

        internal interface IVirtualKeyboard
        {
            /// <summary>
            /// The Keyboard instance the IVirtualKeyboard is using when it queries the real keyboard buffer
            /// </summary>
            Keyboard Keyboard { get; }

            /// <summary>
            /// Does this given virtual keyboard layout make use of extended modifiers
            /// </summary>
            bool UsesExtendedModifiers { get; }

            /// <summary>
            /// Is the caps lock key currently toggled on the keyboard buffer
            /// </summary>
            bool IsCapsLockToggled { get; }

            /// <summary>
            /// Get the extended virtual key modifiers that are currently set on the keyboard buffer
            /// </summary>
            VirtualKeyModifiers VirtualKeyModifiersExtended { get; }

            /// <summary>
            /// Try and map the given char to it's virtual key and modifiers.  There is no guarantee that if 
            /// succesful that this return is the only combination which produces that char.  Many more 
            /// could exist
            /// </summary>
            bool TryMapChar(char c, out uint virtualKey, out VirtualKeyModifiers virtualKeyModifiers);

            /// <summary>
            /// Simple mechanism for getting the text for the given virtual key code and the specified
            /// modifiers
            ///
            /// This method is intended to leave the values in the keyboard state array set that are
            /// set before calling.  It will clear out the keyboard state after calling though
            /// </summary>
            bool TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text, out bool isDeadKey);

            bool TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text);
        }

        /// <summary>
        /// Holds the key states for a keyboard.  This is a scratch buffer and doesn't represent the live state
        /// of the keyboard. 
        /// </summary>
        internal sealed class Keyboard
        {
            private const int KeyBoardArrayLength = 256;

            private readonly byte[] _keyboardStateArray = new byte[KeyBoardArrayLength];
            private uint? _oem1ModifierVirtualKey;
            private uint? _oem2ModifierVirtualKey;

            /// <summary>
            /// The raw value for the state of the keyboard
            /// </summary>
            internal byte[] KeyboardState
            {
                get { return _keyboardStateArray; }
            }

            /// <summary>
            /// The virtual key for the 0x10 shift state modifier if it exists for the given layout
            /// </summary>
            internal uint? Oem1ModifierVirtualKey
            {
                get { return _oem1ModifierVirtualKey; }
                set { _oem1ModifierVirtualKey = value; }
            }

            /// <summary>
            /// The virtual key for the 0x20 shift state modifier if it exists for the given layout
            /// </summary>
            internal uint? Oem2ModifierVirtualKey
            {
                get { return _oem2ModifierVirtualKey; }
                set { _oem2ModifierVirtualKey = value; }
            }

            internal void SetKey(uint virtualKey)
            {
                Contract.Assert(virtualKey < KeyBoardArrayLength);
                _keyboardStateArray[virtualKey] = KeySetValue;
            }

            internal void SetShiftState(VirtualKeyModifiers virtualKeyModifiers, bool capslock = false)
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
                    _keyboardStateArray[NativeMethods.VK_CAPITAL] = KeyToggledValue;
                }
            }

            internal void Clear()
            {
                Array.Clear(_keyboardStateArray, 0, KeyBoardArrayLength);
            }
        }
    }
}
