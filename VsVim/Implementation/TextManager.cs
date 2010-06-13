using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(ITextManager))]
    internal sealed class TextManager : ITextManager
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly IVsTextManager _textManager;

        public IVsTextManager VsTextManager
        {
            get { return _textManager; }
        }
        
        [ImportingConstructor]
        internal TextManager(
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _textManager = (IVsTextManager)serviceProvider.GetService(typeof(SVsTextManager));
        }

        public Tuple<bool, IWpfTextView> TryGetActiveTextView()
        {
            IVsTextView vsTextView;
            IWpfTextView textView = null;
            if (ErrorHandler.Succeeded(_textManager.GetActiveView(0, null, out vsTextView)) && vsTextView != null)
            {
                textView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);
            }

            return Tuple.Create(textView != null, textView);
        }

        public bool NavigateTo(VirtualSnapshotPoint point)
        {
            var tuple = SnapshotPointUtil.GetLineColumn(point.Position);
            var line = tuple.Item1;
            var column = tuple.Item2;
            var vsBuffer = _editorAdaptersFactoryService.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
            var viewGuid = VSConstants.LOGVIEWID_Code;
            var hr = _textManager.NavigateToLineAndColumn(
                vsBuffer,
                ref viewGuid,
                line,
                column,
                line,
                column);
            return ErrorHandler.Succeeded(hr);
        }
    }
}
