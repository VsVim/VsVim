using System;
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

namespace VsVim.Implementation.VisualAssist
{
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
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
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly VisualStudioVersion _visualStudioVersion;
        private bool _isRegistryFixedNeeded;
        private EventHandler _isRegistryFixNeededChanged;

        [ImportingConstructor]
        internal VisualAssistUtil(
            SVsServiceProvider serviceProvider,
            IVim vim,
            IEditorFormatMapService editorFormatMapService)
        {
            _vim = vim;
            _editorFormatMapService = editorFormatMapService;

            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isVisualAssistInstalled = vsShell.IsPackageInstalled(VisualAssistPackageId);
            if (_isVisualAssistInstalled)
            {
                var dte = serviceProvider.GetService<SDTE, _DTE>();
                _visualStudioVersion = dte.GetVisualStudioVersion();
                _isRegistryFixedNeeded = !CheckRegistryKey(_visualStudioVersion);
            }
            else
            {
                // If Visual Assist isn't installed then don't do any extra work
                _isRegistryFixedNeeded = false;
                _visualStudioVersion = VisualStudioVersion.Unknown;
            }
        }

        private static string GetRegistryKeyName(VisualStudioVersion version)
        {
            string subKey;
            switch (version)
            {
                case VisualStudioVersion.Vs2010:
                    subKey = "VANet10";
                    break;
                case VisualStudioVersion.Vs2012:
                    subKey = "VANet11";
                    break;
                case VisualStudioVersion.Unknown:
                default:
                    // Default to the Vs2010 version
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
                // If the registry entry doesn't exist then it's properly set
                return true;
            }
        }

        private void RaiseIsRegistryFixNeededChanged()
        {
            if (_isRegistryFixNeededChanged != null)
            {
                _isRegistryFixNeededChanged(this, EventArgs.Empty);
            }
        }

        #region IVisualAssistUtil

        bool IVisualAssistUtil.IsInstalled
        {
            get { return _isVisualAssistInstalled; }
        }

        bool IVisualAssistUtil.IsRegistryFixNeeed
        {
            get { return _isRegistryFixedNeeded; }
            set
            {
                var changed = _isRegistryFixedNeeded != value;
                _isRegistryFixedNeeded = value;
                if (changed)
                {
                    RaiseIsRegistryFixNeededChanged();
                }
            }
        }

        event EventHandler IVisualAssistUtil.IsRegistryFixNeededChanged
        {
            add { _isRegistryFixNeededChanged += value; }
            remove { _isRegistryFixNeededChanged -= value; }
        }

        #endregion

        #region IKeyProcessorProvider

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (!_isVisualAssistInstalled)
            {
                return null;
            }

            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            return new VisualAssistKeyProcessor(vimBuffer);
        }

        #endregion

        #region IWpfTextViewMargineProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!_isVisualAssistInstalled || !_isRegistryFixedNeeded)
            {
                return null;
            }

            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextViewHost.TextView);
            return new VisualAssistMargin(this, editorFormatMap);
        }

        #endregion
    }
}
