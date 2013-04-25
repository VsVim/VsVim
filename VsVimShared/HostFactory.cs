using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
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
        private readonly HashSet<IVimBuffer> _toSyncSet = new HashSet<IVimBuffer>();
        private readonly Dictionary<IVimBuffer, VsCommandTarget> _vimBufferToCommandTargetMap = new Dictionary<IVimBuffer, VsCommandTarget>();
        private readonly IResharperUtil _resharperUtil;
        private readonly IDisplayWindowBrokerFactoryService _displayWindowBrokerFactoryServcie;
        private readonly IVim _vim;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly ITextManager _textManager;
        private readonly IVsAdapter _adapter;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVimBufferCoordinatorFactory _bufferCoordinatorFactory;
        private readonly IKeyUtil _keyUtil;
        private readonly IEditorToSettingsSynchronizer _editorToSettingSynchronizer;

        [ImportingConstructor]
        public HostFactory(
            IVim vim,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IResharperUtil resharperUtil,
            IDisplayWindowBrokerFactoryService displayWindowBrokerFactoryService,
            ITextManager textManager,
            IVsAdapter adapter,
            [EditorUtilsImport] IProtectedOperations protectedOperations,
            IVimBufferCoordinatorFactory bufferCoordinatorFactory,
            IKeyUtil keyUtil,
            IEditorToSettingsSynchronizer editorToSettingSynchronizer)
        {
            _vim = vim;
            _resharperUtil = resharperUtil;
            _displayWindowBrokerFactoryServcie = displayWindowBrokerFactoryService;
            _adaptersFactory = adaptersFactory;
            _textManager = textManager;
            _adapter = adapter;
            _protectedOperations = protectedOperations;
            _bufferCoordinatorFactory = bufferCoordinatorFactory;
            _keyUtil = keyUtil;
            _editorToSettingSynchronizer = editorToSettingSynchronizer;

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

            // Synchronize any further changes between the buffers
            _editorToSettingSynchronizer.StartSynchronizing(vimBuffer); 

            // We have to make a decision on whether Visual Studio or Vim settings win during the startup
            // process.  If there was a Vimrc file then the vim settings win, else the Visual Studio ones
            // win.  
            //
            // By the time this function is called both the Vim and Editor settings are at their final 
            // values.  We just need to decide on a winner and copy one to the other 
            if (_vim.VimRcState.IsLoadSucceeded && !_vim.GlobalSettings.UseEditorDefaults)
            {
                // Vim settings win.  
                _editorToSettingSynchronizer.CopyVimToEditorSettings(vimBuffer);
            }
            else
            {
                // Visual Studio settings win.  
                _editorToSettingSynchronizer.CopyEditorToVimSettings(vimBuffer);
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

            var broker = _displayWindowBrokerFactoryServcie.CreateDisplayWindowBroker(textView);
            var bufferCoordinator = _bufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            var result = VsCommandTarget.Create(bufferCoordinator, vsView, _textManager, _adapter, broker, _resharperUtil, _keyUtil);
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

        #endregion
    }

}
