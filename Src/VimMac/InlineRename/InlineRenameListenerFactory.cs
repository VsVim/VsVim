//using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Vim.Mac;
using Vim.VisualStudio;

namespace Vim.UI.Cocoa.Implementation.InlineRename
{
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IExtensionAdapter))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class InlineRenameListenerFactory : VimExtensionAdapter, IVimBufferCreationListener
    {
        private readonly IVimApplicationSettings _vimApplicationSettings;

        private IInlineRenameUtil _inlineRenameUtil;
        private bool _inRename;
        private List<IVimBuffer> _vimBufferList = new List<IVimBuffer>();

        // Undo-redo is expected when the inline rename window is active.
        protected override bool IsUndoRedoExpected =>
            IsActive;

        internal IInlineRenameUtil RenameUtil
        {
            //get { return _inlineRenameUtil; }
            set
            {
                if (_inlineRenameUtil != null)
                {
                    _inlineRenameUtil.IsRenameActiveChanged -= OnIsRenameActiveChanged;
                }

                _inlineRenameUtil = value;
                if (_inlineRenameUtil != null)
                {
                    _inlineRenameUtil.IsRenameActiveChanged += OnIsRenameActiveChanged;
                }
            }
        }

        /// <summary>
        /// The inline rename utility manipulates the undo / redo buffer
        /// directly during a  rename.  Need to register this as expected so
        /// the undo implementation doesn't raise any errors.
        /// </summary>
        internal bool IsActive => _inlineRenameUtil != null && _inlineRenameUtil.IsRenameActive;

        [ImportingConstructor]
        internal InlineRenameListenerFactory(
            IVimApplicationSettings vimApplicationSettings)
        {
            _vimApplicationSettings = vimApplicationSettings;

            if (InlineRenameUtil.TryCreate(out IInlineRenameUtil renameUtil))
            {
                RenameUtil = renameUtil;
            }
        }

        private void OnIsRenameActiveChanged(object sender, EventArgs e)
        {
            if (_inRename && !_inlineRenameUtil.IsRenameActive)
            {
                _inRename = false;
                foreach (var vimBuffer in _vimBufferList)
                {
                    vimBuffer.SwitchedMode -= OnModeChange;
                    if (vimBuffer.ModeKind == ModeKind.ExternalEdit)
                    {
                        vimBuffer.SwitchPreviousMode();
                    }
                }
            }
            else if (!_inRename && _inlineRenameUtil.IsRenameActive)
            {
                _inRename = true;
                foreach (var vimBuffer in _vimBufferList)
                {
                    // Respect the user's edit monitoring setting.
                    if (_vimApplicationSettings.EnableExternalEditMonitoring)
                    {
                        vimBuffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                    }
                    vimBuffer.SwitchedMode += OnModeChange;
                }
            }
        }

        private void OnModeChange(object sender, SwitchModeEventArgs args)
        {
            if (_inRename && _inlineRenameUtil.IsRenameActive && args.ModeArgument.IsCancelOperation)
            {
                _inlineRenameUtil.Cancel();
            }
        }

        internal static bool IsInlineRenameContentType(IContentType contentType)
        {
            return
                contentType.IsCSharp() ||
                //contentType.IsFSharp() ||
                contentType.IsVisualBasic();
        }

        internal void OnVimBufferCreated(IVimBuffer vimBuffer)
        {
            var contentType = vimBuffer.TextBuffer.ContentType;
            if (IsInlineRenameContentType(contentType))
            {
                _vimBufferList.Add(vimBuffer);
                vimBuffer.Closed += delegate { _vimBufferList.Remove(vimBuffer); };
            }
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            OnVimBufferCreated(vimBuffer);
        }
    }
}
