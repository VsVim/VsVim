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
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim.Implementation.OptionPages
{
    public sealed class DefaultOptionPage : DialogPage
    {
        private const string CategoryGeneral = "General";
        private const string CategoryColors = "Item Colors";
        private static readonly string[] ColorNames = new string[]
        {
            VimConstants.IncrementalSearchTagName,
            VimConstants.HighlightIncrementalSearchTagName,
            VimWpfConstants.BlockCaretFormatDefinitionName,
            VimWpfConstants.CommandMarginFormatDefinitionName,
            VimWpfConstants.ControlCharactersFormatDefinitionName,
            Constants.ToastMarginFormatDefinitionName
        };

        private sealed class ColorInfo
        {
            internal readonly string Name;
            internal readonly bool IsValid;
            internal readonly Color OriginalColor;
            internal Color Color;

            internal ColorInfo(string name, Color color, bool isValid = true)
            {
                Name = name;
                OriginalColor = color;
                Color = color;
                IsValid = isValid;
            }
        }

        private readonly Dictionary<string, ColorInfo> _colorMap = new Dictionary<string, ColorInfo>();

        [DisplayName("Default Settings")]
        [Description("Default settings to use when no vimrc file is found")]
        [Category(CategoryGeneral)]
        public DefaultSettings DefaultSettings { get; set; }

        [DisplayName("Rename and Snippet Tracking")]
        [Description("Integrate with R# renames, snippet insertion, etc ... Disabling will cause R# integration issues")]
        [Category(CategoryGeneral)]
        public bool EnableExternalEditMonitoring { get; set; }

        [DisplayName("Block Caret")]
        [Category(CategoryColors)]
        public Color BlockCaretColor
        {
            get { return GetColor(VimWpfConstants.BlockCaretFormatDefinitionName); }
            set { SetColor(VimWpfConstants.BlockCaretFormatDefinitionName, value); }
        }

        [DisplayName("Incremental Search")]
        [Category(CategoryColors)]
        public Color IncrementalSearchColor
        {
            get { return GetColor(VimConstants.IncrementalSearchTagName); }
            set { SetColor(VimConstants.IncrementalSearchTagName, value); }
        }

        [DisplayName("Highlight Incremental Search")]
        [Category(CategoryColors)]
        public Color HilightIncrementalSearchColor 
        {
            get { return GetColor(VimConstants.HighlightIncrementalSearchTagName); }
            set { SetColor(VimConstants.HighlightIncrementalSearchTagName, value); }
        }

        [DisplayName("Control Characters")]
        [Category(CategoryColors)]
        public Color ControlCharactersColor
        {
            get { return GetColor(VimWpfConstants.ControlCharactersFormatDefinitionName); }
            set { SetColor(VimWpfConstants.ControlCharactersFormatDefinitionName, value); }
        }

        [DisplayName("Command Margin")]
        [Category(CategoryColors)]
        public Color CommandMarginColor 
        {
            get { return GetColor(VimWpfConstants.CommandMarginFormatDefinitionName); }
            set { SetColor(VimWpfConstants.CommandMarginFormatDefinitionName, value); }
        }

        [DisplayName("Toast Margin")]
        [Category(CategoryColors)]
        public Color ToastMarginColor 
        {
            get { return GetColor(Constants.ToastMarginFormatDefinitionName); }
            set { SetColor(Constants.ToastMarginFormatDefinitionName, value); }
        }

        public DefaultOptionPage()
        {
            foreach (var name in ColorNames)
            {
                _colorMap[name] = new ColorInfo(name, Color.Black);
            }
        }

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

            SaveColors();
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

        private Color GetColor(string name)
        {
            return _colorMap[name].Color;
        }

        private void SetColor(string name, Color value)
        {
            _colorMap[name].Color = value;
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
                LoadColorsCore(vsStorage);
                ErrorHandler.ThrowOnFailure(vsStorage.CloseCategory());
            }
            catch (Exception ex)
            {
                VimTrace.TraceError("Unable to load colors: {0}", ex.ToString());
            }
        }

        private void LoadColorsCore(IVsFontAndColorStorage vsStorage)
        {
            foreach (var name in ColorNames)
            {
                ColorInfo colorInfo;
                try
                {
                    var color = LoadColor(vsStorage, name);
                    colorInfo = new ColorInfo(name, color);
                }
                catch (Exception ex)
                {
                    VimTrace.TraceError(ex);
                    colorInfo = new ColorInfo(name, Color.Black, isValid: false);
                }

                _colorMap[name] = colorInfo;
            }
        }

        private void SaveColors()
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

                foreach (var colorInfo in _colorMap.Values)
                {
                    if (colorInfo.OriginalColor == colorInfo.Color)
                    {
                        continue;
                    }

                    SaveColor(vsStorage, colorInfo.Name, colorInfo.Color);
                }

                ErrorHandler.ThrowOnFailure(vsStorage.CloseCategory());
            }
            catch (Exception ex)
            {
                VimTrace.TraceError("Unable to save colors: {0}", ex.ToString());
            }
        }

        private Color LoadColor(IVsFontAndColorStorage vsStorage, string name)
        {
            var array = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(vsStorage.GetItem(name, array));
            if (array[0].bForegroundValid == 0)
            {
                throw new Exception();
            }

            return FromColorRef(vsStorage, array[0].crForeground);
        }

        private static void SaveColor(IVsFontAndColorStorage vsStorage, string name, Color color)
        {
            var colorableItemInfo = new ColorableItemInfo();
            colorableItemInfo.bForegroundValid = 1;
            colorableItemInfo.crForeground = (uint)ColorTranslator.ToWin32(color);
            ErrorHandler.ThrowOnFailure(vsStorage.SetItem(name, new[] { colorableItemInfo }));
        }

        private Color FromColorRef(IVsFontAndColorStorage vsStorage, uint colorValue)
        {
            var vsUtil = (IVsFontAndColorUtilities)vsStorage;
            int type;
            ErrorHandler.ThrowOnFailure(vsUtil.GetColorType(colorValue, out type));
            switch ((__VSCOLORTYPE)type)
            {
                case __VSCOLORTYPE.CT_SYSCOLOR:
                case __VSCOLORTYPE.CT_RAW:
                    return ColorTranslator.FromWin32((int)colorValue);
                case __VSCOLORTYPE.CT_COLORINDEX:
                    {
                        var array = new COLORINDEX[1];
                        ErrorHandler.ThrowOnFailure(vsUtil.GetEncodedIndex(colorValue, array));
                        uint rgb;
                        ErrorHandler.ThrowOnFailure(vsUtil.GetRGBOfIndex(array[0], out rgb));
                        return ColorTranslator.FromWin32((int)rgb);
                    };
                case __VSCOLORTYPE.CT_VSCOLOR:
                    {
                        var vsUIShell = (IVsUIShell2)GetService(typeof(SVsUIShell));
                        int index;
                        ErrorHandler.ThrowOnFailure(vsUtil.GetEncodedVSColor(colorValue, out index));
                        uint rgbValue;
                        ErrorHandler.ThrowOnFailure(vsUIShell.GetVSSysColorEx(index, out rgbValue));
                        return ColorTranslator.FromWin32((int)rgbValue);
                    };
                case __VSCOLORTYPE.CT_AUTOMATIC:
                case __VSCOLORTYPE.CT_TRACK_BACKGROUND:
                case __VSCOLORTYPE.CT_TRACK_FOREGROUND:
                case __VSCOLORTYPE.CT_INVALID:
                    throw new Exception("Invalid color value");
                default:
                    Contract.GetInvalidEnumException((__VSCOLORTYPE)type);
                    return default(Color);
            }
        }
    }
}
