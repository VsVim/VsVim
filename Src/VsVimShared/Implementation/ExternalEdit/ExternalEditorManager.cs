﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text.Tagging;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace Vim.VisualStudio.Implementation.ExternalEdit
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ExternalEditorManager : IVimBufferCreationListener
    {
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVsAdapter _vsAdapter;
        private readonly List<IExternalEditAdapter> _adapterList = new List<IExternalEditAdapter>();
        private readonly Dictionary<IVimBuffer, ExternalEditMonitor> _monitorMap = new Dictionary<IVimBuffer, ExternalEditMonitor>();

        [ImportingConstructor]
        internal ExternalEditorManager(
            IVimApplicationSettings vimApplicationSettings,
            IVsAdapter vsAdapter,
            IProtectedOperations protectedOperations,
            [ImportMany] IEnumerable<IExternalEditAdapter> adapters)
        {
            _vimApplicationSettings = vimApplicationSettings;
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
                if (adapter.IsInterested(textView, out ITagger<ITag> tagger))
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
                    _vimApplicationSettings,
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
