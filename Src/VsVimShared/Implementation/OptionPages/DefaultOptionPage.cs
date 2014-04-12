using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Vim.UI.Wpf;

namespace VsVim.Implementation.OptionPages
{
    public sealed class DefaultOptionPage : DialogPage
    {
        [DisplayName("Default Settings")]
        [Description("Default settings to use when no vimrc file is found")]
        public DefaultSettings DefaultSettings { get; set; }

        [DisplayName("Rename and Snippet Tracking")]
        [Description("Integrate with R# renames, snippet insertion, etc ... Disabling will cause R# integration issues")]
        public bool EnableExternalEditMonitoring { get; set; }

        [DisplayName("Caret color")]
        public System.Drawing.Color CaretColor { get; set; }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);

            var vimApplicationSettings = GetVimApplicationSettings();
            if (vimApplicationSettings != null)
            {
                DefaultSettings = vimApplicationSettings.DefaultSettings;
                EnableExternalEditMonitoring = vimApplicationSettings.EnableExternalEditMonitoring;
            }

            LoadColors();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);

            var vimApplicationSettings = GetVimApplicationSettings();
            if (vimApplicationSettings != null)
            {
                vimApplicationSettings.DefaultSettings = DefaultSettings;
                vimApplicationSettings.EnableExternalEditMonitoring = EnableExternalEditMonitoring;
            }

            SetColors();
        }

        private IVimApplicationSettings GetVimApplicationSettings()
        {
            if (Site == null)
            {
                return null;
            }

            var componentModel = (IComponentModel)(Site.GetService(typeof(SComponentModel)));
            return componentModel.DefaultExportProvider.GetExportedValue<IVimApplicationSettings>();
        }

        private void LoadColors()
        {
            if (Site == null)
            {
                return;
            }

            try
            {
                var guid = Guid.Parse("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}");
                var flags = __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS;
                var vsStorage = (IVsFontAndColorStorage)(Site.GetService(typeof(SVsFontAndColorStorage)));
                ErrorHandler.ThrowOnFailure(vsStorage.OpenCategory(ref guid, (uint)flags));

                var array = new ColorableItemInfo[1];
                ErrorHandler.ThrowOnFailure(vsStorage.GetItem(VimWpfConstants.BlockCaretFormatDefinitionName, array));
                ErrorHandler.ThrowOnFailure(vsStorage.CloseCategory());

                if (array[0].bForegroundValid != 0)
                {
                    CaretColor = FromRGB((int)array[0].crForeground);
                }
            }
            catch (Exception ex)
            {
                VimTrace.TraceError("Unable to save colors: {0}", ex.ToString());
            }
        }

        private void SetColors()
        {
            if (Site == null)
            {
                return;
            }

            try
            {
                var guid = Guid.Parse("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}");
                var flags = __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES;
                var vsStorage = (IVsFontAndColorStorage)(Site.GetService(typeof(SVsFontAndColorStorage)));
                ErrorHandler.ThrowOnFailure(vsStorage.OpenCategory(ref guid, (uint)flags));

                var colorableItemInfo = new ColorableItemInfo();
                colorableItemInfo.bForegroundValid = 1;
                colorableItemInfo.crForeground = (uint)ToRGB(CaretColor);
                ErrorHandler.ThrowOnFailure(vsStorage.SetItem(VimWpfConstants.BlockCaretFormatDefinitionName, new[] { colorableItemInfo }));
                ErrorHandler.ThrowOnFailure(vsStorage.CloseCategory());
            }
            catch (Exception ex)
            {
                VimTrace.TraceError("Unable to save colors: {0}", ex.ToString());
            }
        }

        private static int ToRGB(Color color)
        {
            int i = 0;
            i = i | (color.B << 16);
            i = i | (color.G << 8);
            i = i | color.R;
            return i;
        }

        private static Color FromRGB(int value)
        {
            int b = value >> 16 & 0xFF;
            int g = value >> 8 & 0xFF;
            int r = value & 0xFF;
            return Color.FromArgb(red: r, green: g, blue: b);
        }
    }
}
