using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using IServiceProvider = System.IServiceProvider;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class HostFactory : IWpfTextViewCreationListener, IVimBufferCreationListener, IVsTextViewCreationListener
    {
        private readonly IKeyBindingService _keyBindingService;
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly Dictionary<IVimBuffer, VsCommandFilter> _filterMap = new Dictionary<IVimBuffer, VsCommandFilter>();
        private readonly IVimHost _host;
        private readonly IFileSystem _fileSystem;
        private readonly IVsAdapter _adapter;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            ITextEditorFactoryService editorFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService,
            IKeyBindingService keyBindingService,
            SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IVimHost host,
            IFileSystem fileSystem,
            IVsAdapter adapter)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _editorFactoryService = editorFactoryService;
            _editorOptionsFactoryService = editorOptionsFactoryService;
            _serviceProvider = serviceProvider;
            _adaptersFactory = adaptersFactory;
            _host = host;
            _fileSystem = fileSystem;
            _adapter = adapter;
        }

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            var buffer = _vim.GetOrCreateBuffer(textView);

            // Load the VimRC file if we haven't tried yet
            if (!_vim.IsVimRcLoaded && String.IsNullOrEmpty(_vim.Settings.VimRcPaths))
            {
                var func = FSharpFunc<Unit, ITextView>.FromConverter(_ => _editorFactoryService.CreateTextView());
                _vim.LoadVimRc(_fileSystem, func);
            }

            Action doCheck = () =>
                {
                    // Run the key binding check now
                    if (_keyBindingService.ConflictingKeyBindingState == ConflictingKeyBindingState.HasNotChecked)
                    {
                        if (Settings.Settings.Default.IgnoredConflictingKeyBinding)
                        {
                            _keyBindingService.IgnoreAnyConflicts();
                        }
                        else
                        {
                            _keyBindingService.RunConflictingKeyBindingStateCheck(buffer, (x, y) => { });
                        }
                    }
                };

            Dispatcher.CurrentDispatcher.BeginInvoke(doCheck, null);
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer buffer)
        {
            var textView = buffer.TextView;
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                _filterMap.Remove(buffer);
            };
        }

        void IVsTextViewCreationListener.VsTextViewCreated(IVsTextView vsView)
        {
            var view = _adaptersFactory.GetWpfTextView(vsView);
            if (view == null)
            {
                return;
            }

            var opt = _vim.GetBuffer(view);
            if (!opt.IsSome())
            {
                return;
            }

            var buffer = opt.Value;
            var filter = new VsCommandFilter(buffer, vsView, _serviceProvider);
            _filterMap.Add(buffer, filter);

            // Try and install the IVsFilterKeys adapter 
            VsFilterKeysAdapter.TryInstallFilterKeysAdapter(_adapter, _editorOptionsFactoryService, buffer);
        }
    }

}
