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
    sealed class VimComponentHost 
    {
        private readonly EditorHost _editorHost;
        private readonly IVim _vim;

        public EditorHost EditorHost
        {
            get { return _editorHost; }
        }

        public CompositionContainer CompositionContainer
        {
            get { return _editorHost.CompositionContainer; }
        }

        internal IVim Vim
        {
            get { return _vim; }
        }

        internal VimComponentHost()
        {
            var editorHostFactory = new EditorHostFactory();

            editorHostFactory.Add(new AssemblyCatalog(typeof(IVim).Assembly));
            editorHostFactory.Add(new AssemblyCatalog(typeof(VimKeyProcessor).Assembly));
            editorHostFactory.Add(new AssemblyCatalog(typeof(VimComponentHost).Assembly));
            _editorHost = editorHostFactory.CreateEditorHost();
            _vim = _editorHost.CompositionContainer.GetExportedValue<IVim>();
        }
    }
}
