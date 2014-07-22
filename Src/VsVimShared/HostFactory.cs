using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;  
using System.Windows.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Factory responsible for creating IVimBuffer instances as ITextView instances are created 
    /// in Visual Studio
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory :
        IWpfTextViewCreationListener,
        IVimBufferCreationListener,
        IVsTextViewCreationListener
    {
        private readonly HashSet<IVimBuffer> _toSyncSet = new HashSet<IVimBuffer>();
        private readonly Dictionary<IVimBuffer, VsCommandTarget> _vimBufferToCommandTargetMap = new Dictionary<IVimBuffer, VsCommandTarget>();
        private readonly ReadOnlyCollection<ICommandTargetFactory> _commandTargetFactoryList;
        private readonly IDisplayWindowBrokerFactoryService _displayWindowBrokerFactoryServcie;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly ITextManager _textManager;
        private readonly IVsAdapter _adapter;
        private readonly IVimProtectedOperations _protectedOperations;
        private readonly IVimBufferCoordinatorFactory _bufferCoordinatorFactory;
        private readonly IKeyUtil _keyUtil;
        private readonly IEditorToSettingsSynchronizer _editorToSettingSynchronizer;
        private readonly IVimApplicationSettings _vimApplicationSettings;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IDisplayWindowBrokerFactoryService displayWindowBrokerFactoryService,
            ITextManager textManager,
            IVsAdapter adapter,
            IVimProtectedOperations protectedOperations,
            IVimBufferCoordinatorFactory bufferCoordinatorFactory,
            IKeyUtil keyUtil,
            IEditorToSettingsSynchronizer editorToSettingSynchronizer,
            IVimApplicationSettings vimApplicationSettings,
            [ImportMany] IEnumerable<Lazy<ICommandTargetFactory, IOrderable>> commandTargetFactoryList)
        {
            _vim = vim;
            _displayWindowBrokerFactoryServcie = displayWindowBrokerFactoryService;
            _adaptersFactory = adaptersFactory;
            _textManager = textManager;
            _adapter = adapter;
            _protectedOperations = protectedOperations;
            _bufferCoordinatorFactory = bufferCoordinatorFactory;
            _keyUtil = keyUtil;
            _editorToSettingSynchronizer = editorToSettingSynchronizer;
            _vimApplicationSettings = vimApplicationSettings;
            _commandTargetFactoryList = Orderer.Order(commandTargetFactoryList).Select(x => x.Value).ToReadOnlyCollection();

#if DEBUG
            VimTrace.TraceSwitch.Level = TraceLevel.Info;
#endif
        }

        /// <summary>
        /// Begin the synchronization of settings for the given IVimBuffer.  It's okay if this method
        /// is called after synchronization is started.  The StartSynchronizing method will ignore
        /// multiple calls and only synchronize once
        /// </summary>
        private void BeginSettingSynchronization(IVimBuffer vimBuffer)
        {
            // Protect against multiple calls 
            if (!_toSyncSet.Remove(vimBuffer))
            {
                return;
            }

            // We have to make a decision on whether Visual Studio or Vim settings win during the startup
            // process.  If there was a Vimrc file then the vim settings win, else the Visual Studio ones
            // win.  
            //
            // By the time this function is called both the Vim and Editor settings are at their final 
            // values.  We just need to decide on a winner and copy one to the other 
            var settingSyncSource = _vimApplicationSettings.UseEditorDefaults
                ? SettingSyncSource.Editor
                : SettingSyncSource.Vim;

            // Synchronize any further changes between the buffers
            _editorToSettingSynchronizer.StartSynchronizing(vimBuffer, settingSyncSource);
        }

        private void ConnectToOleCommandTarget(IVimBuffer vimBuffer, ITextView textView, IVsTextView vsTextView)
        {
            var broker = _displayWindowBrokerFactoryServcie.GetDisplayWindowBroker(textView);
            var vimBufferCoordinator = _bufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            var result = VsCommandTarget.Create(vimBufferCoordinator, vsTextView, _textManager, _adapter, broker, _keyUtil, _vimApplicationSettings, _commandTargetFactoryList);
            if (result.IsSuccess)
            {
                // Store the value for debugging
                _vimBufferToCommandTargetMap[vimBuffer] = result.Value;
            }

            // Try and install the IVsFilterKeys adapter.  This cannot be done synchronously here
            // because Venus projects are not fully initialized at this state.  Merely querying 
            // for properties cause them to corrupt internal state and prevents rendering of the 
            // view.  Occurs for aspx and .js pages
            Action install = () => VsFilterKeysAdapter.TryInstallFilterKeysAdapter(_adapter, vimBuffer);

            _protectedOperations.BeginInvoke(install);
        }

        /// <summary>
        /// The JavaScript language service connects its IOleCommandTarget asynchronously based on events that we can't 
        /// reasonable hook into.  The current architecture of our IOleCommandTarget solution requires that we appear 
        /// before them in order to function.  This is particularly important for ReSharper behavior.  
        ///
        /// The most reliable solution we could find is to find the last event that JavaScript uses which is 
        /// OnGotAggregateFocus.  Once focus is achieved we schedule a background item to then connect to IOleCommandTarget. 
        /// This is very reliable in getting us in front of them.  
        /// 
        /// Long term we need to find a better solution here 
        /// </summary>
        private void ConnectJavaScriptToOleCommandTarget(IVimBuffer vimBuffer, ITextView textView, IVsTextView vsTextView)
        {
            Action connectInBackground = () => _protectedOperations.BeginInvoke(
                () => ConnectToOleCommandTarget(vimBuffer, textView, vsTextView),
                DispatcherPriority.Background);

            EventHandler onFocus = null;
            onFocus = (sender, e) =>
            {
                connectInBackground();
                textView.GotAggregateFocus -= onFocus;
            };

            if (textView.HasAggregateFocus)
            {
                connectInBackground();
            }
            else
            {
                textView.GotAggregateFocus += onFocus;
            }
        }

        #region IWpfTextViewCreationListener

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            // Create the IVimBuffer after loading the VimRc so that it gets the appropriate
            // settings
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(textView, out vimBuffer))
            {
                return;
            }

            // Visual Studio really puts us in a bind with respect to setting synchronization.  It doesn't
            // have a prescribed time to apply it's own customized settings and in fact differs between 
            // versions (2010 after TextViewCreated and 2012 is before).  If we start synchronizing 
            // before Visual Studio settings take affect then they will just overwrite the Vim settings. 
            //
            // We need to pick a point where VS is done with settings.  Then we can start synchronization
            // and change the settings to what we want them to be.
            //
            // In most cases we can just wait until IVsTextViewCreationListener.VsTextViewCreated fires 
            // because that happens after language preferences have taken affect.  Unfortunately this won't
            // fire for ever IWpfTextView.  If the IWpfTextView doesn't have any shims it won't fire.  So
            // we post a simple action as a backup mechanism to catch this case.  
            _toSyncSet.Add(vimBuffer);
            _protectedOperations.BeginInvoke(() => BeginSettingSynchronization(vimBuffer), DispatcherPriority.Loaded);
        }

        #endregion

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var textView = vimBuffer.TextView;
            textView.Closed += (x, y) =>
            {
                vimBuffer.Close();
                _toSyncSet.Remove(vimBuffer);
                _vimBufferToCommandTargetMap.Remove(vimBuffer);
            };
        }

        #endregion

        #region IVsTextViewCreationListener

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
            var textView = _adaptersFactory.GetWpfTextViewNoThrow(vsView);
            if (textView == null)
            {
                return;
            }

            IVimBuffer vimBuffer;
            if (!_vim.TryGetVimBuffer(textView, out vimBuffer))
            {
                return;
            }

            // At this point Visual Studio has fully applied it's settings.  Begin the synchronization process
            BeginSettingSynchronization(vimBuffer);

            var contentType = textView.TextBuffer.ContentType;
            if (contentType.IsJavaScript() || contentType.IsResJSON())
            {
                ConnectJavaScriptToOleCommandTarget(vimBuffer, textView, vsView);
            }
            else
            {
                ConnectToOleCommandTarget(vimBuffer, textView, vsView);
            }
        }

        #endregion
    }

}
