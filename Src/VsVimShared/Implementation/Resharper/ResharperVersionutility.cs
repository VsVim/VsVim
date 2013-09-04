using System.Reflection;

namespace VsVim.Implementation.ReSharper
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

        internal static ReSharperVersion DetectFromAssemblyName(string assemblyFullName)
        {
            if (assemblyFullName.StartsWith(ResharperAssemblyNameV8))
            {
                return ReSharperVersion.Version8;
            }

            if (assemblyFullName.StartsWith(ResharperAssemblyName2010) ||
                assemblyFullName.StartsWith(ResharperAssemblyName2012))
            {
                return ReSharperVersion.Version7AndEarlier;
            }

            return ReSharperVersion.Unknown;
        }

        internal static ReSharperVersion DetectFromAssembly(Assembly assembly)
        {
            return DetectFromAssemblyName(assembly.FullName);
        }
    }
}
