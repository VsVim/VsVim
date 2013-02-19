using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Vim;
using Vim.UI.Wpf;

namespace VimTestApp
{
    sealed class VimComponentHost : EditorHost
    {
        [ThreadStatic]
        private static CompositionContainer _vimCompositionContainer;

        private readonly ITextBufferFactoryService _textBufferFactoryService;

        internal ITextBufferFactoryService TextBufferFactoryService
        {
            get { return _textBufferFactoryService; }
        }

        internal VimComponentHost()
        {
            _textBufferFactoryService = GetOrCreateCompositionContainer().GetExportedValue<ITextBufferFactoryService>();
        }

        protected override CompositionContainer GetOrCreateCompositionContainer()
        {
            if (_vimCompositionContainer == null)
            {
                var list = GetVimCatalog();
                var catalog = new AggregateCatalog(list.ToArray());
                _vimCompositionContainer = new CompositionContainer(catalog);
            }

            return _vimCompositionContainer;
        }

        private static List<ComposablePartCatalog> GetVimCatalog()
        {
            var list = GetEditorUtilsCatalog();
            list.Add(new AssemblyCatalog(typeof(IVim).Assembly));
            list.Add(new AssemblyCatalog(typeof(VimKeyProcessor).Assembly));
            list.Add(new AssemblyCatalog(typeof(VimComponentHost).Assembly));

            return list;
        }
    }
}
