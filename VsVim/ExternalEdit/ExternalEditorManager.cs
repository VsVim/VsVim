using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Tagging;
using Vim;

namespace VsVim.ExternalEdit
{
    [Export(typeof(IExternalEditorManager))]
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ExternalEditorManager : IExternalEditorManager, IVimBufferCreationListener
    {
        private static readonly Guid Resharper5Guid = new Guid("0C6E6407-13FC-4878-869A-C8B4016C57FE");

        private readonly IVsAdapter _vsAdapter;
        private readonly IVsShell _vsShell;
        private readonly List<IExternalEditAdapter> _adapterList = new List<IExternalEditAdapter>();
        private readonly Dictionary<IVimBuffer, ExternalEditMonitor> _monitorMap = new Dictionary<IVimBuffer, ExternalEditMonitor>();
        private readonly IViewTagAggregatorFactoryService _viewTagAggregatorFactoryService;
        private readonly bool _isResharperInstalled;
        private bool _isResharperLoaded;

        public bool IsResharperInstalled
        {
            get { return _isResharperInstalled; }
        }

        public bool IsResharperLoaded
        {
            get
            {
                if (!_isResharperInstalled)
                {
                    return false;
                }
                else if(_isResharperLoaded)
                {
                    return true;
                }
                else
                {
                    var guid = Resharper5Guid;
                    IVsPackage package;
                    _isResharperLoaded = ErrorHandler.Succeeded(_vsShell.IsPackageLoaded(ref guid, out package)) &&
                                         package != null;
                    return _isResharperLoaded;
                }
            }
        }

        [ImportingConstructor]
        internal ExternalEditorManager(
            SVsServiceProvider serviceProvider,
            IVsAdapter vsAdapter,
            IViewTagAggregatorFactoryService viewTagAggregatorFactoryService)
        {
            _vsAdapter = vsAdapter;
            _viewTagAggregatorFactoryService = viewTagAggregatorFactoryService;
            _vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _adapterList.Add(new SnippetExternalEditAdapter());
            _isResharperInstalled = CheckResharperInstalled();
            if (_isResharperInstalled)
            {
                _adapterList.Add(new ResharperExternalEditAdapter());
            }
        }

        public void VimBufferCreated(IVimBuffer value)
        {
            _monitorMap[value] = new ExternalEditMonitor(
                value,
                _vsAdapter.GetTextLines(value.TextBuffer),
                new ReadOnlyCollection<IExternalEditAdapter>(_adapterList),
                _viewTagAggregatorFactoryService.CreateTagAggregator<ITag>(value.TextView));
            value.Closed += delegate { _monitorMap.Remove(value); };
        }

        private bool CheckResharperInstalled()
        {
            var guid = Resharper5Guid;
            int isInstalled;
            return ErrorHandler.Succeeded(_vsShell.IsPackageInstalled(ref guid, out isInstalled)) && 1 == isInstalled;
        }
    }
}
