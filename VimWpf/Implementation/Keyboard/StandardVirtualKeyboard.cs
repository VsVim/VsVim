using System;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    /// <summary>
    /// This is the standard implementation of IVirtualKeyboard.  It's the implementation
    /// that talks directly to the operating system.  All other implementations are used
    /// for testing
    /// </summary>
    internal sealed class StandardVirtualKeyboard : IVirtualKeyboard
    {
        private const int UnicodeBufferLength = 10;

        private readonly StringBuilder _clearBuilder = new StringBuilder(UnicodeBufferLength);
        private readonly StringBuilder _normalBuilder = new StringBuilder(UnicodeBufferLength);
        private readonly KeyboardState _keyboardState = new KeyboardState();
        private readonly IntPtr _keyboardId;

        internal KeyboardState KeyboardState
        {
            get { return _keyboardState; }
        }

        internal bool IsCapsLockToggled
        {
            get { return IsKeyToggled(NativeMethods.VK_CAPITAL); }
        }

        internal VirtualKeyModifiers VirtualKeyModifiersExtended
        {
            get
            {
                var virtualKeyModifiers = VirtualKeyModifiers.None;
                if (_keyboardState.Oem1ModifierVirtualKey.HasValue && IsKeySet(_keyboardState.Oem1ModifierVirtualKey.Value))
                {
                    virtualKeyModifiers |= VirtualKeyModifiers.Oem1;
                }

                if (_keyboardState.Oem2ModifierVirtualKey.HasValue && IsKeySet(_keyboardState.Oem2ModifierVirtualKey.Value))
                {
                    virtualKeyModifiers |= VirtualKeyModifiers.Oem2;
                }

                return virtualKeyModifiers;
            }
        }

        internal bool UsesExtendedModifiers
        {
            get { return _keyboardState.Oem1ModifierVirtualKey.HasValue || _keyboardState.Oem2ModifierVirtualKey.HasValue; }
        }

        internal StandardVirtualKeyboard(IntPtr keyboardId)
        {
            _keyboardId = keyboardId;
        }

        /// <summary>
        /// Map the given char to a virtual key code and the associated necessary modifier keys for
        /// the provided keyboard layout
        /// </summary>
        private bool TryMapChar(char c, out uint virtualKeyCode, out VirtualKeyModifiers virtualKeyModifiers)
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

        /// <summary>
        /// Simple mechanism for getting the text for the given virtual key code and the specified
        /// modifiers
        ///
        /// This method is intended to leave the values in the keyboard state array set that are
        /// set before calling.  It will clear out the keyboard state after calling though
        /// </summary>
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
                _keyboardState.SetShiftState(virtualKeyModifiers);
                _normalBuilder.Length = 0;
                var value = NativeMethods.ToUnicodeEx(
                    virtualKey,
                    scanCode,
                    _keyboardState.State,
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
                _keyboardState.Clear();
            }
        }

        private bool IsKeyToggled(uint virtualKey)
        {
            var state = NativeMethods.GetKeyState(virtualKey);
            return 0 != (state & KeyboardState.KeyToggledValue);
        }

        private bool IsKeySet(uint virtualKey)
        {
            var state = NativeMethods.GetKeyState(virtualKey);
            return 0 != (state & KeyboardState.KeySetValue);
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
                value = NativeMethods.ToUnicodeEx(virtualKey, scanCode, _keyboardState.State, _clearBuilder, _clearBuilder.Capacity, 0, _keyboardId);
                _clearBuilder.Length = 0;
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

        #region IVirtualKeyboard

        KeyboardState IVirtualKeyboard.KeyboardState
        {
            get { return KeyboardState; }
        }

        bool IVirtualKeyboard.IsCapsLockToggled
        {
            get { return IsCapsLockToggled; }
        }

        VirtualKeyModifiers IVirtualKeyboard.VirtualKeyModifiersExtended
        {
            get { return VirtualKeyModifiersExtended; }
        }

        bool IVirtualKeyboard.UsesExtendedModifiers
        {
            get { return UsesExtendedModifiers; }
        }

        bool IVirtualKeyboard.TryMapChar(char c, out uint virtualKey, out VirtualKeyModifiers virtualKeyModifiers)
        {
            return TryMapChar(c, out virtualKey, out virtualKeyModifiers);
        }

        bool IVirtualKeyboard.TryGetText(uint virtualKey, VirtualKeyModifiers virtualKeyModifiers, out string text, out bool isDeadKey)
        {
            return TryGetText(virtualKey, virtualKeyModifiers, out text, out isDeadKey);
        }

        #endregion
    }
}
