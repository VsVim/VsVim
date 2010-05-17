using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Editor;
using System.Diagnostics;
using System.Collections.Generic;
using Vim;
using Vim.Extensions;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell;
using EnvDTE;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class HostFactory : IWpfTextViewCreationListener, IVimBufferCreationListener, IVsTextViewCreationListener
    {
        private readonly IKeyBindingService _keyBindingService;
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly Dictionary<IVimBuffer, VsCommandFilter> _filterMap = new Dictionary<IVimBuffer, VsCommandFilter>();
        private readonly IVimHost _host;
        private readonly IFileSystem _fileSystem;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            ITextEditorFactoryService editorFactoryService,
            IKeyBindingService keyBindingService,
            SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IVimHost host,
            IFileSystem fileSystem)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _editorFactoryService = editorFactoryService;
            _serviceProvider = serviceProvider;
            _adaptersFactory = adaptersFactory;
            _host = host;
            _fileSystem = fileSystem;
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
            // Once we have the Vs view, stop listening to the event
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
        }
    }

}
