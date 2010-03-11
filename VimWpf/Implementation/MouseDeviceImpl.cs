using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation
{
    internal sealed class MouseDeviceImpl : IMouseDevice
    {
        private readonly MouseDevice _mouseDevice = InputManager.Current.PrimaryMouseDevice;

        public MouseButtonState LeftButtonState
        {
            get { return _mouseDevice.LeftButton; }
        }
    }
}
