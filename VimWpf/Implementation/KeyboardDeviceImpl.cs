using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation
{
    [Export(typeof(IKeyboardDevice))]
    internal sealed class KeyboardDeviceImpl : IKeyboardDevice
    {
        private KeyboardDevice _keyboardDevice = InputManager.Current.PrimaryKeyboardDevice;

        public bool IsKeyDown(KeyInput value)
        {
            var tuple = KeyUtil.ConvertToKeyAndModifiers(value);
            return IsKeyDown(tuple.Item1, tuple.Item2);
        }

        internal bool IsKeyDown(Key key, ModifierKeys modifiers)
        {
            try
            {
                return _keyboardDevice.IsKeyDown(key) && _keyboardDevice.Modifiers == modifiers;
            }
            catch (InvalidEnumArgumentException)
            {
                // IsKeyDown will throw this exception if ther Key value is None or other non-defined keys
                return false;
            }
        }
    }
}
