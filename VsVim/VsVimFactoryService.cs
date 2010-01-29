using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Vim;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace VsVim
{
    [Export(typeof(IVsVimFactoryService))]
    internal sealed class VsVimFactoryService : IVsVimFactoryService
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly IVimFactoryService _vimFactoryService;
        private readonly IVimHost _vimHost;
        private readonly Dictionary<ITextView, VsVimBuffer> _map = new Dictionary<ITextView, VsVimBuffer>();

        [ImportingConstructor]
        internal VsVimFactoryService(
            IVimFactoryService vimFactoryService,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IVimHost vimHost
            )
        {
            _adaptersFactory = adaptersFactory;
            _vimFactoryService = vimFactoryService;
            _vimHost = vimHost;
        }

        #region Private

        private void EnsureDte(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            var vimHost = _vimHost as VsVimHost;
            if (vimHost != null && vimHost.DTE == null)
            {
                vimHost.OnServiceProvider(sp);
            }
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
            if (!_map.TryGetValue(view, out buffer))
            {
                return;
            }

            buffer.VsCommandFilter = new VsCommandFilter(buffer.VimBuffer, vsView);
        }

        private VsVimBuffer CreateBuffer(IWpfTextView textView)
        {
            var vsTextLines = _adaptersFactory.GetBufferAdapter(textView.TextBuffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return null;
            }

            var objectWithSite = vsTextLines as IObjectWithSite;
            if (objectWithSite == null)
            {
                return null;
            }

            var sp = objectWithSite.GetServiceProvider();
            EnsureDte(sp);

            var buffer = new VsVimBuffer(
                _vimFactoryService.Vim,
                textView,
                vsTextLines.GetFileName());
            _map.Add(textView, buffer);

            // Have to wait for Aggregate focus before being able to set the VsCommandFilter
            textView.GotAggregateFocus += new EventHandler(OnGotAggregateFocus);
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                _map.Remove(textView);
                ITextViewDebugUtil.Detach(textView);
            };
            ITextViewDebugUtil.Attach(textView);
            return buffer;
        }

        #endregion

        #region IVsVimFactoryService

        public IVimFactoryService VimFactoryService
        {
            get { return _vimFactoryService; }
        }

        public VsVimBuffer GetOrCreateBuffer(IWpfTextView textView)
        {
            VsVimBuffer buffer;
            if (_map.TryGetValue(textView, out buffer))
            {
                return buffer;
            }

            return CreateBuffer(textView);
        }

        #endregion

    }
}
