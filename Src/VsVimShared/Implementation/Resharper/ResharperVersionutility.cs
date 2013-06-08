using System.Reflection;

namespace VsVim.Implementation.Resharper
{
    internal class ResharperVersionUtility
    {
        internal const string ResharperAssemblyName2010 = "JetBrains.Platform.ReSharper.VsIntegration.DevTen";
        internal const string ResharperAssemblyName2012 = "JetBrains.Platform.ReSharper.VisualStudio.v10.v11";

        /// <summary>
        /// Assembly name starting with ReSharper 8, valid for both Visual Studio 2010 and Visual Studio 2012
        /// </summary>
        internal const string ResharperAssemblyNameV8 = "JetBrains.Platform.ReSharper.VisualStudio.SinceVs10";

        public static ReSharperVersion DetectFromAssemblyName(string assemblyFullName)
        {
            if (assemblyFullName.StartsWith(ResharperAssemblyNameV8))
            {
                return ReSharperVersion.Version8;
            }
            if (assemblyFullName.StartsWith(ResharperAssemblyName2010) ||
                assemblyFullName.StartsWith(ResharperAssemblyName2012))
            {
                return ReSharperVersion.Version7AndEarlier;;
            }
            return ReSharperVersion.Unknown;;
        }

        public static ReSharperVersion DetectFromAssembly(Assembly assembly)
        {
            return DetectFromAssemblyName(assembly.FullName);
        }
    }
}