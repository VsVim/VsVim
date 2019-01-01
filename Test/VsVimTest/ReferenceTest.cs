extern alias VsVim2015;
extern alias VsVim2017;
extern alias VsVim2019;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vim.UI.Wpf;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public sealed class ReferenceTest
    {
        internal enum VsVersion
        {
            /// <summary>
            /// Current targeted version of Visual Studio
            /// </summary>
            VsCurrent,
            Vs2017,
            Vs2019
        }

        internal enum ReferenceKind
        {
            /// <summary>
            /// Part of the .NET framework.
            /// </summary>
            Framework,

            /// <summary>
            /// Part of the VsVim code base
            /// </summary>
            VsVim,

            /// <summary>
            /// Part of the core editor binaries (not a part of the Visual Studio shell).
            /// </summary>
            Editor,

            /// <summary>
            /// Part of the Visual Studio shell that is versioned. That means it appears in devenv.exe.config and 
            /// it's possible to load older versions in newer ones.
            /// </summary>
            ShellVersioned,

            /// <summary>
            /// Part of the Visual Studio shell that is not versioned. That means Visual Studio will only load the 
            /// specific version that was deployed with the installation.
            /// </summary>
            ShellNonVersioned,

            /// <summary>
            /// Primary interop assemblies used in the SHell. Similar to <see cref="ShellVersioned"/> in that it can
            /// be loaded in newer versions.
            /// </summary>
            ShellPia,
        }

        internal readonly struct ReferenceData
        {
            internal AssemblyName Name { get; }
            internal ReferenceKind Kind { get; }

            /// <summary>
            /// What version of VS is this reference supported on.
            /// </summary>
            internal VsVersion VsVersion { get; }

            internal ReferenceData(AssemblyName name, ReferenceKind kind, VsVersion vsVersion)
            {
                Name = name;
                Kind = kind;
                VsVersion = vsVersion;
            }
        }

        internal static class AssemblyData
        {
            internal static readonly Assembly VimCore = typeof(IVim).Assembly;
            internal static readonly Assembly VimWpf = typeof(VimHost).Assembly;
            internal static readonly Assembly VsVimShared = typeof(VsVimHost).Assembly;
            internal static readonly Assembly VsInterfaces = typeof(ISharedService).Assembly;
            internal static readonly Assembly VsVim = typeof(ISharedService).Assembly;
            internal static readonly Assembly VsVim2015 = typeof(VsVim2015::Vim.VisualStudio.Specific.SharedService).Assembly;
            internal static readonly Assembly VsVim2017 = typeof(VsVim2017::Vim.VisualStudio.Specific.SharedService).Assembly;
            internal static readonly Assembly VsVim2019 = typeof(VsVim2019::Vim.VisualStudio.Specific.SharedService).Assembly;

            internal static IEnumerable<Assembly> GetCoreAssemblies()
            {
                yield return VimCore;
                yield return VimWpf;
                yield return VsVimShared;
                yield return VsInterfaces;
                yield return VsVim;
            }

            internal static IEnumerable<Assembly> GetAllAssemblies()
            {
                foreach (var assembly in GetCoreAssemblies())
                {
                    yield return assembly;
                }

                yield return VsVim2015;
                yield return VsVim2017;
                yield return VsVim2019;
            }

            internal static List<ReferenceData> GetTransitiveReferenceData(Assembly assembly)
            {
                var references = assembly.GetReferencedAssemblies();
                var list = new List<ReferenceData>(references.Length);
                foreach (var reference in references)
                {
                    var data = GetReferenceData(reference);
                    list.Add(data);
                }

                return list;
            }

            internal static ReferenceData GetReferenceData(AssemblyName assemblyName)
            {
                var name = assemblyName.Name;
                if (name.StartsWith("System."))
                {
                    return new ReferenceData(assemblyName, ReferenceKind.Framework, VsVersion.VsCurrent);
                }

                switch (name)
                {
                    case "System":
                    case "mscorlib":
                    case "PresentationCore":
                    case "PresentationFramework":
                    case "WindowsBase":
                    case "WindowsFormsIntegration":
                        return new ReferenceData(assemblyName, ReferenceKind.Framework, VsVersion.VsCurrent);

                    case "Microsoft.VisualStudio.CoreUtility":
                    case "Microsoft.VisualStudio.Text.Data":
                    case "Microsoft.VisualStudio.Text.Logic":
                    case "Microsoft.VisualStudio.Text.UI":
                    case "Microsoft.VisualStudio.Text.UI.Wpf":
                    case "Microsoft.VisualStudio.Language.Intellisense":
                    case "Microsoft.VisualStudio.Language.NavigateTo.Interfaces":
                        return new ReferenceData(assemblyName, ReferenceKind.Editor, getVersionFromVersionNumber());

                    case "Vim.Core":
                    case "Vim.UI.Wpf":
                    case "Vim.VisualStudio.Shared":
                    case "Vim.VisualStudio.Interfaces":
                        return new ReferenceData(assemblyName, ReferenceKind.VsVim, VsVersion.VsCurrent);

                    case "EnvDTE":
                    case "EnvDTE80":
                    case "EnvDTE90":
                    case "EnvDTE100":
                    case "Microsoft.VisualStudio.OLE.Interop":
                    case "Microsoft.VisualStudio.Shell.Interop":
                    case "Microsoft.VisualStudio.Shell.Interop.8.0":
                    case "Microsoft.VisualStudio.Shell.Interop.9.0":
                    case "Microsoft.VisualStudio.Shell.Interop.10.0":
                    case "Microsoft.VisualStudio.Shell.Interop.11.0":
                    case "Microsoft.VisualStudio.Shell.Interop.12.0":
                    case "Microsoft.VisualStudio.TextManager.Interop":
                    case "Microsoft.VisualStudio.TextManager.Interop.8.0":
                    case "Microsoft.VisualStudio.TextManager.Interop.10.0":
                        return new ReferenceData(assemblyName, ReferenceKind.ShellPia, VsVersion.VsCurrent);

                    case "Microsoft.VisualStudio.ComponentModelHost":
                    case "Microsoft.VisualStudio.Editor":
                    case "Microsoft.VisualStudio.Shell.10.0":
                    case "Microsoft.VisualStudio.Shell.11.0":
                    case "Microsoft.VisualStudio.Shell.12.0":
                    case "Microsoft.VisualStudio.Shell.Immutable.10.0":
                    case "Microsoft.VisualStudio.Shell.ViewManager":
                        return new ReferenceData(assemblyName, ReferenceKind.ShellVersioned, getVersionFromVersionNumber());

                    case "Microsoft.VisualStudio.Platform.WindowManagement":
                        return new ReferenceData(assemblyName, ReferenceKind.ShellNonVersioned, getVersionFromVersionNumber());

                    default:
                        throw new Exception($"Unrecognized reference {name}");
                }

                VsVersion getVersionFromVersionNumber()
                {
                    switch (assemblyName.Version.Major)
                    {
                        case 10:
                        case 11:
                        case 12:
                        case 14:
                            return VsVersion.VsCurrent;
                        case 15: return VsVersion.Vs2017;
                        case 16: return VsVersion.Vs2019;
                        default: throw new Exception();
                    }
                }
            }
        }

        /// <summary>
        /// Make sure the correct VS SDK binaries are referenced in the core binaries.
        /// </summary>
        [Fact]
        public void EnsureCorrectVisualStudioVersion()
        {
            var count = 0;
            foreach (var refData in AssemblyData.GetCoreAssemblies().SelectMany(a => AssemblyData.GetTransitiveReferenceData(a)))
            {
                Assert.NotEqual(ReferenceKind.ShellNonVersioned, refData.Kind);
                Assert.Equal(VsVersion.VsCurrent, refData.VsVersion);
                count++;
            }

            Assert.True(count >= 60);
        }

        [Fact]
        public void Ensure2015()
        {
            foreach (var refData in AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2015))
            {
                Assert.Equal(VsVersion.VsCurrent, refData.VsVersion);
            }
        }

        [Fact]
        public void Ensure2017()
        {
            foreach (var refData in AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2017))
            {
                Assert.True(refData.VsVersion == VsVersion.Vs2017 || refData.VsVersion == VsVersion.VsCurrent);
            }
        }

        [Fact]
        public void Ensure2019()
        {
            foreach (var refData in AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2019))
            {
                Assert.True(refData.VsVersion == VsVersion.Vs2019 || refData.VsVersion == VsVersion.VsCurrent);
            }
        }
    }
}
