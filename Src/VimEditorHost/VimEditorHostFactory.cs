using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using Vim.UI.Wpf;

namespace Vim.EditorHost
{
    public sealed partial class VimEditorHostFactory
    {
#if VS_SPECIFIC_2017
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

        public VimEditorHostFactory(Func<Type, bool> typeFilter = null)
        {
            BuildCatalog(typeFilter ?? (_ => true));
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

        public VimEditorHost CreateVimEditorHost()
        {
            return new VimEditorHost(CreateCompositionContainer());
        }

        private void BuildCatalog(Func<Type, bool> typeFilter)
        {
#if VS_UNIT_TEST_HOST
            // The unit test host exports several replacement components to facilitate
            // better testing. For example it exports TestableClipboard which doesn't use
            // the real clipboard (would make testing flaky). Have to exclude the real components
            // here to avoid export conflicts
            Func<Type, bool> unitTestTypeFilter = type =>
                type != typeof(Vim.UI.Wpf.Implementation.Misc.ClipboardDevice) &&
                type != typeof(Vim.UI.Wpf.Implementation.Misc.KeyboardDeviceImpl) &&
                type != typeof(Vim.UI.Wpf.Implementation.Misc.MouseDeviceImpl) &&
                type.FullName != "Vim.VisualStudio.VsVimHost";
            var originalTypeFilter = typeFilter;

            typeFilter = type => originalTypeFilter(type) && unitTestTypeFilter(type);
#endif

            // https://github.com/VsVim/VsVim/issues/2905
            // Once VimEditorUtils is broken up correctly the composition code here should be 
            // reconsidered: particularly all of the ad-hoc exports below. Really need to move 
            // to a model where we export everything in the assemblies and provide a filter to 
            // exclude types at the call site when necessary for the given test.
            //
            // The ad-hoc export here is just too difficult to maintain and reason about. It's also
            // likely leading to situations where our test code is executing different than 
            // production because the test code doesn't have the same set of exports as production
            var editorAssemblyVersion = new Version(VisualStudioVersion.Major, 0);
            AppendEditorAssemblies(editorAssemblyVersion);
            AppendEditorAssembly("Microsoft.VisualStudio.Threading", VisualStudioThreadingVersion);
            _exportProviderList.Add(new JoinableTaskContextExportProvider());

            AddAssembly(typeof(IVim).Assembly);

            var wpfAssembly = typeof(IBlockCaret).Assembly;
            AddAssembly(wpfAssembly);

            var hostAssembly = typeof(VimEditorHostFactory).Assembly;
            if (hostAssembly != wpfAssembly)
            {
                AddAssembly(hostAssembly);
            }

            void AddAssembly(Assembly assembly)
            {
                var types = assembly
                    .GetTypes()
                    .Where(typeFilter)
                    .OrderBy(x => x.Name)
                    .ToList();
                _composablePartCatalogList.Add(new TypeCatalog(types));
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
