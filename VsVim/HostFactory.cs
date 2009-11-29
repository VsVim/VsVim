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
using VimCore;
using Microsoft.VisualStudio.UI.Undo;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsVim
{
    /// <summary>
    /// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
    /// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        [Import]
        public IVsEditorAdaptersFactoryService m_service = null;
        [Import]
        public IUndoHistoryRegistry m_undoHistoryRegistry = null;
        [Import]
        public KeyBindingService m_keyBindingService = null;

        private List<VsVimBuffer> m_bufferList = new List<VsVimBuffer>();

        public HostFactory()
        {

        }

        public void TextViewCreated(IWpfTextView textView)
        {
            textView.GotAggregateFocus += new EventHandler(OnGotAggregateFocus);
        }

        private void OnGotAggregateFocus(object sender, EventArgs e)
        {
            var view = sender as IWpfTextView;
            if (view == null)
            {
                return;
            }

            var interopView = m_service.GetViewAdapter(view);
            if (interopView == null)
            {
                return;
            }

            var interopLines = m_service.GetBufferAdapter(view.TextBuffer) as IVsTextLines;
            if (interopLines == null)
            {
                return;
            }

            // Once we have the view, stop listening to the event
            view.GotAggregateFocus -= new EventHandler(OnGotAggregateFocus);

            var buffer = new VsVimBuffer(view, interopView, interopLines, m_undoHistoryRegistry);
            view.Properties.AddTypedProperty(buffer);

            m_keyBindingService.OneTimeCheckForConflictingKeyBindings(buffer.VsVimHost.DTE, buffer.VimBuffer);
            m_bufferList.Add(buffer);
            view.Closed += (x, y) =>
            {
                view.Properties.RemoveTypedProperty<VsVimBuffer>();
                m_bufferList.Remove(buffer);
            };
        }
    


      
        
    }
}
