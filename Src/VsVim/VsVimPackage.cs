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
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", productId: VimConstants.VersionNumber, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(VsVim.Implementation.OptionPages.DefaultOptionPage), "VsVim", "Defaults", 0, 0, supportsAutomation: true)]
    [ProvideOptionPage(typeof(VsVim.Implementation.OptionPages.KeyboardOptionPage), "VsVim", "Keyboard", 0, 0, supportsAutomation: true)]
    [Guid(GuidList.VsVimPackageString)]
    public sealed class VsVimPackage : Package
    {
        private IComponentModel _componentModel;
        private ExportProvider _exportProvider;
        private IVim _vim;

        public VsVimPackage()
        {

        }

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
                var optionsId = new CommandID(GuidList.VsVimCommandSet, (int)CommandIds.Options);
                var optionsMenuItem = new MenuCommand(OnOptionsClick, optionsId);
                mcs.AddCommand(optionsMenuItem);
            }
        }

        private void OnOptionsClick(object sender, EventArgs e)
        {
            ShowOptionPage(typeof(VsVim.Implementation.OptionPages.KeyboardOptionPage));
        }
    }
}
