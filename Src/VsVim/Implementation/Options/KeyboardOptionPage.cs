using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms.Integration;

namespace VsVim.Implementation.Options
{
    internal sealed class KeyboardOptionPage : DialogPage
    {
        private ElementHost _elementHost;
        private KeyboardSettingsControl _keyboardSettingsControl;

        protected override System.Windows.Forms.IWin32Window Window
        {
            get
            {
                if (_elementHost == null)
                {
                    _keyboardSettingsControl = new KeyboardSettingsControl();
                    _elementHost = new ElementHost();
                    _elementHost.Child = _keyboardSettingsControl;
                }

                return _elementHost;
            }
        }
    }
}
