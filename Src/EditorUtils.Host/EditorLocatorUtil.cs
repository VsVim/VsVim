using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EditorUtils
{
    /// <summary>
    /// Utility for locating instances of Visual Studio on the machine. 
    /// </summary>
    internal static class EditorLocatorUtil
    {
        /// <summary>
        /// A list of key names for versions of Visual Studio which have the editor components 
        /// necessary to create an EditorHost instance.  Listed in preference order
        /// </summary>
        internal static readonly string[] VisualStudioSkuKeyNames =
            new[]
            {
                "VisualStudio",
                "WDExpress",
                "VCSExpress",
                "VCExpress",
                "VBExpress",
            };

        internal static bool TryGetEditorInfo(EditorVersion? editorVersion, out Version vsVersion, out string vsvsInstallDirectory)
        {
            if (editorVersion.HasValue)
            {
                return TryGetEditorInfo(editorVersion.Value, out vsVersion, out vsvsInstallDirectory);
            }

            foreach (var e in EditorVersionUtil.All)
            {
                if (TryGetEditorInfo(e, out vsVersion, out vsvsInstallDirectory))
                {
                    return true;
                }
            }

            vsVersion = default(Version);
            vsvsInstallDirectory = null;
            return false;
        }

        internal static bool TryGetEditorInfo(EditorVersion editorVersion, out Version vsVersion, out string vsInstallDirectory)
        {
            var majorVersion = EditorVersionUtil.GetMajorVersionNumber(editorVersion);
            return majorVersion < 15
                ? TryGetEditorInfoLegacy(majorVersion, out vsVersion, out vsInstallDirectory)
                : TryGetEditorInfoWillow(majorVersion, out vsVersion, out vsInstallDirectory);
        }

        private static bool TryGetEditorInfoLegacy(int majorVersion, out Version vsVersion, out string vsInstallDirectory)
        {
            if (TryGetVsInstallDirectoryLegacy(majorVersion, out vsInstallDirectory))
            {
                vsVersion = new Version(majorVersion, 0);
                return true;
            }

            vsVersion = default(Version);
            vsInstallDirectory = null;
            return false;
        }

        /// <summary>
        /// Try and get the installation directory for the specified SKU of Visual Studio.  This 
        /// will fail if the specified vsVersion of Visual Studio isn't installed.  Only works on 
        /// pre-willow VS installations (< 15.0).  
        /// </summary>
        private static bool TryGetVsInstallDirectoryLegacy(int majorVersion, out string vsInstallDirectory)
        {
            foreach (var skuKeyName in VisualStudioSkuKeyNames)
            {
                if (TryGetvsInstallDirectoryLegacy(majorVersion, skuKeyName, out vsInstallDirectory))
                {
                    return true;
                }
            }

            vsInstallDirectory = null;
            return false;
        }

        private static bool TryGetvsInstallDirectoryLegacy(int majorVersion, string skuKeyName,out string vsInstallDirectory)
        {
            try
            {
                var subKeyPath = String.Format(@"Software\Microsoft\{0}\{1}.0", skuKeyName, majorVersion);
                using (var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false))
                {
                    if (key != null)
                    {
                        vsInstallDirectory = key.GetValue("InstallDir", null) as string;
                        if (!String.IsNullOrEmpty(vsInstallDirectory))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore and try the next vsVersion
            }

            vsInstallDirectory = null;
            return false;
        }

        /// <summary>
        /// Get the first Willow VS installation with the specified major vsVersion.
        /// </summary>
        private static bool TryGetEditorInfoWillow(int majorVersion, out Version vsVersion, out string directory)
        {
            Debug.Assert(majorVersion >= 15);

            var setup = new SetupConfiguration();
            var e = setup.EnumAllInstances();
            var array = new ISetupInstance[] { null };
            do
            {
                var found = 0;
                e.Next(array.Length, array, out found);
                if (found == 0)
                {
                    break;
                }

                var instance = array[0];
                if (Version.TryParse(instance.GetInstallationVersion(), out vsVersion) &&
                    vsVersion.Major == majorVersion)
                {
                    directory = Path.Combine(instance.GetInstallationPath(), @"Common7\IDE");
                    return true;
                }
            }
            while (true);

            directory = null;
            vsVersion = default(Version);
            return false;
        }
    }
}
