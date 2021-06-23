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
using Vim.VisualStudio.Specific;

namespace Vim.EditorHost
{
    public sealed partial class EditorHostFactory
    {
#if VS_SPECIFIC_2015
        internal static EditorVersion DefaultEditorVersion => EditorVersion.Vs2015;
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
#if VS_SPECIFIC_2019
                "Microsoft.VisualStudio.Language.dll",
#endif
            };

        private readonly List<ComposablePartCatalog> _composablePartCatalogList = new List<ComposablePartCatalog>();
        private readonly List<ExportProvider> _exportProviderList = new List<ExportProvider>();

        public EditorHostFactory(bool includeSelf = true)
        {
            BuildCatalog(includeSelf);
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
            var aggregateCatalog = new AggregateCatalog(_composablePartCatalogList.ToArray());
#if DEBUG
            DumpExports();
#endif

            return new CompositionContainer(aggregateCatalog, _exportProviderList.ToArray());

#if DEBUG
            void DumpExports()
            {
                var exportNames = new List<string>();
                foreach (var catalog in aggregateCatalog)
                {
                    foreach (var exportDefinition in catalog.ExportDefinitions)
                    {
                        exportNames.Add(exportDefinition.ContractName);
                    }
                }

                exportNames.Sort();
                var groupedExportNames = exportNames
                    .GroupBy(x => x)
                    .Select(x => (Count: x.Count(), x.Key))
                    .OrderByDescending(x => x.Count)
                    .Select(x => $"{x.Count} {x.Key}")
                    .ToList();
            }
#endif
        }

        public EditorHost CreateEditorHost()
        {
            return new EditorHost(CreateCompositionContainer());
        }

        private void BuildCatalog(bool includeSelf)
        {
            // TODO_SHARED move the IVim assembly in here, silly for it not to be
            var editorAssemblyVersion = new Version(VisualStudioVersion.Major, 0);
            AppendEditorAssemblies(editorAssemblyVersion);
            AppendEditorAssembly("Microsoft.VisualStudio.Threading", VisualStudioThreadingVersion);
            _exportProviderList.Add(new JoinableTaskContextExportProvider());

            if (includeSelf)
            {
                // Other Exports needed to construct VsVim
                var types = new List<Type>()
                {
                    typeof(Implementation.BasicUndo.BasicTextUndoHistoryRegistry),
                    typeof(Implementation.Misc.BasicObscuringTipManager),
                    typeof(Implementation.Misc.VimErrorDetector),
    #if VS_SPECIFIC_2019
                    typeof(Implementation.Misc.BasicExperimentationServiceInternal),
                    typeof(Implementation.Misc.BasicLoggingServiceInternal),
                    typeof(Implementation.Misc.BasicObscuringTipManager),
    #elif VS_SPECIFIC_2017
                    typeof(Implementation.Misc.BasicLoggingServiceInternal),
                    typeof(Implementation.Misc.BasicObscuringTipManager),
    #elif VS_SPECIFIC_2015

    #else
    #error Unsupported configuration
    #endif

                };

                _composablePartCatalogList.Add(new TypeCatalog(types));
                _composablePartCatalogList.Add(VimSpecificUtil.GetTypeCatalog());
            }

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
