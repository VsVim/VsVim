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
#if VS_SPECIFIC_2015
        internal static EditorVersion DefaultEditorVersion => EditorVersion.Vs2017;
        internal static Version VisualStudioVersion => new Version(14, 0, 0, 0);
        internal static Version VisualStudioThreadingVersion => new Version(14, 0, 0, 0);
#elif VS_SPECIFIC_2017
        internal static EditorVersion DefaultEditorVersion => EditorVersion.Vs2017;
        internal static Version VisualStudioVersion => new Version(15, 0, 0, 0);
        internal static Version VisualStudioThreadingVersion => new Version(15, 3, 0, 0);
#elif VS_SPECIFIC_2019
        internal static EditorVersion DefaultEditorVersion => EditorVersion.Vs2019;
        internal static Version VisualStudioVersion => new Version(16, 0, 0, 0);
        internal static Version VisualStudioThreadingVersion => new Version(16, 0, 0, 0);
#elif VS_SPECIFIC_2022
        internal static EditorVersion DefaultEditorVersion => EditorVersion.Vs2022;
        internal static Version VisualStudioVersion => new Version(17, 0, 0, 0);
        internal static Version VisualStudioThreadingVersion => new Version(17, 0, 0, 0);
#else
#error Unsupported configuration
#endif

        internal static string[] CoreEditorComponents =
            new[]
            {
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Text.Internal.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
                "Microsoft.VisualStudio.Text.UI.Wpf.dll",
#if VS_SPECIFIC_2019 || VS_SPECIFIC_2022
                "Microsoft.VisualStudio.Language.dll",
#endif
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        public EditorHostFactory()
        {
            BuildCatalog();
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

        private void BuildCatalog()
        {
            var editorAssemblyVersion = new Version(VisualStudioVersion.Major, 0);
            AppendEditorAssemblies(editorAssemblyVersion);
            AppendEditorAssembly("Microsoft.VisualStudio.Threading", VisualStudioThreadingVersion);
            _exportProviderList.Add(new JoinableTaskContextExportProvider());
            _composablePartCatalogList.Add(new AssemblyCatalog(typeof(EditorHostFactory).Assembly));
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
