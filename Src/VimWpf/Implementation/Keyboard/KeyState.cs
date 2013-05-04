using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    internal struct KeyState
    {
        internal readonly Key Key;
        internal readonly VirtualKeyModifiers Modifiers;

        internal bool HasExtendedModifiers
        {
            get { return 0 != (Modifiers & VirtualKeyModifiers.Extended); }
        }

        internal ModifierKeys ModifierKeys
        {
            get
            {
                var val = (int)Modifiers & 0xf;
                return (ModifierKeys)val;
            }
        }

        internal KeyState(Key key) : this(key, ModifierKeys.None)
        {

        }

        internal KeyState(Key key, VirtualKeyModifiers modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        internal KeyState(Key key, ModifierKeys modifierKeys)
        {
            Key = key;
            Modifiers = GetVirtualKeyModifiers(modifierKeys);
        }

        internal static VirtualKeyModifiers GetVirtualKeyModifiers(ModifierKeys modifierKeys)
        {
            return (VirtualKeyModifiers)modifierKeys;
        }

        public override string ToString()
        {
            if (Modifiers == VirtualKeyModifiers.None)
            {
                return Key.ToString();
            }

            return String.Format("{0}+{1}", Key, Modifiers);
        }
    }

}
