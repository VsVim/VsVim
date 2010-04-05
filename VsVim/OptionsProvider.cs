using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf;
using Vim;
using System.ComponentModel.Composition;

namespace VsVim
{
    [Export(typeof(IOptionsProviderFactory))]
    internal sealed class OptionsProviderFatory : IOptionsProviderFactory
    {
        private sealed class Provider : IOptionsProvider
        {
            private readonly KeyBindingService _keyBindingService;
            internal Provider(KeyBindingService keyBindingService)
            {
                _keyBindingService = keyBindingService;
            }
            public void ShowDialog(IVimBuffer buffer)
            {
                var snapshot = _keyBindingService.CalculateCommandKeyBindingSnapshot(buffer);
                UI.ConflictingKeyBindingDialog.DoShow(snapshot);
            }
        }

        private readonly KeyBindingService _keyBindingService;

        [ImportingConstructor]
        internal OptionsProviderFatory(KeyBindingService service)
        {
            _keyBindingService = service;
        }

        public IOptionsProvider CreateOptionsProvider()
        {
            return new Provider(_keyBindingService);
        }
    }
}
