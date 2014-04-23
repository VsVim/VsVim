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
using System.Collections.ObjectModel;

namespace VsVim.Implementation.OptionPages
{
    public sealed class DefaultOptionPage : DialogPage
    {
        private struct ColorKey
        {
            internal readonly string Name;
            internal readonly bool IsForeground;

            internal ColorKey(string name, bool isForeground)
            {
                Name = name;
                IsForeground = isForeground;
            }

            internal static ColorKey Foreground(string name)
            {
                return new ColorKey(name, isForeground: true);
            }

            internal static ColorKey Background(string name)
            {
                return new ColorKey(name, isForeground: false);
            }
        }

        private sealed class ColorInfo
        {
            internal readonly ColorKey ColorKey;
            internal readonly Color OriginalColor;
            internal bool IsValid;
            internal Color Color;

            internal ColorInfo(ColorKey colorKey, Color color, bool isValid = true)
            {
                ColorKey = colorKey;
                OriginalColor = color;
                Color = color;
                IsValid = isValid;
            }
        }

        private const string CategoryGeneral = "General";
        private const string CategoryColors = "Item Colors";

        private static readonly ColorKey IncrementalSearchColorKey = ColorKey.Background(VimConstants.IncrementalSearchTagName);
        private static readonly ColorKey HighlightIncrementalSearchColorKey = ColorKey.Background(VimConstants.HighlightIncrementalSearchTagName);
        private static readonly ColorKey BlockCaretColorKey = ColorKey.Foreground(VimWpfConstants.BlockCaretFormatDefinitionName);
        private static readonly ColorKey ControlCharacterColorKey = ColorKey.Foreground(VimWpfConstants.ControlCharactersFormatDefinitionName);

        private static readonly ReadOnlyCollection<ColorKey> ColorKeyList;

        static DefaultOptionPage()
        {
            ColorKeyList = new ReadOnlyCollection<ColorKey>(new[]
            {
                IncrementalSearchColorKey,
                HighlightIncrementalSearchColorKey,
                BlockCaretColorKey,
                ControlCharacterColorKey,
            });
        }

        private readonly Dictionary<ColorKey, ColorInfo> _colorMap = new Dictionary<ColorKey, ColorInfo>();

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
            get { return GetColor(BlockCaretColorKey); }
            set { SetColor(BlockCaretColorKey, value); }
        }

        [DisplayName("Incremental Search")]
        [Category(CategoryColors)]
        public Color IncrementalSearchColor
        {
            get { return GetColor(IncrementalSearchColorKey); }
            set { SetColor(IncrementalSearchColorKey, value); }
        }

        [DisplayName("Highlight Incremental Search")]
        [Category(CategoryColors)]
        public Color HilightIncrementalSearchColor 
        {
            get { return GetColor(HighlightIncrementalSearchColorKey); }
            set { SetColor(HighlightIncrementalSearchColorKey, value); }
        }

        [DisplayName("Control Characters")]
        [Category(CategoryColors)]
        public Color ControlCharacterColor
        {
            get { return GetColor(ControlCharacterColorKey); }
            set { SetColor(ControlCharacterColorKey, value); }
        }

        public DefaultOptionPage()
        {
            foreach (var colorKey in ColorKeyList)
            {
                _colorMap[colorKey] = new ColorInfo(colorKey, Color.Black);
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

        private Color GetColor(ColorKey colorKey)
        {
            return _colorMap[colorKey].Color;
        }

        private void SetColor(ColorKey colorKey, Color value)
        {
            _colorMap[colorKey].Color = value;
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
                var flags = __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS;
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
            foreach (var colorKey in ColorKeyList)
            {
                ColorInfo colorInfo;
                try
                {
                    var color = LoadColor(vsStorage, colorKey);
                    colorInfo = new ColorInfo(colorKey, color);
                }
                catch (Exception ex)
                {
                    VimTrace.TraceError(ex);
                    colorInfo = new ColorInfo(colorKey, Color.Black, isValid: false);
                }

                _colorMap[colorKey] = colorInfo;
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

                    SaveColor(vsStorage, colorInfo.ColorKey, colorInfo.Color);
                }

                ErrorHandler.ThrowOnFailure(vsStorage.CloseCategory());
            }
            catch (Exception ex)
            {
                VimTrace.TraceError("Unable to save colors: {0}", ex.ToString());
            }
        }

        private Color LoadColor(IVsFontAndColorStorage vsStorage, ColorKey colorKey)
        {
            var array = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(vsStorage.GetItem(colorKey.Name, array));

            int isValid = colorKey.IsForeground
                ? array[0].bForegroundValid
                : array[0].bBackgroundValid;
            if (isValid == 0)
            {
                throw new Exception();
            }

            uint colorRef = colorKey.IsForeground
                ? array[0].crForeground
                : array[0].crBackground;
            return FromColorRef(vsStorage, colorRef);
        }

        private static void SaveColor(IVsFontAndColorStorage vsStorage, ColorKey colorKey, Color color)
        {
            var colorableItemInfo = new ColorableItemInfo();
            if (colorKey.IsForeground)
            {
                colorableItemInfo.bForegroundValid = 1;
                colorableItemInfo.crForeground = (uint)ColorTranslator.ToWin32(color);
            }
            else
            {
                colorableItemInfo.bBackgroundValid = 1;
                colorableItemInfo.crBackground = (uint)ColorTranslator.ToWin32(color);
            }

            ErrorHandler.ThrowOnFailure(vsStorage.SetItem(colorKey.Name, new[] { colorableItemInfo }));
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
                    // These values should never show up because we passed the FCSF_NOAUTOCOLORS flag.  Everything
                    // should be CT_RAW / CT_SYSCOLOR
                    throw new Exception("Invalid color value");
                default:
                    Contract.GetInvalidEnumException((__VSCOLORTYPE)type);
                    return default(Color);
            }
        }
    }
}
