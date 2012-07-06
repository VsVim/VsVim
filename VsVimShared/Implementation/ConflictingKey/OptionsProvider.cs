using System;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.ConflictingKey
{
    [Export(typeof(IOptionsProviderFactory))]
    internal sealed class OptionsProviderFatory : IOptionsProviderFactory
    {
        private sealed class Provider : IOptionsProvider
        {
            private readonly IKeyBindingService _keyBindingService;
            private readonly IServiceProvider _serviceProvider;
            private readonly ILegacySettings _legacySettings;

            internal Provider(IKeyBindingService keyBindingService, IServiceProvider serviceProvider, ILegacySettings legacySettings)
            {
                _keyBindingService = keyBindingService;
                _serviceProvider = serviceProvider;
                _legacySettings = legacySettings;
            }

            public void ShowDialog(IVimBuffer vimBuffer)
            {
                try
                {
                    var snapshot = _keyBindingService.CreateCommandKeyBindingSnapshot(vimBuffer);
                    new ConflictingKeyBindingDialog(snapshot, _legacySettings).ShowDialog();
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

        private readonly IKeyBindingService _keyBindingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILegacySettings _legacySettings;

        [ImportingConstructor]
        internal OptionsProviderFatory(IKeyBindingService keyBindingService, SVsServiceProvider provider, ILegacySettings legacySettings)
        {
            _keyBindingService = keyBindingService;
            _serviceProvider = provider;
            _legacySettings = legacySettings;
        }

        public IOptionsProvider CreateOptionsProvider()
        {
            return new Provider(_keyBindingService, _serviceProvider, _legacySettings);
        }
    }
}
