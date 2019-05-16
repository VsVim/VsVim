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
            Vs2010 = 10,
            Vs2012 = 11,
            Vs2013 = 12,
            Vs2015 = 14,
            Vs2017 = 15,
            Vs2019 = 16,

            MinimumSupported = Vs2015,
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

            /// <summary>
            /// Part of a component that is versioned.
            /// it's possible to load older versions in newer ones.
            /// Component version is not related to Visual Studio's version.
            /// Mapping with the version of Visual Studio should be done individually.
            /// </summary>
            ComponentVersioned,
        }

        internal readonly struct ReferenceData
        {
            internal AssemblyName Name { get; }
            internal ReferenceKind Kind { get; }

            /// <summary>
            /// What version of VS is this reference supported on.
            /// </summary>
            internal VsVersion? VsVersion { get; }

            internal ReferenceData(AssemblyName name, ReferenceKind kind, VsVersion? vsVersion)
            {
                Name = name;
                Kind = kind;
                VsVersion = vsVersion;
            }

            public override string ToString() => $"{Name} {VsVersion}";
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

            /// <summary>
            /// These are the core VsVim assemblies that load in every version of Visual Studio.
            /// </summary>
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
                VsVersion vsVersion;
                var name = assemblyName.Name;

                switch (name)
                {
                    case "System":
                    case "mscorlib":
                    case "PresentationCore":
                    case "PresentationFramework":
                    case "WindowsBase":
                    case "WindowsFormsIntegration":
                        return new ReferenceData(assemblyName, ReferenceKind.Framework, vsVersion: null);

                    case "Microsoft.VisualStudio.CoreUtility":
                    case "Microsoft.VisualStudio.Text.Data":
                    case "Microsoft.VisualStudio.Text.Logic":
                    case "Microsoft.VisualStudio.Text.UI":
                    case "Microsoft.VisualStudio.Text.UI.Wpf":
                    case "Microsoft.VisualStudio.Language":
                    case "Microsoft.VisualStudio.Language.Intellisense":
                    case "Microsoft.VisualStudio.Language.NavigateTo.Interfaces":
                        return new ReferenceData(assemblyName, ReferenceKind.Editor, getVersionFromVersionNumber());

                    case "Vim.Core":
                    case "Vim.UI.Wpf":
                    case "Vim.VisualStudio.Shared":
                    case "Vim.VisualStudio.Interfaces":
                        return new ReferenceData(assemblyName, ReferenceKind.VsVim, VsVersion.MinimumSupported);

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
                        return new ReferenceData(assemblyName, ReferenceKind.ShellPia, VsVersion.MinimumSupported);

                    case "Microsoft.VisualStudio.ComponentModelHost":
                    case "Microsoft.VisualStudio.Editor":
                    case "Microsoft.VisualStudio.Shell.10.0":
                    case "Microsoft.VisualStudio.Shell.11.0":
                    case "Microsoft.VisualStudio.Shell.12.0":
                    case "Microsoft.VisualStudio.Shell.Immutable.10.0":
                    case "Microsoft.VisualStudio.Shell.ViewManager":
                    case "Microsoft.VisualStudio.Utilities":
                        return new ReferenceData(assemblyName, ReferenceKind.ShellVersioned, getVersionFromVersionNumber());

                    case "Microsoft.VisualStudio.Platform.WindowManagement":
                        return new ReferenceData(assemblyName, ReferenceKind.ShellNonVersioned, getVersionFromVersionNumber());

                    case "Microsoft.CodeAnalysis":
                    case "Microsoft.CodeAnalysis.CSharp":
                    case "Microsoft.CodeAnalysis.CSharp.Scripting":
                    case "Microsoft.CodeAnalysis.Scripting":
                        if (new Version("3.0.0.0") <= assemblyName.Version)
                        {
                            vsVersion = VsVersion.Vs2019;
                        }
                        else if (new Version("2.10.0.0") <= assemblyName.Version)
                        {
                            vsVersion = VsVersion.Vs2017;
                        }
                        else
                        {
                            vsVersion = VsVersion.Vs2015;
                        }
                        return new ReferenceData(assemblyName, ReferenceKind.ComponentVersioned, vsVersion);

                    case "System.Collections.Immutable":
                        if (new Version("1.2.0.0") <= assemblyName.Version)
                        {
                            vsVersion = VsVersion.Vs2017;
                        }
                        else
                        {
                            vsVersion = VsVersion.Vs2015;
                        }
                        return new ReferenceData(assemblyName, ReferenceKind.ComponentVersioned, vsVersion);

                    default:
                        if (name.StartsWith("System."))
                        {
                            return new ReferenceData(assemblyName, ReferenceKind.Framework, vsVersion: null);
                        }
                        throw new Exception($"Unrecognized reference {name}");
                }

                VsVersion getVersionFromVersionNumber()
                {
                    var value = assemblyName.Version.Major;
                    if (Enum.IsDefined(typeof(VsVersion), value))
                    {
                        return (VsVersion)value;
                    }

                    throw new InvalidOperationException($"Invalid enum value {value}");
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
                switch (refData.Kind)
                {
                    case ReferenceKind.Editor:
                        Assert.Equal(VsVersion.MinimumSupported, refData.VsVersion);
                        break;
                    case ReferenceKind.ComponentVersioned:
                    case ReferenceKind.ShellVersioned:
                        Assert.True(refData.VsVersion <= VsVersion.MinimumSupported);
                        break;
                    case ReferenceKind.Framework:
                    case ReferenceKind.VsVim:
                        // Nothing to validate for these.
                        break;
                    case ReferenceKind.ShellNonVersioned:
                        Assert.True(false, "A non-versioned assembly should never be referenced in the core assembly set");
                        break;
                    case ReferenceKind.ShellPia:
                        Assert.NotNull(refData.VsVersion);
                        Assert.True(refData.VsVersion.Value <= VsVersion.MinimumSupported);
                        break;
                    default:
                        throw Contract.GetInvalidEnumException(refData.Kind);
                }

                count++;
            }

            Assert.True(count >= 60);
        }

        private void ValidateSpecific(VsVersion vsVersion, IEnumerable<ReferenceData> referenceDataCollection)
        {
            var badList = new List<ReferenceData>();
            var count = 0;
            foreach (var refData in referenceDataCollection)
            {
                switch (refData.Kind)
                {
                    case ReferenceKind.ComponentVersioned:
                    case ReferenceKind.ShellVersioned:
                    case ReferenceKind.ShellPia:
                        if (vsVersion < refData.VsVersion)
                        {
                            badList.Add(refData);
                        }
                        break;
                    case ReferenceKind.Framework:
                    case ReferenceKind.VsVim:
                        // Nothing to validate for these.
                        break;
                    case ReferenceKind.Editor:
                    case ReferenceKind.ShellNonVersioned:
                        if (refData.VsVersion != vsVersion)
                        {
                            badList.Add(refData);
                        }
                        break;
                    default:
                        throw Contract.GetInvalidEnumException(refData.Kind);
                }

                count++;
            }

            Assert.True(count >= 8);
            Assert.Empty(badList);
        }

        [Fact]
        public void Ensure2015()
        {
            ValidateSpecific(VsVersion.Vs2015, AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2015));
        }

        [Fact]
        public void Ensure2017()
        {
            ValidateSpecific(VsVersion.Vs2017, AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2017));
        }

        [Fact]
        public void Ensure2019()
        {
            ValidateSpecific(VsVersion.Vs2019, AssemblyData.GetTransitiveReferenceData(AssemblyData.VsVim2019));
        }
    }
}
