using System;
using System.Linq;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;
using Vim;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.VisualAssist
{
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Order(Before = VsVimConstants.VisualStudioKeyProcessorName, After = VimConstants.MainKeyProcessorName)]
    [MarginContainer(PredefinedMarginNames.Top)]
    [Export(typeof(IKeyProcessorProvider))]
    [Export(typeof(IVisualAssistUtil))]
    [Export(typeof(IVimBufferCreationListener))]
    [Name("VisualAssistKeyProcessor")]
    internal sealed class VisualAssistUtil : IKeyProcessorProvider, IVisualAssistUtil, IVimBufferCreationListener
    {
        private const string RegistryBaseKeyName = @"Software\Whole Tomato\Visual Assist X\";
        private const string RegistryValueName = @"TrackCaretVisibility";

        private static readonly Guid s_visualAssistPackageId = new Guid("{44630d46-96b5-488c-8df9-26e21db8c1a3}");

        private readonly IVim _vim;
        private readonly bool _isVisualAssistInstalled;
        private readonly object _toastKey = new object();
        private readonly IToastNotificationServiceProvider _toastNotificationServiceProvider;
        private readonly bool _isRegistryFixedNeeded;

        [ImportingConstructor]
        internal VisualAssistUtil(
            SVsServiceProvider serviceProvider,
            IVim vim,
            IToastNotificationServiceProvider toastNotificationServiceProvider)
        {
            _vim = vim;
            _toastNotificationServiceProvider = toastNotificationServiceProvider;

            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isVisualAssistInstalled = vsShell.IsPackageInstalled(s_visualAssistPackageId);
            if (_isVisualAssistInstalled)
            {
                var dte = serviceProvider.GetService<SDTE, _DTE>();
                _isRegistryFixedNeeded = !CheckRegistryKey(VsVimHost.VisualStudioVersion);
            }
            else
            {
                // If Visual Assist isn't installed then don't do any extra work
                _isRegistryFixedNeeded = false;
            }
        }

        private static string GetRegistryKeyName(VisualStudioVersion version)
        {
            string subKey;
            switch (version)
            {
                // Visual Assist settings stored in registry using the Visual Studio major version number
                case VisualStudioVersion.Vs2015:
                    subKey = "VANet14";
                    break;
                case VisualStudioVersion.Vs2017:
                    subKey = "VANet15";
                    break;
                case VisualStudioVersion.Vs2019:
                    subKey = "VANet16";
                    break;
                default:
                    // Default to the latest version
                    subKey = "VANet16";
                    break;
            }

            return RegistryBaseKeyName + subKey;
        }

        private static bool CheckRegistryKey(VisualStudioVersion version)
        {
            try
            {
                var keyName = GetRegistryKeyName(version);
                using (var key = Registry.CurrentUser.OpenSubKey(keyName))
                {
                    var value = (byte[])key.GetValue(RegistryValueName);
                    return value.Length > 0 && value[0] != 0;
                }
            }
            catch
            {
                // If the registry entry doesn't exist then it's properly set
                return true;
            }
        }

        /// <summary>
        /// When any of the toast notifications are closed then close them all.  No reason to make
        /// the developer dismiss it on every single display that is open 
        /// </summary>
        private void OnToastNotificationClosed()
        {
            _vim.VimBuffers
                .Select(x => x.TextView)
                .OfType<IWpfTextView>()
                .ForEach(x => _toastNotificationServiceProvider.GetToastNoficationService(x).Remove(_toastKey));
        }

        #region IVisualAssistUtil

        bool IVisualAssistUtil.IsInstalled
        {
            get { return _isVisualAssistInstalled; }
        }

        #endregion

        #region IKeyProcessorProvider

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (!_isVisualAssistInstalled)
            {
                return null;
            }

            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out IVimBuffer vimBuffer))
            {
                return null;
            }

            return new VisualAssistKeyProcessor(vimBuffer);
        }

        #endregion

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (!_isVisualAssistInstalled || !_isRegistryFixedNeeded)
            {
                return;
            }

            var wpfTextView = vimBuffer.TextView as IWpfTextView;
            if (wpfTextView == null)
            {
                return;
            }

            var toastNotificationService = _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView);
            toastNotificationService.Display(_toastKey, new VisualAssistMargin(), OnToastNotificationClosed);
        }

        #endregion
    }
}
