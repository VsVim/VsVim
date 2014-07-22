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

namespace Vim.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", productId: VimConstants.VersionNumber, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.DefaultOptionPage), categoryName: "VsVim", pageName: "Defaults", categoryResourceID: 0, pageNameResourceID: 0, supportsAutomation: true)]
    [ProvideOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.KeyboardOptionPage), categoryName: "VsVim", pageName: "Keyboard", categoryResourceID: 0, pageNameResourceID: 0, supportsAutomation: true)]
    [Guid(GuidList.VsVimPackageString)]
    public sealed class VsVimPackage : Package, IOleCommandTarget
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
        }

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandGroup == GuidList.VsVimCommandSet)
            {
                switch (commandId)
                {
                    case CommandIds.Options:
                        ShowOptionPage(typeof(Vim.VisualStudio.Implementation.OptionPages.KeyboardOptionPage));
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }

                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        int IOleCommandTarget.QueryStatus(ref Guid commandGroup, uint commandsCount, OLECMD[] commands, IntPtr pCmdText)
        {
            if (commandGroup == GuidList.VsVimCommandSet && commandsCount == 1)
            {
                switch (commands[0].cmdID)
                {
                    case CommandIds.Options:
                        commands[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                        break;
                    default:
                        commands[0].cmdf = 0;
                        break;

                }

                return VSConstants.S_OK;
            }

            return VSConstants.E_FAIL;
        }

        #endregion 
    }
}
