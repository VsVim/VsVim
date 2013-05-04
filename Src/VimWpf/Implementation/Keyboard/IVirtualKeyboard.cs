using System;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
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
        CapsLock = 0x40,

        Regular = Alt | Control | Shift,
        Extended = Windows | Oem1 | Oem2,
    }

    internal interface IVirtualKeyboard
    {
        /// <summary>
        /// The Keyboard instance the IVirtualKeyboard is using when it queries the real keyboard buffer
        /// </summary>
        KeyboardState KeyboardState { get; }

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
    }
}
