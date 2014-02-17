using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using System.Windows;
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

        public Point? GetPosition(ITextView textView)
        {
            var wpfTextView = textView as IWpfTextView;
            if (wpfTextView != null)
            {
                return _mouseDevice.GetPosition(wpfTextView.VisualElement);
            }

            return null;
        }
    }
}
