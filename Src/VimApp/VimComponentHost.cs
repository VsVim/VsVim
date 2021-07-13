using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Vim;
using Vim.UI.Wpf;

namespace VimApp
{
    public sealed class VimComponentHost 
    {
        private readonly VimEditorHost _editorHost;
        private readonly IVim _vim;

        public VimEditorHost EditorHost
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
            var editorHostFactory = new VimEditorHostFactory();

            _editorHost = editorHostFactory.CreateVimEditorHost();
            _vim = _editorHost.CompositionContainer.GetExportedValue<IVim>();
        }
    }
}
