using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Text.Tagging;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.ExternalEdit
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ExternalEditorManager : IVimBufferCreationListener
    {
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVsAdapter _vsAdapter;
        private readonly List<IExternalEditAdapter> _adapterList = new List<IExternalEditAdapter>();
        private readonly Dictionary<IVimBuffer, ExternalEditMonitor> _monitorMap = new Dictionary<IVimBuffer, ExternalEditMonitor>();

        [ImportingConstructor]
        internal ExternalEditorManager(
            IVsAdapter vsAdapter, 
            [EditorUtilsImport] IProtectedOperations protectedOperations,
            [ImportMany] IEnumerable<IExternalEditAdapter> adapters)
        {
            _vsAdapter = vsAdapter;
            _protectedOperations = protectedOperations;
            _adapterList = adapters.ToList();
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var taggerList = new List<ITagger<ITag>>();
            var bufferAdapterList = new List<IExternalEditAdapter>();
            var textView = vimBuffer.TextView;

            foreach (var adapter in _adapterList)
            {
                ITagger<ITag> tagger;
                if (adapter.IsInterested(textView, out tagger))
                {
                    bufferAdapterList.Add(adapter);
                    if (tagger != null)
                    {
                        taggerList.Add(tagger);
                    }
                }
            }

            if (bufferAdapterList.Count > 0)
            {
                var externalEditMonitor = new ExternalEditMonitor(
                    vimBuffer,
                    _protectedOperations,
                    _vsAdapter.GetTextLines(vimBuffer.TextBuffer),
                    taggerList.ToReadOnlyCollectionShallow(),
                    bufferAdapterList.ToReadOnlyCollectionShallow());
                _monitorMap[vimBuffer] = externalEditMonitor;
                vimBuffer.Closed += delegate 
                { 
                    _monitorMap.Remove(vimBuffer);
                    externalEditMonitor.Close();
                };
            }
        }

        #endregion
    }
}
