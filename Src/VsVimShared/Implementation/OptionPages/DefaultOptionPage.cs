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
using System.Globalization;
using System.Collections;

namespace Vim.VisualStudio.Implementation.OptionPages
{
    public sealed class DefaultOptionPage : DialogPage
    {
        #region ColorKey 

        private readonly struct ColorKey
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

        #endregion

        #region ColorInfo

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

        #endregion

        #region EnumMapConverter

        public abstract class EnumMapConverter<T> : EnumConverter
        {
            private readonly Dictionary<T, string> _map;
            private readonly Dictionary<string, T> _reverseMap;

            public EnumMapConverter() : base(typeof(T))
            {
                Contract.Assert(typeof(T).IsEnum);
                _map = CreateMap();
                _reverseMap = _map.ToDictionary(pair => pair.Value, pair => pair.Key);
            }

            public abstract Dictionary<T, string> CreateMap();

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return destinationType == typeof(string);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (!(value is T) || destinationType != typeof(string))
                {
                    return null;
                }

                if (_map.TryGetValue((T)value, out string convertedValue))
                {
                    return convertedValue;
                }

                return null;
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                var str = value as string;
                if (_reverseMap.TryGetValue(str, out T convertedValue))
                {
                    return convertedValue;
                }

                return null;
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(_map.Keys);
            }
        }

        #endregion

        #region VimRcLoadSettingConverter

        public sealed class VimRcLoadSettingConverter : EnumMapConverter<VimRcLoadSetting>
        {
            public override Dictionary<VimRcLoadSetting, string> CreateMap()
            {
                var map = new Dictionary<VimRcLoadSetting, string>
                {
                    { VimRcLoadSetting.None, "No vsvimrc or vimrc files" },
                    { VimRcLoadSetting.VsVimRc, "vsvimrc files only" },
                    { VimRcLoadSetting.VimRc, "vimrc files only" },
                    { VimRcLoadSetting.Both, "vsvimrc or vimrc files" }
                };
                return map;
            }
        }

        #endregion

        #region WordWrapDisplaySettingConverter

        public sealed class WordWrapDisplaySettingConverter : EnumMapConverter<WordWrapDisplay>
        {
            public override Dictionary<WordWrapDisplay, string> CreateMap()
            {
                var map = new Dictionary<WordWrapDisplay, string>
                {
                    { WordWrapDisplay.AutoIndent, "AutoIndent" },
                    { WordWrapDisplay.Glyph, "Glyph" },
                    { WordWrapDisplay.All, "AutoIndent + Glyph" }
                };
                return map;
            }
        }

        #endregion

        private const string CategoryGeneral = "General";
        private const string CategoryColors = "Item Colors";
        private const string CategoryEditing = "Vim Edit Behavior";
        private const string CategoryVsVim = "VsVim";

        private static readonly ColorKey s_incrementalSearchColorKey = ColorKey.Background(VimConstants.IncrementalSearchTagName);
        private static readonly ColorKey s_highlightIncrementalSearchColorKey = ColorKey.Background(VimConstants.HighlightIncrementalSearchTagName);
        private static readonly ColorKey s_blockCaretForegroundColorKey = ColorKey.Foreground(VimWpfConstants.BlockCaretFormatDefinitionName);
        private static readonly ColorKey s_blockCaretBackgroundColorKey = ColorKey.Background(VimWpfConstants.BlockCaretFormatDefinitionName);
        private static readonly ColorKey s_primaryCaretForegroundColorKey = ColorKey.Foreground(VimWpfConstants.PrimaryCaretFormatDefinitionName);
        private static readonly ColorKey s_primaryCaretBackgroundColorKey = ColorKey.Background(VimWpfConstants.PrimaryCaretFormatDefinitionName);
        private static readonly ColorKey s_secondaryCaretForegroundColorKey = ColorKey.Foreground(VimWpfConstants.SecondaryCaretFormatDefinitionName);
        private static readonly ColorKey s_secondaryCaretBackgroundColorKey = ColorKey.Background(VimWpfConstants.SecondaryCaretFormatDefinitionName);
        private static readonly ColorKey s_controlCharacterColorKey = ColorKey.Foreground(VimWpfConstants.ControlCharactersFormatDefinitionName);
        private static readonly ColorKey s_commandMarginForegroundColorKey = ColorKey.Foreground(VimWpfConstants.CommandMarginFormatDefinitionName);
        private static readonly ColorKey s_commandMarginBackgroundColorKey = ColorKey.Background(VimWpfConstants.CommandMarginFormatDefinitionName);

        private static readonly ReadOnlyCollection<ColorKey> s_colorKeyList;

        static DefaultOptionPage()
        {
            s_colorKeyList = new ReadOnlyCollection<ColorKey>(new[]
            {
                s_incrementalSearchColorKey,
                s_highlightIncrementalSearchColorKey,
                s_blockCaretForegroundColorKey,
                s_blockCaretBackgroundColorKey,
                s_primaryCaretForegroundColorKey,
                s_primaryCaretBackgroundColorKey,
                s_secondaryCaretForegroundColorKey,
                s_secondaryCaretBackgroundColorKey,
                s_controlCharacterColorKey,
                s_commandMarginForegroundColorKey,
                s_commandMarginBackgroundColorKey,
            });
        }

        private readonly Dictionary<ColorKey, ColorInfo> _colorMap = new Dictionary<ColorKey, ColorInfo>();

        [DisplayName("Default Settings")]
        [Description("Default settings to use when no vimrc file is found")]
        [Category(CategoryGeneral)]
        public DefaultSettings DefaultSettings { get; set; }

        [DisplayName("Use Editor Command Margin")]
        [Description("Use editor command margin for mode line instead of the status bar")]
        [Category(CategoryGeneral)]
        public bool UseEditorCommandMargin { get; set; }

        [DisplayName("Display Control Characters")]
        [Description("Whether or not control characters will display as they do in gVim.  For example should (char)29 display as an invisible character or ^]")]
        [Category(CategoryGeneral)]
        public bool DisplayControlCharacters { get; set; }

        [DisplayName("Rename and Snippet Tracking")]
        [Description("Integrate with R# renames, snippet insertion, etc ... Disabling will cause R# integration issues")]
        [Category(CategoryGeneral)]
        public bool EnableExternalEditMonitoring { get; set; }

        [DisplayName("Output Window")]
        [Description("Whether or not to use output window to display trace data.")]
        [Category(CategoryGeneral)]
        public bool EnableOutputWindow { get; set; }

        [DisplayName("Hide Marks")]
        [Description("Marks which should be hidden in the margin")]
        [Category(CategoryGeneral)]
        public string HideMarks { get; set; }

        [DisplayName("Use Visual Studio Tab / Backspace")]
        [Description("Let Visual Studio control tab and backspace in insert mode.  This will cause VsVim to ignore settings like 'softtabstop', 'tabstop', 'backspace', etc ...")]
        [Category(CategoryEditing)]
        public bool UseEditorTabAndBackspace { get; set; }

        [DisplayName("Use Visual Studio Indent")]
        [Description("Let Visual Studio control indentation for new lines instead of strict 'autoindent' rules")]
        [Category(CategoryEditing)]
        public bool UseEditorIndent { get; set; }

        [DisplayName("Use Visual Studio Settings")]
        [Description("Use Visual Studio values to initialize 'tabsize', 'expandtab', 'cursorline', etc ...  This will override values specified in a vsvimrc file")]
        [Category(CategoryEditing)]
        public bool UseEditorDefaults { get; set; }

        [DisplayName("Clean Macro Recording")]
        [Description("During macro recording disable features that would interfere with it: intellisense, brace completion, etc ...")]
        [Category(CategoryEditing)]
        public bool CleanMacros { get; set; }

        [DisplayName("Report Clipboard Errors")]
        [Description("Prevent VsVim from showing an error dialog for exceptions that occur when accessing the clipboard")]
        [Category(CategoryEditing)]
        public bool ReportClipboardErrors { get; set; }

        [DisplayName("VimRc File Loading")]
        [Description("Controls how VsVim probes for vsvimrc / vimrc files")]
        [Category(CategoryGeneral)]
        [TypeConverter(typeof(VimRcLoadSettingConverter))]
        public VimRcLoadSetting VimRcLoadSetting { get; set; }

        [DisplayName("VimRc Error Reporting")]
        [Description("Display errors when loading a vsvimrc / vimrc file")]
        [Category(CategoryGeneral)]
        public bool DisplayVimRcLoadErrors { get; set; }

        [DisplayName("Word Wrap Display")]
        [Description("Controls how word wrap is displayed")]
        [Category(CategoryGeneral)]
        [TypeConverter(typeof(WordWrapDisplaySettingConverter))]
        public WordWrapDisplay WordWrapDisplay { get; set; }

        [DisplayName("Block Caret Foreground")]
        [Category(CategoryColors)]
        public Color BlockCaretForegroundColor
        {
            get { return GetColor(s_blockCaretForegroundColorKey); }
            set { SetColor(s_blockCaretForegroundColorKey, value); }
        }

        [DisplayName("Block Caret Background")]
        [Category(CategoryColors)]
        public Color BlockCaretBackgroundColor
        {
            get { return GetColor(s_blockCaretBackgroundColorKey); }
            set { SetColor(s_blockCaretBackgroundColorKey, value); }
        }

        [DisplayName("Primary Caret Foreground")]
        [Category(CategoryColors)]
        public Color PrimaryCaretForegroundColor
        {
            get { return GetColor(s_primaryCaretForegroundColorKey); }
            set { SetColor(s_primaryCaretForegroundColorKey, value); }
        }

        [DisplayName("Primary Caret Background")]
        [Category(CategoryColors)]
        public Color PrimaryCaretBackgroundColor
        {
            get { return GetColor(s_primaryCaretBackgroundColorKey); }
            set { SetColor(s_primaryCaretBackgroundColorKey, value); }
        }

        [DisplayName("Secondary Caret Foreground")]
        [Category(CategoryColors)]
        public Color SecondaryCaretForegroundColor
        {
            get { return GetColor(s_secondaryCaretForegroundColorKey); }
            set { SetColor(s_secondaryCaretForegroundColorKey, value); }
        }

        [DisplayName("Secondary Caret Background")]
        [Category(CategoryColors)]
        public Color SecondaryCaretBackgroundColor
        {
            get { return GetColor(s_secondaryCaretBackgroundColorKey); }
            set { SetColor(s_secondaryCaretBackgroundColorKey, value); }
        }

        [DisplayName("Incremental Search")]
        [Category(CategoryColors)]
        public Color IncrementalSearchColor
        {
            get { return GetColor(s_incrementalSearchColorKey); }
            set { SetColor(s_incrementalSearchColorKey, value); }
        }

        [DisplayName("Highlight Incremental Search")]
        [Category(CategoryColors)]
        public Color HilightIncrementalSearchColor
        {
            get { return GetColor(s_highlightIncrementalSearchColorKey); }
            set { SetColor(s_highlightIncrementalSearchColorKey, value); }
        }

        [DisplayName("Control Characters")]
        [Category(CategoryColors)]
        public Color ControlCharacterColor
        {
            get { return GetColor(s_controlCharacterColorKey); }
            set { SetColor(s_controlCharacterColorKey, value); }
        }

        [DisplayName("Command Margin Foreground Color")]
        [Category(CategoryColors)]
        public Color CommandMarginForegroundColor
        {
            get { return GetColor(s_commandMarginForegroundColorKey); }
            set { SetColor(s_commandMarginForegroundColorKey, value); }
        }
        [DisplayName("Command Margin Background Color")]
        [Category(CategoryColors)]
        public Color CommandMarginBackgroundColor
        {
            get { return GetColor(s_commandMarginBackgroundColorKey); }
            set { SetColor(s_commandMarginBackgroundColorKey, value); }
        }

        [DisplayName("Disable VsVim")]
        [Description("Disable VsVim temporarily and let Visual Studio handle keystrokes and mouse events.  By default it can also be toggled using Ctrl+Shift+F12")]
        [Category(CategoryVsVim)]
        public bool DisableVsVim { get; set; }

        public DefaultOptionPage()
        {
            foreach (var colorKey in s_colorKeyList)
            {
                _colorMap[colorKey] = new ColorInfo(colorKey, Color.Black);
            }
        }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);

            var vim = GetVim();
            if (vim != null)
            {
                DisableVsVim = vim.IsDisabled;
            }

            var vimApplicationSettings = GetVimApplicationSettings();
            if (vimApplicationSettings != null)
            {
                DefaultSettings = vimApplicationSettings.DefaultSettings;
                EnableExternalEditMonitoring = vimApplicationSettings.EnableExternalEditMonitoring;
                EnableOutputWindow = vimApplicationSettings.EnableOutputWindow;
                HideMarks = vimApplicationSettings.HideMarks;
                UseEditorDefaults = vimApplicationSettings.UseEditorDefaults;
                UseEditorIndent = vimApplicationSettings.UseEditorIndent;
                UseEditorTabAndBackspace = vimApplicationSettings.UseEditorTabAndBackspace;
                UseEditorCommandMargin = vimApplicationSettings.UseEditorCommandMargin;
                CleanMacros = vimApplicationSettings.CleanMacros;
                ReportClipboardErrors = vimApplicationSettings.ReportClipboardErrors;
                VimRcLoadSetting = vimApplicationSettings.VimRcLoadSetting;
                DisplayControlCharacters = vimApplicationSettings.DisplayControlChars;
                DisplayVimRcLoadErrors = !vimApplicationSettings.HaveNotifiedVimRcErrors;
                WordWrapDisplay = vimApplicationSettings.WordWrapDisplay;
            }

            LoadColors();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);

            var vim = GetVim();
            if (vim != null)
            {
                vim.IsDisabled = DisableVsVim;
            }

            var vimApplicationSettings = GetVimApplicationSettings();
            if (vimApplicationSettings != null)
            {
                vimApplicationSettings.DefaultSettings = DefaultSettings;
                vimApplicationSettings.EnableExternalEditMonitoring = EnableExternalEditMonitoring;
                vimApplicationSettings.EnableOutputWindow = EnableOutputWindow;
                vimApplicationSettings.HideMarks = HideMarks;
                vimApplicationSettings.UseEditorDefaults = UseEditorDefaults;
                vimApplicationSettings.UseEditorIndent = UseEditorIndent;
                vimApplicationSettings.UseEditorTabAndBackspace = UseEditorTabAndBackspace;
                vimApplicationSettings.UseEditorCommandMargin = UseEditorCommandMargin;
                vimApplicationSettings.CleanMacros = CleanMacros;
                vimApplicationSettings.ReportClipboardErrors = ReportClipboardErrors;
                vimApplicationSettings.VimRcLoadSetting = VimRcLoadSetting;
                vimApplicationSettings.DisplayControlChars = DisplayControlCharacters;
                vimApplicationSettings.HaveNotifiedVimRcErrors = !DisplayVimRcLoadErrors;
                vimApplicationSettings.WordWrapDisplay = WordWrapDisplay;
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

        private IVim GetVim()
        {
            if (Site == null)
            {
                return null;
            }

            var componentModel = (IComponentModel)(Site.GetService(typeof(SComponentModel)));
            return componentModel.DefaultExportProvider.GetExportedValue<IVim>();
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
            foreach (var colorKey in s_colorKeyList)
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

            var isValid = colorKey.IsForeground
                ? array[0].bForegroundValid
                : array[0].bBackgroundValid;
            if (isValid == 0)
            {
                throw new Exception();
            }

            var colorRef = colorKey.IsForeground
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
            ErrorHandler.ThrowOnFailure(vsUtil.GetColorType(colorValue, out int type));
            switch ((__VSCOLORTYPE)type)
            {
                case __VSCOLORTYPE.CT_SYSCOLOR:
                case __VSCOLORTYPE.CT_RAW:
                    return ColorTranslator.FromWin32((int)colorValue);
                case __VSCOLORTYPE.CT_COLORINDEX:
                    {
                        var array = new COLORINDEX[1];
                        ErrorHandler.ThrowOnFailure(vsUtil.GetEncodedIndex(colorValue, array));
                        ErrorHandler.ThrowOnFailure(vsUtil.GetRGBOfIndex(array[0], out uint rgb));
                        return ColorTranslator.FromWin32((int)rgb);
                    };
                case __VSCOLORTYPE.CT_VSCOLOR:
                    {
                        var vsUIShell = (IVsUIShell2)GetService(typeof(SVsUIShell));
                        ErrorHandler.ThrowOnFailure(vsUtil.GetEncodedVSColor(colorValue, out int index));
                        ErrorHandler.ThrowOnFailure(vsUIShell.GetVSSysColorEx(index, out uint rgbValue));
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
                    return default;
            }
        }
    }
}
