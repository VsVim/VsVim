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
using Microsoft.VisualStudio.Text.Classification;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        public const string BlockAdornmentLayer = "BlockCaret";

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockAdornmentLayer)]
        [Order(After = PredefinedAdornmentLayers.Selection)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition _blockAdornmentLayer = null;

        [Import]
        public IVsEditorAdaptersFactoryService _service = null;
        [Import]
        public IUndoHistoryRegistry _undoHistoryRegistry = null;
        [Import]
        public KeyBindingService _keyBindingService = null;
        [Import]
        public IEditorFormatMapService _editorFormatMapService = null;

        private IVim _vim;

        public HostFactory()
        {
            _vim = Factory.CreateVim();
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

            var buffer = new VsVimBuffer(_vim, view, interopView, interopLines, _undoHistoryRegistry, _editorFormatMapService.GetEditorFormatMap(view));
            view.Properties.AddTypedProperty(buffer);

            _keyBindingService.OneTimeCheckForConflictingKeyBindings(buffer.VsVimHost.DTE, buffer.VimBuffer);
            view.Closed += (x, y) =>
            {
                view.Properties.RemoveTypedProperty<VsVimBuffer>();
                buffer.Close();
            };
        }
    }
}
