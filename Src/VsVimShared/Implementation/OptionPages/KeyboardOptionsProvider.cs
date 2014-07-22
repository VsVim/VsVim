using System;
using System.ComponentModel.Composition;
using EditorUtils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;
using Vim.UI.Wpf;
using Vim.Extensions;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio;

namespace Vim.VisualStudio.Implementation.OptionPages
{
    [Export(typeof(IKeyboardOptionsProvider))]
    internal sealed class KeyboardOptionsProvider : IKeyboardOptionsProvider
    {
        private readonly IVsShell _vsShell;

        [ImportingConstructor]
        internal KeyboardOptionsProvider(SVsServiceProvider serviceProvider)
        {
            _vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
        }

        private void ShowOptionsPage()
        {
            IVsPackage vsPackage;
            Guid packageGuid = Constants.PackageGuid;
            if (ErrorHandler.Succeeded(_vsShell.LoadPackage(ref packageGuid, out vsPackage)))
            {
                var package = vsPackage as Package;
                if (package != null)
                {
                    package.ShowOptionPage(typeof(KeyboardOptionPage));
                }
            }
        }

        #region IKeyboardOptionsProvider

        void IKeyboardOptionsProvider.ShowOptionsPage()
        {
            ShowOptionsPage();
        }

        #endregion
    }
}
