using System;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation
{
    [Export(typeof(IOptionsProviderFactory))]
    internal sealed class OptionsProviderFatory : IOptionsProviderFactory
    {
        private sealed class Provider : IOptionsProvider
        {
            private readonly _DTE _dte;
            private readonly IServiceProvider _serviceProvider;

            internal Provider(_DTE dte, IServiceProvider serviceProvider)
            {
                _dte = dte;
                _serviceProvider = serviceProvider;
            }

            public void ShowDialog(IVimBuffer buffer)
            {
                try
                {
                    var util = new KeyBindingUtil(_dte);
                    var snapshot = util.CreateCommandKeyBindingSnapshot(buffer);
                    new UI.ConflictingKeyBindingDialog(snapshot).ShowDialog();
                }
                catch (Exception)
                {
                    // When dogfooding VsVim there is a bug in Visual Studio which causes the 
                    // VsVim DLL to be loaded twice (once for VsVim and once for the Settings
                    // and associated designers).  This causes WPF to error when loading the dialog
                    // resources and an unhandled exception occurs.  
                    VsShellUtilities.ShowMessageBox(
                        _serviceProvider,
                        "Error displaying options page",
                        "Error",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }

            }
        }

        private readonly _DTE _dte;
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        internal OptionsProviderFatory(SVsServiceProvider provider)
        {
            _dte = provider.GetService<SDTE, _DTE>();
            _serviceProvider = provider;
        }

        public IOptionsProvider CreateOptionsProvider()
        {
            return new Provider(_dte, _serviceProvider);
        }
    }
}
