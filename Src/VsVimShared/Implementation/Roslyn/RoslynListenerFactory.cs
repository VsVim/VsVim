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
        private readonly SVsServiceProvider _vsServiceProvider;
        private RoslynRenameUtil _roslynRenameUtil;
        private bool _hasQueriedRoslyn;
        private bool _inRename;
        private List<IVimBuffer> _vimBufferList = new List<IVimBuffer>();

        private bool IsRoslynAvailable
        {
            get
            {
                MaybeLoadRoslynRenameUtil();
                return _roslynRenameUtil != null;
            }
        }

        [ImportingConstructor]
        internal RoslynListenerFactory(SVsServiceProvider vsServiceProvider)
        {
            _vsServiceProvider = vsServiceProvider;
        }

        private void MaybeLoadRoslynRenameUtil()
        {
            if (_hasQueriedRoslyn)
            {
                return;
            }

            _hasQueriedRoslyn = true;
            if (RoslynRenameUtil.TryCreate(_vsServiceProvider, out _roslynRenameUtil))
            {
                _roslynRenameUtil.IsRenameActiveChanged += OnIsRenameActiveChanged;
            }
        }

        private void OnIsRenameActiveChanged(object sender, EventArgs e)
        {
            if (_inRename && !_roslynRenameUtil.IsRenameActive)
            {
                _inRename = false;
                foreach (var vimBuffer in _vimBufferList)
                {
                    vimBuffer.SwitchPreviousMode();
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

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var contentType = vimBuffer.TextBuffer.ContentType;
            if (contentType.IsCSharp() && IsRoslynAvailable)
            {
                _vimBufferList.Add(vimBuffer);
                vimBuffer.Closed += delegate { _vimBufferList.Remove(vimBuffer); };
            }
        }
    }
}
