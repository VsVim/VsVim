using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VsVim.UI
{
    public sealed class KeyBindingHandledByOption : DependencyObject
    {
        public string HandlerName { get; set; }
        public string HandlerDetails { get; set; }
    }
}
