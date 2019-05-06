using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Vim;

namespace Vim.VisualStudio.Implementation.Roslyn
{
    [Export(typeof(IVimBufferCreationListener))]
    [Export(typeof(IExtensionAdapter))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class RoslynListenerFactory : IVimBufferCreationListener, IExtensionAdapter
    {
        private IRoslynRenameUtil _roslynRenameUtil;
        private bool _inRename;
        private List<IVimBuffer> _vimBufferList = new List<IVimBuffer>();

        internal IRoslynRenameUtil RenameUtil
        {
            get { return _roslynRenameUtil; }
            set
            {
                if (_roslynRenameUtil != null)
                {
                    _roslynRenameUtil.IsRenameActiveChanged -= OnIsRenameActiveChanged;
                }

                _roslynRenameUtil = value;
                if (_roslynRenameUtil != null)
                {
                    _roslynRenameUtil.IsRenameActiveChanged += OnIsRenameActiveChanged;
                }
            }
        }

        /// <summary>
        /// The Roslyn rename utility manipulates the undo / redo buffer directly during a 
        /// rename.  Need to register this as expected so the undo implementation doesn't
        /// raise any errors.
        /// </summary>
        internal bool IsUndoRedoExpected
        {
            get { return _roslynRenameUtil != null && _roslynRenameUtil.IsRenameActive; }
        }

        [ImportingConstructor]
        internal RoslynListenerFactory(SVsServiceProvider vsServiceProvider)
        {
            if (RoslynRenameUtil.TryCreate(vsServiceProvider, out IRoslynRenameUtil renameUtil))
            {
                RenameUtil = renameUtil;
            }
        }

        private void OnIsRenameActiveChanged(object sender, EventArgs e)
        {
            if (_inRename && !_roslynRenameUtil.IsRenameActive)
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
            else if (!_inRename && _roslynRenameUtil.IsRenameActive)
            {
                _inRename = true;
                foreach (var vimBuffer in _vimBufferList)
                {
                    vimBuffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                    vimBuffer.SwitchedMode += OnModeChange;
                }
            }
        }

        private void OnModeChange(object sender, EventArgs e)
        {
            if (_inRename && _roslynRenameUtil.IsRenameActive)
            {
                _roslynRenameUtil.Cancel();
            }
        }

        internal static bool IsRoslynContentType(IContentType contentType)
        {
            return contentType.IsCSharp() || contentType.IsVisualBasic();
        }

        internal void OnVimBufferCreated(IVimBuffer vimBuffer)
        {
            var contentType = vimBuffer.TextBuffer.ContentType;
            if (IsRoslynContentType(contentType))
            {
                _vimBufferList.Add(vimBuffer);
                vimBuffer.Closed += delegate { _vimBufferList.Remove(vimBuffer); };
            }
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            OnVimBufferCreated(vimBuffer);
        }

        #region IExtensionAdapter

        bool? IExtensionAdapter.IsUndoRedoExpected
        {
            get { return IsUndoRedoExpected; }
        }

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            return null;
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.UseDefaultCaret
        {
            get { return null; }
        }

        #endregion
    }
}
