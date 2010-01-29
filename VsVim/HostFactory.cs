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
using Microsoft.VisualStudio.UI.Undo;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        [Import]
        private KeyBindingService _keyBindingService = null;
        [Import]
        private IVsEditorAdaptersFactoryService _adaptersFactory = null;
        [Import]
        private IVimFactoryService _vimFactory = null;
        [Import]
        private IVimHost _vimHost = null;

        public HostFactory()
        {
        }

        public void TextViewCreated(IWpfTextView textView)
        {
            var vsTextLines = _adaptersFactory.GetBufferAdapter(textView.TextBuffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return;
            }

            var objectWithSite = vsTextLines as IObjectWithSite;
            if (objectWithSite == null)
            {
                return;
            }

            var sp = objectWithSite.GetServiceProvider();
            EnsureDte(sp);

            var buffer = new VsVimBuffer(
                _vimFactory.Vim,
                textView,
                vsTextLines.GetFileName());
            textView.SetVimBuffer(buffer);

            // Run the key binding check now
            var dte = sp.GetService<SDTE, EnvDTE.DTE>();
            _keyBindingService.OneTimeCheckForConflictingKeyBindings(dte, buffer.VimBuffer);

            // Have to wait for Aggregate focus before being able to set the VsCommandFilter
            textView.GotAggregateFocus += new EventHandler(OnGotAggregateFocus);
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                textView.RemoveVimBuffer();
                ITextViewDebugUtil.Detach(textView);
            };
            ITextViewDebugUtil.Attach(textView);
        }



        private void OnGotAggregateFocus(object sender, EventArgs e)
        {
            var view = sender as IWpfTextView;
            if (view == null)
            {
                return;
            }

            var vsView = _adaptersFactory.GetViewAdapter(view);
            if (vsView == null)
            {
                return;
            }

            // Once we have the Vs view, stop listening to the event
            view.GotAggregateFocus -= new EventHandler(OnGotAggregateFocus);

            VsVimBuffer buffer;
            if (!view.TryGetVimBuffer(out buffer))
            {
                return;
            }

            buffer.VsCommandFilter = new VsCommandFilter(buffer.VimBuffer, vsView);
        }

        private void EnsureDte(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            var vimHost = _vimHost as VsVimHost;
            if (vimHost != null && vimHost.DTE == null)
            {
                vimHost.OnServiceProvider(sp);
            }
        }
    }

}
