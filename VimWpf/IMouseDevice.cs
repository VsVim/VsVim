using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Define an interface for System.Windows.Input.MouseDevice which allows me
    /// to better test MouseProcessor
    /// </summary>
    public interface IMouseDevice
    {
        MouseButtonState LeftButtonState { get; }
    }
}
