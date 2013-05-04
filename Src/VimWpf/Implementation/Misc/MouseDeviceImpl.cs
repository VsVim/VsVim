using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IMouseDevice))]
    internal sealed class MouseDeviceImpl : IMouseDevice
    {
        private readonly MouseDevice _mouseDevice = InputManager.Current.PrimaryMouseDevice;

        public bool IsLeftButtonPressed
        {
            get { return _mouseDevice.LeftButton == MouseButtonState.Pressed; }
        }
    }
}
