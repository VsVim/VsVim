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

namespace EditorUtils
{
    public sealed partial class EditorHostFactory
    {
        internal static string[] CoreEditorComponents =
            new[]
            {
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Text.Internal.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
                "Microsoft.VisualStudio.Text.UI.Wpf.dll",
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        public EditorHostFactory(EditorVersion? editorVersion = null)
        {
            BuildBaseCatalog(editorVersion);
            _exportProviderList.Add(new UndoExportProvider());
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

        private void BuildBaseCatalog(EditorVersion? editorVersion)
        {
            Version vsVersion;
            GetEditorInfoAndHookResolve(editorVersion, out vsVersion);
            BuildBaseCatalog(vsVersion);
        }

        private void BuildBaseCatalog(Version vsVersion)
        {
            var editorAssemblyVersion = new Version(vsVersion.Major, 0);
            AppendEditorAssemblies(editorAssemblyVersion);
            AppendSpecificComponents(vsVersion, editorAssemblyVersion);
        }

        private static void GetEditorInfoAndHookResolve(EditorVersion? editorVersion, out Version vsVersion)
        {
            string vsInstallDirectory;
            if (!EditorLocatorUtil.TryGetEditorInfo(editorVersion, out vsVersion, out vsInstallDirectory))
            {
                throw new Exception("Unable to calculate the version of Visual Studio installed on the machine");
            }

            HookResolve(vsInstallDirectory);
        }

        /// <summary>
        /// Need to hook <see cref="AppDomain.AssemblyResolve" /> so that we can load the editor assemblies from the 
        /// desired location for this AppDomain.
        /// </summary>
        private static void HookResolve(string installDirectory)
        {
            var dirList = new List<string>();
            dirList.Add(Path.Combine(installDirectory, "PrivateAssemblies"));

            // Before 15.0 all of the editor assemblies were located in the GAC.  Hence no resolve needs to be done
            // because they will be discovered automatically when we load by the qualified name.  Starting in 15.0 
            // though the assemblies are not GAC'd and we need to load from the extension directory. 
            dirList.Add(Path.Combine(installDirectory, @"CommonExtensions\Microsoft\Editor"));

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                {
                    var assemblyName = new AssemblyName(e.Name);
                    var name = string.Format("{0}.dll", assemblyName.Name);
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
                var assembly = GetEditorAssembly(simpleName, editorAssemblyVersion);
                _composablePartCatalogList.Add(new AssemblyCatalog(assembly));
            }
        }

        private void AppendSpecificComponents(Version vsVersion, Version editorAssemblyVersion)
        {
            if (vsVersion.Major >= 15)
            {
                var qualifiedName = string.Format("EditorUtils.Host.Vs2017, Version={0}, Culture=neutral, PublicKeyToken={1}, processorArchitecture=MSIL", Constants.AssemblyVersion, Constants.PublicKeyToken);
                var assembly = Assembly.Load(qualifiedName);
                _composablePartCatalogList.Add(new AssemblyCatalog(assembly));

                _exportProviderList.Add(new JoinableTaskContextExportProvider());
            }
        }

        private static Assembly GetEditorAssembly(string assemblyName, Version version)
        {
            var qualifiedName = string.Format("{0}, Version={1}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL", assemblyName, version);
            return Assembly.Load(qualifiedName);
        }
    }
}
