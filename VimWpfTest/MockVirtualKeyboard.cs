using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.UnitTest
{
    internal class MockVirtualKeyboard : KeyboardMap.IVirtualKeyboard
    {
        internal KeyboardMap.Keyboard Keyboard { get; set; }
        internal bool UsesExtendedModifiers { get; set; }
        internal bool IsCapsLockToggled { get; set; }

        #region IVirtualKeyboard

        KeyboardMap.Keyboard KeyboardMap.IVirtualKeyboard.Keyboard
        {
            get { throw new NotImplementedException(); }
        }

        bool KeyboardMap.IVirtualKeyboard.UsesExtendedModifiers
        {
            get { throw new NotImplementedException(); }
        }

        bool KeyboardMap.IVirtualKeyboard.IsCapsLockToggled
        {
            get { throw new NotImplementedException(); }
        }

        KeyboardMap.VirtualKeyModifiers KeyboardMap.IVirtualKeyboard.VirtualKeyModifiersExtended
        {
            get { throw new NotImplementedException(); }
        }

        bool KeyboardMap.IVirtualKeyboard.TryMapChar(char c, out uint virtualKey, out KeyboardMap.VirtualKeyModifiers virtualKeyModifiers)
        {
            throw new NotImplementedException();
        }

        bool KeyboardMap.IVirtualKeyboard.TryGetText(uint virtualKey, KeyboardMap.VirtualKeyModifiers virtualKeyModifiers, out string text, out bool isDeadKey)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
