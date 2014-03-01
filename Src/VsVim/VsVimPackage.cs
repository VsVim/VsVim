using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.ComponentModelHost;
using Vim.UI.Wpf;
using System.ComponentModel.Composition.Hosting;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidVsVimPkgString)]
    public sealed class VsVimPackage : Package
    {
        private IComponentModel _componentModel;
        private ExportProvider _exportProvider;
        private ITextManager _textManager;
        private IVim _vim;

        public VsVimPackage()
        {

        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            _componentModel = (IComponentModel)GetService(typeof(SComponentModel));
            _exportProvider = _componentModel.DefaultExportProvider;
            _vim = _exportProvider.GetExportedValue<IVim>();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                var optionsId = new CommandID(GuidList.guidVsVimCmdSet, (int)CommandIds.Options);
                var optionsMenuItem = new MenuCommand(OnOptionsClick, optionsId);
                mcs.AddCommand(optionsMenuItem);
            }
        }

        private void OnOptionsClick(object sender, EventArgs e)
        {
            // TODO: must handle the case better when there isn't an active buffer
            var vimBufferOption = _vim.FocusedBuffer;
            if (vimBufferOption.IsNone())
            {
                return;
            }

            var optionsProviderFactory = _componentModel.DefaultExportProvider.GetExportedValue<IOptionsProviderFactory>();
            optionsProviderFactory.CreateOptionsProvider().ShowDialog(vimBufferOption.Value);
        }

        /*
        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "VsVim",
                       string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }
        */
    }
}
