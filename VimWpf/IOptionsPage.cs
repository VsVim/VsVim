using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Vim.UI.Wpf
{
    public interface IOptionsPage
    {
        string Name { get; }
        FrameworkElement VisualElement { get; } 
    }
}
