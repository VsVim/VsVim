using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// This is the Visual Studio specific implementation of the typical Vim key processor.  The
    /// base key processor is sufficient to actually handle most types of input.  Unfortunately 
    /// there are Visual Studio specific quirks we need to handle.  
    /// </summary>
    internal sealed class VsKeyProcessor : VimKeyProcessor
    {
        private readonly IVsAdapter _adapter;
        private readonly IVimBufferCoordinator _bufferCoordinator;
        private readonly IReportDesignerUtil _reportDesignerUtil;
        private readonly IVimHost _vimHost;
        private int _keyDownCount;
        private Lazy<PropertyInfo> _searchInProgressInfo;

        internal int KeyDownCount
        {
            get { return _keyDownCount; }
        }

        internal VsKeyProcessor(IVsAdapter adapter, IVimBufferCoordinator bufferCoordinator, IKeyUtil keyUtil, IReportDesignerUtil reportDesignerUtil)
            : base(bufferCoordinator.VimBuffer, keyUtil)
        {
            _adapter = adapter;
            _reportDesignerUtil = reportDesignerUtil;
            _bufferCoordinator = bufferCoordinator;
            _vimHost = bufferCoordinator.VimBuffer.Vim.VimHost;
            _searchInProgressInfo = new Lazy<PropertyInfo>(FindSearchInProgressPropertyInfo);
        }

        /// <summary>
        /// This method is called to process KeyInput in any fashion by the IVimBuffer.  There are 
        /// several cases where we want to defer to Visual Studio and IOleCommandTarget for processing
        /// of a command.  In particular we don't want to process any text input here
        /// </summary>
        protected override bool TryProcess(KeyInput keyInput)
        {
            // If the ITextView doesn't have aggregate focus then this event needs to be discarded.  This will
            // happen when a peek definition window is active.  The peek window is a child and if it fails to 
            // process a key event it will bubble up to the parent window.  If the peek window has focus the
            // parent window shouldn't be handling the key
            if (!_vimHost.IsFocused(TextView))
            {
                return false;
            }

            // Check to see if we should be discarding this KeyInput value.  If it is discarded and 
            // made it back to us then we need to pretend that it was handled here
            if (_bufferCoordinator.IsDiscarded(keyInput))
            {
                return true;
            }

            // Don't handle input when incremental search is active.  Let Visual Studio handle it
            if (_adapter.IsIncrementalSearchActive(TextView))
            {
                VimTrace.TraceInfo("VsKeyProcessor::TryProcess Incremental search active");
                return false;
            }

            // In insert mode we don't want text input going directly to VsVim.  Text input must
            // be routed through Visual Studio and IOleCommandTarget in order to get intellisense
            // properly hooked up.  Not handling it in this KeyProcessor will eventually cause
            // it to be routed through IOleCommandTarget if it's input
            //
            // The Visual Studio KeyProcessor won't pass along control characters that are less than
            // or equal to 0x1f so we have to handle them here 
            if (VimBuffer.ModeKind.IsAnyInsert() && 
                !VimBuffer.CanProcessAsCommand(keyInput) &&
                (int)keyInput.Char > 0x1f)
            {
                return false;
            }

            // The report designer will process certain key strokes both through IOleCommandTarget and 
            // then back through the KeyProcessor interface.  This happens because they call into IOleCommandTarget
            // from Control.PreProcessMessage and if the execution succeeds they return false.  This 
            // causes it to continue to be processed as a normal message and hence leads to this 
            // interface.  Don't process any of these keys twice 
            if (_reportDesignerUtil.IsExpressionView(TextView) &&
                _reportDesignerUtil.IsSpecialHandled(keyInput))
            {
                return false;
            }

            return base.TryProcess(keyInput);
        }

        /// <summary>
        /// Once the key goes up the KeyStroke is complete and we should clear out the 
        /// DiscardedKeyInput flag as it's only relevant for a single key stroke
        /// </summary>
        public override void KeyUp(KeyEventArgs args)
        {
            OnKeyEvent(isDown: false);
            base.KeyUp(args);
        }

        public override void KeyDown(KeyEventArgs args)
        {
            OnKeyEvent(isDown: true);
            base.KeyDown(args);
        }

        /// <summary>
        /// Called for the KeyUp and KeyDown events.  This is needed to work around a feature
        /// of the VsCodeWindowAdapter class.  It overrides PreProcessMessage and will intercept
        /// all WM_CHAR messages when it considers the document to be readonly.  This prevents
        /// us from getting the TextInput event and hence we won't process a good chunk of
        /// commands.  
        /// 
        /// To work around this we will temporarily suppress the PreProcessMessage call by
        /// setting the SearchInProgress flag (this allows edit commands to go through).
        /// </summary>
        private void OnKeyEvent(bool isDown)
        {
            if (!_adapter.IsReadOnly(VimBuffer.TextView))
            {
                if (_keyDownCount > 0)
                {
                    DisablePreProcessMessageWorkaround();
                    _keyDownCount = 0;
                }

                return;
            }

            if (_keyDownCount == 0)
            {
                if (isDown)
                {
                    EnablePreProcessMessageWorkaround();
                    _keyDownCount = 1;
                }
            }
            else if (isDown)
            {
                _keyDownCount++;
            }
            else 
            {
                _keyDownCount--;
                if (_keyDownCount == 0)
                {
                    DisablePreProcessMessageWorkaround();
                }
            }
        }

        private void EnablePreProcessMessageWorkaround()
        {
            Debug.Assert(0 == _keyDownCount);
            SetSearchInProgress(true);
        }

        private void DisablePreProcessMessageWorkaround()
        {
            SetSearchInProgress(false);
        }

        private void SetSearchInProgress(bool value)
        {
            try
            {
                var propertyInfo = _searchInProgressInfo.Value;
                var vsTextView = _adapter.EditorAdapter.GetViewAdapter(VimBuffer.TextView);
                if (vsTextView != null && propertyInfo != null)
                {
                    propertyInfo.SetValue(vsTextView, value, null);
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private PropertyInfo FindSearchInProgressPropertyInfo()
        {
            var vsTextView = _adapter.EditorAdapter.GetViewAdapter(VimBuffer.TextView);
            if (vsTextView == null)
            {
                return null;
            }

            return vsTextView.GetType().GetProperty("SearchInProgress", BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
