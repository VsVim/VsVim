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
using VsVim.Properties;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    [Export(typeof(IVsVimFactoryService))]
    internal sealed class VsVimFactoryService : IVsVimFactoryService
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly IVimFactoryService _vimFactoryService;
        private readonly IVimHost _vimHost;
        private readonly Dictionary<ITextView, IVimBuffer> _map = new Dictionary<ITextView, IVimBuffer>();
        private readonly Dictionary<IVimBuffer, VsCommandFilter> _filterMap = new Dictionary<IVimBuffer, VsCommandFilter>();

        /// <summary>
        /// Temporary Hack is to calculate IServiceProvider on the fly.  Once we hit RC we can simply 
        /// do a MEF import here
        /// </summary>
        private IServiceProvider _serviceProvider;

        internal IServiceProvider ServiceProvider
        {
            get { return _serviceProvider; }
            set
            {
                _serviceProvider = value;
                MaybeUpdateVimHostServiceProvider();
            }
        }

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

        private void MaybeUpdateVimHostServiceProvider()
        {
            // Update the host as well
            var vimHost = _vimHost as VsVimHost;
            if (vimHost != null && vimHost.DTE == null && ServiceProvider != null)
            {
                vimHost.OnServiceProvider(ServiceProvider);
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

            IVimBuffer buffer;
            if (!_map.TryGetValue(view, out buffer))
            {
                return;
            }

            var filter = new VsCommandFilter(buffer, vsView);
            _filterMap.Add(buffer, filter);
        }

        private IVimBuffer CreateBuffer(IWpfTextView textView)
        {
            GetOrUpdateServiceProvider(textView.TextBuffer);
            var vsTextLines = _adaptersFactory.GetBufferAdapter(textView.TextBuffer) as IVsTextLines;
            var fileName = vsTextLines != null ? vsTextLines.GetFileName() : String.Empty;
            var buffer = _vimFactoryService.Vim.CreateBuffer(textView, fileName);

            // Have to wait for Aggregate focus before being able to set the VsCommandFilter
            textView.GotAggregateFocus += new EventHandler(OnGotAggregateFocus);
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                _map.Remove(textView);
                _filterMap.Remove(buffer);
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

        public IVimBuffer GetOrCreateBuffer(IWpfTextView textView)
        {
            IVimBuffer buffer;
            if (_map.TryGetValue(textView, out buffer))
            {
                return buffer;
            }

            return CreateBuffer(textView);
        }

        public IServiceProvider GetOrUpdateServiceProvider(ITextBuffer buffer)
        {
            if (ServiceProvider != null)
            {
                return ServiceProvider;
            }

            var vsTextLines = _adaptersFactory.GetBufferAdapter(buffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return null;
            }

            var objectWithSite = vsTextLines as IObjectWithSite;
            if (objectWithSite == null)
            {
                return null;
            }

            ServiceProvider = objectWithSite.GetServiceProvider();
            MaybeUpdateVimHostServiceProvider();
            return ServiceProvider;
        }

        #endregion

    }
}
