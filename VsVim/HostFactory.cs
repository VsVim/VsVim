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
    /// <summary>
    /// Factory responsible for creating IVimBuffer instances as ITextView instances are created 
    /// in Visual Studio
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class HostFactory : IWpfTextViewCreationListener, IVimBufferCreationListener, IVsTextViewCreationListener
    {
        /// <summary>
        /// Holds data about the IVimBuffer needed by the factory over the IVimBuffer life time
        /// </summary>
        private sealed class BufferData
        {
            internal int TabStop;
            internal bool ExpandTab;
            internal bool Number;
            internal VsCommandTarget VsCommandTarget;
        }

        private readonly IKeyBindingService _keyBindingService;
        private readonly ITextBufferFactoryService _bufferFactoryService;
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly IExternalEditorManager _externalEditorManager;
        private readonly IDisplayWindowBrokerFactoryService _displayWindowBrokerFactoryServcie;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly Dictionary<IVimBuffer, BufferData> _bufferMap = new Dictionary<IVimBuffer, BufferData>();
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
                if (!_vim.LoadVimRc(createViewFunc.ToFSharpFunc()))
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

            // Save the tab size and expand tab in case we need to reset them later
            var bufferData = new BufferData
            {
                TabStop = buffer.LocalSettings.TabStop,
                ExpandTab = buffer.LocalSettings.ExpandTab,
                Number = buffer.LocalSettings.Number
            };
            _bufferMap[buffer] = bufferData;

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
                _bufferMap.Remove(buffer);
            };
        }

        /// <summary>
        /// Raised when an IVsTextView is created.  When this occurs it means a previously created
        /// ITextView was associated with an IVsTextView shim.  This means the ITextView will be 
        /// hooked into the Visual Studio command system and a host of other items.  Setup all of
        /// our plumbing here
        /// </summary>
        void IVsTextViewCreationListener.VsTextViewCreated(IVsTextView vsView)
        {
            // Get the ITextView created.  Shouldn't ever be null unless a non-standard Visual Studio
            // component is calling this function
            var textView = _adaptersFactory.GetWpfTextView(vsView);
            if (textView == null)
            {
                return;
            }

            // Sanity check. No reason for this to be null
            var opt = _vim.GetBuffer(textView);
            if (!opt.IsSome())
            {
                return;
            }

            var buffer = opt.Value;
            BufferData bufferData;
            if (_bufferMap.TryGetValue(buffer, out bufferData))
            {
                // During the lifetime of an IVimBuffer the local and editor settings are kept
                // in sync for tab values.  At startup though a decision has to be made about which
                // settings should "win" and this is controlled by 'UseEditorSettings'.
                //
                // Visual Studio of course makes this difficult.  It will create an ITextView and 
                // then later force all of it's language preference settings down on the ITextView
                // if it does indeed have an IVsTextView.  This setting will inherently overwrite
                // the custom settings with the stored Visual Studio settings.  
                //
                // To work around this we store the original values and reset them here.  This event
                // is raised after this propagation occurs so we can put them back
                if (!_vim.Settings.UseEditorSettings)
                {
                    buffer.LocalSettings.TabStop = bufferData.TabStop;
                    buffer.LocalSettings.ExpandTab = bufferData.ExpandTab;
                    buffer.LocalSettings.Number = bufferData.Number;
                }
            }
            else
            {
                bufferData = new BufferData();
                _bufferMap[buffer] = bufferData;
            }

            var broker = _displayWindowBrokerFactoryServcie.CreateDisplayWindowBroker(textView);
            var result = VsCommandTarget.Create(buffer, vsView, _adapter, broker, _externalEditorManager);
            if (result.IsSuccess)
            {
                // Store the value for debugging
                bufferData.VsCommandTarget = result.Value;
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
