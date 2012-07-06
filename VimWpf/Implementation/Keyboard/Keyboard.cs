using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    /// <summary>
    /// Holds the key states for a keyboard.  This is a scratch buffer and doesn't represent the live state
    /// of the keyboard. 
    /// </summary>
    internal sealed class Keyboard
    {
        internal const byte KeySetValue = 0x80;
        internal const byte KeyToggledValue = 0x01;
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
