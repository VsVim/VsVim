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
using Microsoft.FSharp.Control;

namespace VsVim
{
    [Export(typeof(IVsVimFactoryService))]
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class VsVimFactoryService : IVsVimFactoryService, IVimBufferCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactory;
        private readonly IVim _vim;
        private readonly IVimHost _vimHost;
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
            }
        }

        [ImportingConstructor]
        internal VsVimFactoryService(
            IVim vim,
            IVsEditorAdaptersFactoryService adaptersFactory,
            IVimHost vimHost
            )
        {
            _vim = vim;
            _adaptersFactory = adaptersFactory;
            _vimHost = vimHost;

        }

        #region Private

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
            var opt = _vim.GetBuffer(view);
            if (!opt.IsSome())
            {
                return;
            }

            var buffer = opt.Value;
            var filter = new VsCommandFilter(buffer, vsView);
            _filterMap.Add(buffer, filter);
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer buffer)
        {
            GetOrUpdateServiceProvider(buffer.TextBuffer);

            // Have to wait for Aggregate focus before being able to set the VsCommandFilter
            var textView = buffer.TextView;
            textView.GotAggregateFocus += new EventHandler(OnGotAggregateFocus);
            textView.Closed += (x, y) =>
            {
                buffer.Close();
                _filterMap.Remove(buffer);
                ITextViewDebugUtil.Detach(textView);
            };
            ITextViewDebugUtil.Attach(textView);
        }

        #endregion

        #endregion

        #region IVsVimFactoryService

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
            return ServiceProvider;
        }

        #endregion

    }
}
