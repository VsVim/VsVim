using System;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;
using Vim;

namespace VsVim.Implementation.VisualAssist
{
    // TODO: Clean up the native method code.  Need to use HandleRef and make sure I have IntPtr vs. int correct
    // TODO: Need to coordinate the key handling with IVimBufferCoordinator
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Order(Before = Constants.VisualStudioKeyProcessorName, After = Constants.VsKeyProcessorName)]
    [MarginContainer(PredefinedMarginNames.Top)]
    [Export(typeof(IKeyProcessorProvider))]
    [Export(typeof(IVisualAssistUtil))]
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name("VisualAssistKeyProcessor")]
    internal sealed class VisualAssistUtil : IKeyProcessorProvider, IVisualAssistUtil, IWpfTextViewMarginProvider
    {
        private const string RegistryBaseKeyName = @"Software\Whole Tomato\Visual Assist X\";
        private const string RegistryValueName = @"TrackCaretVisibility";

        private static readonly Guid VisualAssistPackageId = new Guid("{44630d46-96b5-488c-8df9-26e21db8c1a3}");

        private readonly IVim _vim;
        private readonly bool _isVisualAssistInstalled;
        private readonly VisualStudioVersion _visualStudioVersion;
        private bool _isRegistryFixed;
        private EventHandler _registryFixCompleted;

        [ImportingConstructor]
        internal VisualAssistUtil(
            SVsServiceProvider serviceProvider,
            IVim vim)
        {
            _vim = vim;

            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isVisualAssistInstalled = vsShell.IsPackageInstalled(VisualAssistPackageId);
            if (_isVisualAssistInstalled)
            {
                var dte = serviceProvider.GetService<SDTE, _DTE>();
                _visualStudioVersion = dte.GetVisualStudioVersion();
                _isRegistryFixed = CheckRegistryKey(_visualStudioVersion);
            }
            else
            {
                // If Visual Assist isn't installed then don't do any extra work
                _isRegistryFixed = true;
                _visualStudioVersion = VisualStudioVersion.Unknown;
            }
        }

        private static string GetRegistryKeyName(VisualStudioVersion version)
        {
            string subKey;
            switch (version)
            {
                case VisualStudioVersion.Dev10:
                    subKey = "VANet10";
                    break;
                case VisualStudioVersion.Dev11:
                    subKey = "VANet11";
                    break;
                case VisualStudioVersion.Unknown:
                default:
                    // Default to the Dev10 version
                    subKey = "VANet10";
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
                // If the registry entry doesn't exist then it's not properly set
                return false;
            }
        }

        private static void FixRegistryKey(VisualStudioVersion version)
        {
            try
            {
                var keyName = GetRegistryKeyName(version);
                using (var key = Registry.CurrentUser.OpenSubKey(keyName, true))
                {
                    var value = new byte[] { 1 };
                    key.SetValue(RegistryValueName, value, RegistryValueKind.Binary);
                }
            }
            catch
            {
                // If the registry can't be accessed don't crash the process
            }
        }

        private void FixRegistry()
        {
            FixRegistryKey(_visualStudioVersion);
            RaiseRegistryFixCompleted();
        }

        private void IgnoreRegistry()
        {
            RaiseRegistryFixCompleted();
        }

        private void RaiseRegistryFixCompleted()
        {
            _isRegistryFixed = true;
            if (_registryFixCompleted != null)
            {
                _registryFixCompleted(this, EventArgs.Empty);
            }
        }

        #region IVisualAssistUtil

        bool IVisualAssistUtil.IsInstalled
        {
            get { return _isVisualAssistInstalled; }
        }

        bool IVisualAssistUtil.IsRegistryFixNeeed
        {
            get { return !_isRegistryFixed; }
        }

        event EventHandler IVisualAssistUtil.RegistryFixCompleted
        {
            add { _registryFixCompleted += value; }
            remove { _registryFixCompleted -= value; }
        }

        void IVisualAssistUtil.FixRegistry()
        {
            FixRegistry();
        }

        void IVisualAssistUtil.IgnoreRegistry()
        {
            IgnoreRegistry();
        }

        #endregion

        #region IKeyProcessorProvider

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (!_isVisualAssistInstalled)
            {
                return null;
            }

            var vimBuffer = _vim.GetOrCreateVimBuffer(wpfTextView);
            return new VisualAssistKeyProcessor(vimBuffer);
        }

        #endregion

        #region IWpfTextViewMargineProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!_isVisualAssistInstalled || _isRegistryFixed)
            {
                return null;
            }

            return new VisualAssistMargin(this);
        }

        #endregion
    }
}
