using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;

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
        private readonly ITextBufferFactoryService _bufferFactoryService;
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IExternalEditorManager _externalEditorManager;
        private readonly IDisplayWindowBrokerFactoryService  _displayWindowBrokerFactoryServcie;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly Dictionary<IVimBuffer, VsCommandTarget> _filterMap = new Dictionary<IVimBuffer, VsCommandTarget>();
        private readonly IFileSystem _fileSystem;
        private readonly IVsAdapter _adapter;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            ITextBufferFactoryService bufferFactoryService,
            ITextEditorFactoryService editorFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService,
            IKeyBindingService keyBindingService,
            SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IExternalEditorManager externalEditorManager,
            IDisplayWindowBrokerFactoryService displayWindowBrokerFactoryService,
            IFileSystem fileSystem,
            IVsAdapter adapter)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _bufferFactoryService = bufferFactoryService;
            _editorFactoryService = editorFactoryService;
            _editorOptionsFactoryService = editorOptionsFactoryService;
            _externalEditorManager = externalEditorManager;
            _displayWindowBrokerFactoryServcie = displayWindowBrokerFactoryService;
            _adaptersFactory = adaptersFactory;
            _fileSystem = fileSystem;
            _adapter = adapter;
        }

        private void MaybeLoadVimRc()
        {
            if (!_vim.IsVimRcLoaded && String.IsNullOrEmpty(_vim.Settings.VimRcPaths))
            {
                // Need to pass the LoadVimRc call a function to create an ITextView that 
                // can be used to load the settings against.  We don't want this ITextView 
                // coming back through TextViewCreated so give it a ITextViewRole that won't
                // hit our filter 
                Func<ITextView> createViewFunc = () => _editorFactoryService.CreateTextView(
                    _bufferFactoryService.CreateTextBuffer(),
                    _editorFactoryService.NoRoles);
                if (!_vim.LoadVimRc(_fileSystem, createViewFunc.ToFSharpFunc()))
                {
                    // If no VimRc file is loaded add a couple of sanity settings
                    _vim.VimRcLocalSettings.AutoIndent = true;
                }
            }
        }


        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            // Load the VimRC file if we haven't tried yet
            MaybeLoadVimRc();

            // Create the IVimBuffer after loading the VimRc so that it gets the appropriate
            // settings
            var buffer = _vim.GetOrCreateBuffer(textView);

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
            var broker = _displayWindowBrokerFactoryServcie.CreateDisplayWindowBroker(view);
            var result = VsCommandTarget.Create(buffer, vsView, _adapter, broker, _externalEditorManager);
            if (result.IsSuccess)
            {
                _filterMap.Add(buffer, result.Value);
            }

            // Try and install the IVsFilterKeys adapter.  This cannot be done synchronously here
            // because Venus projects are not fully initialized at this state.  Merely querying 
            // for properties cause them to corrupt internal state and prevents rendering of the 
            // view.  Occurs for aspx and .js pages
            Action install = () => VsFilterKeysAdapter.TryInstallFilterKeysAdapter(_adapter, _editorOptionsFactoryService, buffer);

            Dispatcher.CurrentDispatcher.BeginInvoke(install, null);
        }


    }

}
