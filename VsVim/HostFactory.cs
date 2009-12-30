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
        [Import]
        private ICompletionWindowBroker _completionBroker = null;
        [Import]
        private IVsEditorAdaptersFactoryService _adaptersFactory = null;

        private VsVimHost _host;
        private IVim _vim;

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
            EnsureVim(sp);

            var opts = _editorOperationsFactoryService.GetEditorOperations(textView);
            var map = _editorFormatMapService.GetEditorFormatMap(textView);
            var buffer = new VsVimBuffer(
                _vim,
                textView,
                opts,
                vsTextLines.GetFileName(),
                _undoHistoryRegistry,
                map);
            textView.SetVimBuffer(buffer);

            // Run the key binding check now
            _keyBindingService.OneTimeCheckForConflictingKeyBindings(_host.DTE, buffer.VimBuffer);

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

            var vsView = _service.GetViewAdapter(view);
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

        private void EnsureVim(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            if (_vim != null)
            {
                return;
            }

            _host = new VsVimHost(sp, _undoHistoryRegistry, _completionBroker);
            _vim = Factory.CreateVim(_host);
        }
    }
}
