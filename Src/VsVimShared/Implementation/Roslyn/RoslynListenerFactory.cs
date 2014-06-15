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

namespace VsVim.Implementation.Roslyn
{
    [Export(typeof(IVimBufferCreationListener))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class RoslynListenerFactory : IVimBufferCreationListener
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

        [ImportingConstructor]
        internal RoslynListenerFactory(SVsServiceProvider vsServiceProvider)
        {
            IRoslynRenameUtil renameUtil;
            if (RoslynRenameUtil.TryCreate(vsServiceProvider, out renameUtil))
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
                }
            }
        }

        internal static bool IsRoslynContentType(IContentType contentType)
        {
            return contentType.IsCSharp();
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
    }
}
