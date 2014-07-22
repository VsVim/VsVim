using System;
using System.Diagnostics;
using System.Reflection;

namespace Vim.VisualStudio.Implementation.ReSharper
{
    internal class ResharperVersionUtility
    {
        /// <summary>
        /// ReSharper version 7 and earlier for Visual Studio 2010 and before
        /// </summary>
        internal const string ResharperAssemblyName2010 = "JetBrains.Platform.ReSharper.VsIntegration.DevTen";

        /// <summary>
        /// ReSharper version 7 and earlier for Visual Studio 2012
        /// </summary>
        internal const string ResharperAssemblyName2012 = "JetBrains.Platform.ReSharper.VisualStudio.v10.v11";

        /// <summary>
        /// ReSharper version 8 for Visual Studio 2010 and later
        /// </summary>
        internal const string ResharperAssemblyNameV8 = "JetBrains.Platform.ReSharper.VisualStudio.SinceVs10";

        internal static ReSharperVersion DetectFromAssembly(Assembly assembly)
        {
            if (assembly.FullName.StartsWith(ResharperAssemblyNameV8))
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                var version = new Version(fvi.FileVersion);

                if (version.Major == 8)
                {
                    switch (version.Minor)
                    {
                        case 0:
                            return ReSharperVersion.Version8;
                        case 1:
                            return ReSharperVersion.Version81;
                        default:
                            return ReSharperVersion.Version81;
                    }
                }
                return ReSharperVersion.Unknown;
            }
            if (assembly.FullName.StartsWith(ResharperAssemblyName2010) ||
                assembly.FullName.StartsWith(ResharperAssemblyName2012))
            {
                return ReSharperVersion.Version7AndEarlier;
            }

            return ReSharperVersion.Unknown;
        }
    }
}
