using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.VisualAssist
{
    // TODO: Clean up the native method code.  Need to use HandleRef and make sure I have IntPtr vs. int correct
    // TODO: Need to coordinate the key handling with IVimBufferCoordinator
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Export(typeof(IVsTextViewCreationListener))]
    [Export(typeof(IVisualAssistUtil))]
    internal sealed class VisualAssistListener : IVsTextViewCreationListener, IVisualAssistUtil
    {
        private static readonly Guid VisualAssistPackageId = new Guid("{44630d46-96b5-488c-8df9-26e21db8c1a3}");

        private readonly ISharedService _sharedService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IVim _vim;
        private readonly bool _isVisualAssistInstalled;
        private bool _subclassedMainWindow;

        /// <summary>
        /// Reference to the key processor must be strongly held.  If it's not held then it will be garbage collected
        /// and take it's PInvoke callback with it.  This will cause the native thunk to be invalid
        /// </summary>
        private VisualAssistKeyProcessor _visualAssistKeyProcessor;

        [ImportingConstructor]
        internal VisualAssistListener(
            SVsServiceProvider serviceProvider,
            IVim vim,
            ISharedServiceFactory sharedServiceFactory,
            IProtectedOperations protectedOperations)
        {
            _vim = vim;
            _sharedService = sharedServiceFactory.Create();
            _protectedOperations = protectedOperations;

            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isVisualAssistInstalled = vsShell.IsPackageInstalled(VisualAssistPackageId);
        }

        private void SubclassMainWindow()
        {
            try
            {
                VisualAssistKeyProcessor.TryCreate(_vim, _sharedService, out _visualAssistKeyProcessor);
            }
            finally
            {
                _subclassedMainWindow = true;
            }
        }

        void IVsTextViewCreationListener.VsTextViewCreated(IVsTextView textViewAdapter)
        {
            if (!_isVisualAssistInstalled || _subclassedMainWindow)
            {
                return;
            }

            _protectedOperations.BeginInvoke(SubclassMainWindow, DispatcherPriority.Background);
        }

        bool IVisualAssistUtil.IsInstalled
        {
            get { return _isVisualAssistInstalled; }
        }
    }
}
