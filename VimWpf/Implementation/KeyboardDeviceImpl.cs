using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation
{
    [Export(typeof(IKeyboardDevice))]
    internal sealed class KeyboardDeviceImpl : IKeyboardDevice
    {
        private KeyboardDevice _keyboardDevice = InputManager.Current.PrimaryKeyboardDevice;

        public bool IsKeyDown(VimKey vimKey)
        {
            Key key;
            return KeyUtil.TryConvertToKey(vimKey, out key)
                && IsKeyDown(key);
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
    }
}
