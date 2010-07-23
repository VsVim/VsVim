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
            return _keyboardDevice.IsKeyDown(tuple.Item1) && _keyboardDevice.Modifiers == tuple.Item2;
        }
    }
}
