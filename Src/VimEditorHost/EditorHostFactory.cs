using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.Win32;

namespace Vim.EditorHost
{
    public sealed partial class EditorHostFactory
    {
        internal static EditorVersion DefaultEditorVersion =>
#if VS2015
            EditorVersion.Vs2015;
#elif VS2017
            EditorVersion.Vs2017;
#elif VS2019
            EditorVersion.Vs2019;
#else
#error Bad version
#endif

        internal static string[] CoreEditorComponents =
            new[]
            {
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Text.Internal.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
                "Microsoft.VisualStudio.Text.UI.Wpf.dll",
#if VS2019
                "Microsoft.VisualStudio.Language.dll",
#endif
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        public EditorHostFactory(EditorVersion? editorVersion = null)
        {
            BuildCatalog(editorVersion ?? DefaultEditorVersion);
        }

        public void Add(ComposablePartCatalog composablePartCatalog)
        {
            _composablePartCatalogList.Add(composablePartCatalog);
        }

        public void Add(ExportProvider exportProvider)
        {
            _exportProviderList.Add(exportProvider);
        }

        public CompositionContainer CreateCompositionContainer()
        {
            var catalog = new AggregateCatalog(_composablePartCatalogList.ToArray());
            return new CompositionContainer(catalog, _exportProviderList.ToArray());
        }

        public EditorHost CreateEditorHost()
        {
            return new EditorHost(CreateCompositionContainer());
        }

        private void BuildCatalog(EditorVersion editorVersion)
        {
            GetEditorInfoAndHookResolve(editorVersion, out Version vsVersion);
            BuildCatalog(vsVersion);
        }

        private void BuildCatalog(Version vsVersion)
        {
            var editorAssemblyVersion = new Version(vsVersion.Major, 0);
            AppendEditorAssemblies(editorAssemblyVersion);

#if VS2015
            // No threading DLL to worry about here.
#elif VS2017
            AppendEditorAssembly("Microsoft.VisualStudio.Threading", new Version(15, 3));
            _exportProviderList.Add(new JoinableTaskContextExportProvider());
#elif VS2019
            AppendEditorAssembly("Microsoft.VisualStudio.Threading", new Version(16, 0));
            _exportProviderList.Add(new JoinableTaskContextExportProvider());
#endif

            _composablePartCatalogList.Add(new AssemblyCatalog(typeof(EditorHostFactory).Assembly));
        }

        private static void GetEditorInfoAndHookResolve(EditorVersion editorVersion, out Version vsVersion)
        {
            if (!EditorLocatorUtil.TryGetEditorInfo(editorVersion, out vsVersion, out string vsInstallDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            if (vsVersion.Major <= 14)
            {
                HookResolve(vsInstallDirectory);
            }
        }

        /// <summary>
        /// Need to hook <see cref="AppDomain.AssemblyResolve" /> so that we can load the editor assemblies from the 
        /// desired location for this AppDomain.
        /// </summary>
        private static void HookResolve(string installDirectory)
        {
            var dirList = new List<string>
            {
                Path.Combine(installDirectory, "PrivateAssemblies"),

                // Before 15.0 all of the editor assemblies were located in the GAC.  Hence no resolve needs to be done
                // because they will be discovered automatically when we load by the qualified name.  Starting in 15.0 
                // though the assemblies are not GAC'd and we need to load from the extension directory. 
                Path.Combine(installDirectory, @"CommonExtensions\Microsoft\Editor")
            };

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                {
                    var assemblyName = new AssemblyName(e.Name);
                    var name = $"{assemblyName.Name}.dll";
                    foreach (var dir in dirList)
                    {
                        var fullName = Path.Combine(dir, name);
                        if (File.Exists(fullName))
                        {
                            return Assembly.LoadFrom(fullName);
                        }
                    }

                    return null;
                };
        }

        private void AppendEditorAssemblies(Version editorAssemblyVersion)
        {
            foreach (var name in CoreEditorComponents)
            {
                var simpleName = Path.GetFileNameWithoutExtension(name);
                AppendEditorAssembly(simpleName, editorAssemblyVersion);
            }
        }

        private void AppendEditorAssembly(string name, Version version)
        {
            var assembly = GetEditorAssembly(name, version);
            _composablePartCatalogList.Add(new AssemblyCatalog(assembly));
        }

        private static Assembly GetEditorAssembly(string assemblyName, Version version)
        {
            var qualifiedName = $"{assemblyName}, Version={version}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL";
            return Assembly.Load(qualifiedName);
        }
    }
}
