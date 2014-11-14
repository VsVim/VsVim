using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Vim;
using Vim.UI.Wpf;

namespace Vim.VisualStudio.Implementation.OptionPages
{
    public sealed class KeyboardOptionPage : DialogPage
    {
        private ElementHost _elementHost;
        private KeyboardSettingsControl _keyboardSettingsControl;

        protected override IWin32Window Window
        {
            get
            {
                if (_elementHost == null)
                {
                    _keyboardSettingsControl = CreateKeyboardSettingsControl();
                    _elementHost = new ElementHost();
                    _elementHost.Child = _keyboardSettingsControl;
                }

                return _elementHost;
            }
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (_keyboardSettingsControl != null)
            {
                _keyboardSettingsControl.Apply();
            }

            base.OnApply(e);
        }

        private KeyboardSettingsControl CreateKeyboardSettingsControl()
        {
            if (Site == null)
            {
                return null;
            }

            var componentModel = (IComponentModel)(Site.GetService(typeof(SComponentModel))); ;
            var exportProvider = componentModel.DefaultExportProvider;
            var vim = exportProvider.GetExportedValue<IVim>();
            var keyBindingService = exportProvider.GetExportedValue<IKeyBindingService>();

            return new KeyboardSettingsControl(vim, keyBindingService, exportProvider.GetExportedValue<IVimApplicationSettings>(), exportProvider.GetExportedValue<IVimProtectedOperations>());
        }
    }
}
