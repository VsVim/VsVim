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
        private readonly KeyBindingService _keyBindingService;
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly Dictionary<IVimBuffer, VsCommandFilter> _filterMap = new Dictionary<IVimBuffer, VsCommandFilter>();
        private readonly IVimHost _host;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            ITextEditorFactoryService editorFactoryService,
            KeyBindingService keyBindingService,
            SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IVimHost host)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _editorFactoryService = editorFactoryService;
            _serviceProvider = serviceProvider;
            _adaptersFactory = adaptersFactory;
            _host = host;
        }

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            var buffer = _vim.GetOrCreateBuffer(textView);

            // Load the VimRC file if we haven't tried yet
            if (!_vim.IsVimRcLoaded && String.IsNullOrEmpty(_vim.Settings.VimRcPaths))
            {
                var func = FSharpFunc<Unit, ITextView>.FromConverter(_ => _editorFactoryService.CreateTextView());
                _vim.LoadVimRc(func);
            }

            Action doCheck = () =>
                {
                    // Run the key binding check now
                    _keyBindingService.OneTimeCheckForConflictingKeyBindings(buffer);
                };

            Dispatcher.CurrentDispatcher.BeginInvoke(doCheck, null);
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer buffer)
        {
            var dte = (_DTE)_serviceProvider.GetService(typeof(_DTE));
            buffer.ErrorMessage += (unused, msg) => dte.StatusBar.Text = msg;
            buffer.StatusMessage += (unused, msg) => dte.StatusBar.Text = msg;

            var textView = buffer.TextView;
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                _filterMap.Remove(buffer);
                ITextViewDebugUtil.Detach(textView);
            };
            ITextViewDebugUtil.Attach(textView);
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
            var filter = new VsCommandFilter(buffer, vsView);
            _filterMap.Add(buffer, filter);
        }
    }

}
