using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf;
using Vim;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsVim.Implementation
{
    [Export(typeof(IOptionsProviderFactory))]
    internal sealed class OptionsProviderFatory : IOptionsProviderFactory
    {
        private sealed class Provider : IOptionsProvider
        {
            private readonly _DTE _dte;
            internal Provider(_DTE dte)
            {
                _dte = dte;
            }

            public void ShowDialog(IVimBuffer buffer)
            {
                var util = new KeyBindingUtil(_dte);
                var snapshot = util.CreateCommandKeyBindingSnapshot(buffer);
                new UI.ConflictingKeyBindingDialog(snapshot).ShowDialog();
            }
        }

        private readonly _DTE _dte;

        [ImportingConstructor]
        internal OptionsProviderFatory(SVsServiceProvider provider)
        {
            _dte = provider.GetService<SDTE, _DTE>();
        }

        public IOptionsProvider CreateOptionsProvider()
        {
            return new Provider(_dte);
        }
    }
}
