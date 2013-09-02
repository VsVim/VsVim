using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Vim;
using Vim.UI.Wpf;

namespace VimApp
{
    sealed class VimComponentHost : EditorHost
    {
        [ThreadStatic]
        private readonly IVim _vim;

        internal IVim Vim
        {
            get { return _vim; }
        }

        internal VimComponentHost()
        {
            _vim = CompositionContainer.GetExportedValue<IVim>();
        }

        protected override void GetEditorHostParts(List<ComposablePartCatalog> composablePartCatalogList, List<ExportProvider> exportProviderList)
        {
            base.GetEditorHostParts(composablePartCatalogList, exportProviderList);

            composablePartCatalogList.Add(new AssemblyCatalog(typeof(IVim).Assembly));
            composablePartCatalogList.Add(new AssemblyCatalog(typeof(VimKeyProcessor).Assembly));
            composablePartCatalogList.Add(new AssemblyCatalog(typeof(VimComponentHost).Assembly));
        }
    }
}
