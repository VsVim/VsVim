using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.PowerShellTools
{
    [Export(typeof(IPowerShellToolsUtil))]
    internal sealed class PowerShellToolsUtil : IPowerShellToolsUtil
    {
        //https://github.com/adamdriscoll/poshtools/blob/dev/PowerShellTools/Guids.cs
        //Visual Studio 2017,2019
        private static readonly Guid s_powerShellToolsPackageIdDev15 = new Guid("{0429083f-fdbc-47a3-84ff-b3d50343b21e}");
        //Visual Studio 2015
        private static readonly Guid s_powerShellToolsPackageIdDev14 = new Guid("{59875F69-67B7-4A5C-B33A-9E2C2B5D266D}");
        private readonly bool _isPowerShellToolsInstalled;

        [ImportingConstructor]
        internal PowerShellToolsUtil(SVsServiceProvider serviceProvider)
        {
            var dte = serviceProvider.GetService<SDTE, _DTE>();
            var version = VsVimHost.VisualStudioVersion;
            var guid = (version == VisualStudioVersion.Vs2015) ? s_powerShellToolsPackageIdDev14 : s_powerShellToolsPackageIdDev15;
            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();

            _isPowerShellToolsInstalled = vsShell.IsPackageInstalled(guid);
        }

        #region IPowerShellToolsUtil

        bool IPowerShellToolsUtil.IsInstalled
        {
            get { return _isPowerShellToolsInstalled; }
        }

        #endregion

    }
}
