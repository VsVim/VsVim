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

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        public const string BlockAdornmentLayerName = "BlockCaret";

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition BlockAdornmentLayer = null;

        [Import]
        private IVsEditorAdaptersFactoryService _service = null;
        [Import]
        private IUndoHistoryRegistry _undoHistoryRegistry = null;
        [Import]
        private KeyBindingService _keyBindingService = null;
        [Import]
        private IEditorFormatMapService _editorFormatMapService = null;
        [Import]
        private IEditorOperationsFactoryService _editorOperationsFactoryService = null;

        private VsVimHost _host;
        private IVim _vim;

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

            var interopView = _service.GetViewAdapter(view);
            if (interopView == null)
            {
                return;
            }

            var interopLines = _service.GetBufferAdapter(view.TextBuffer) as IVsTextLines;
            if (interopLines == null)
            {
                return;
            }

            // Once we have the view, stop listening to the event
            view.GotAggregateFocus -= new EventHandler(OnGotAggregateFocus);
            CreateVsVimBuffer(view, interopView, interopLines);
        }

        private void EnsureVim(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            if (_vim != null)
            {
                return;
            }

            _host = new VsVimHost(sp, _undoHistoryRegistry);
            _vim = Factory.CreateVim(_host);
        }

        /// <summary>
        /// Called to actually create the VsVimBuffer for the given IWpfTextView
        /// </summary>
        private void CreateVsVimBuffer(IWpfTextView view, IVsTextView interopView, IVsTextLines interopLines)
        {
            EnsureVim(((IObjectWithSite)interopView).GetServiceProvider());
            var opts = _editorOperationsFactoryService.GetEditorOperations(view);
            var buffer = new VsVimBuffer(_vim, view, opts, interopView, interopLines, _undoHistoryRegistry, _editorFormatMapService.GetEditorFormatMap(view));
            view.Properties.AddTypedProperty(buffer);
            ITextViewDebugUtil.Attach(view);

            _keyBindingService.OneTimeCheckForConflictingKeyBindings(_host.DTE, buffer.VimBuffer);
            view.Closed += (x, y) =>
            {
                view.Properties.RemoveTypedProperty<VsVimBuffer>();
                ITextViewDebugUtil.Detach(view);
                buffer.Close();
            };
        }
    }
}
