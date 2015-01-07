using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IKeyboardDevice))]
    internal sealed class KeyboardDeviceImpl : IKeyboardDevice
    {
        private readonly IKeyUtil _keyUtil;
        private readonly KeyboardDevice _keyboardDevice = InputManager.Current.PrimaryKeyboardDevice;

        internal bool IsArrowKeyDown
        {
            get
            {
                return
                    IsKeyDown(Key.Left) ||
                    IsKeyDown(Key.Up) ||
                    IsKeyDown(Key.Right) ||
                    IsKeyDown(Key.Down);
            }
        }

        [ImportingConstructor]
        internal KeyboardDeviceImpl(IKeyUtil keyUtil)
        {
            _keyUtil = keyUtil;
        }

        internal bool IsKeyDown(Key key)
        {
            try
            {
                return _keyboardDevice.IsKeyDown(key);
            }
            catch (InvalidEnumArgumentException)
            {
                // IsKeyDown will throw this exception if ther Key value is None or other non-defined keys
                return false;
            }
        }

        #region IKeyboardDevice

        bool IKeyboardDevice.IsArrowKeyDown
        {
            get { return IsArrowKeyDown; }
        }

        VimKeyModifiers IKeyboardDevice.KeyModifiers
        {
            get { return _keyUtil.GetKeyModifiers(_keyboardDevice.Modifiers); }
        }

        #endregion
    }
}
