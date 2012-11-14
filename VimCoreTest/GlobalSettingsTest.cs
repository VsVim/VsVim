using System;
using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class GlobalSettingsTest 
    {
        private readonly IVimGlobalSettings _globalSettings;

        public GlobalSettingsTest()
        {
            _globalSettings = new GlobalSettings();
        }

        [Fact]
        public void Sanity1()
        {
            var all = _globalSettings.AllSettings;
            Assert.True(all.Any(x => x.Name == GlobalSettingNames.IgnoreCaseName));
            Assert.True(all.Any(x => x.Name == GlobalSettingNames.ScrollOffsetName));
        }

        [Fact]
        public void IsVirtualEditOneMore1()
        {
            _globalSettings.VirtualEdit = String.Empty;
            Assert.False(_globalSettings.IsVirtualEditOneMore);
        }

        [Fact]
        public void IsVirtualEditOneMore2()
        {
            _globalSettings.VirtualEdit = "onemore";
            Assert.True(_globalSettings.IsVirtualEditOneMore);
        }

        [Fact]
        public void IsVirtualEditOneMore3()
        {
            _globalSettings.VirtualEdit = "onemore,blah";
            Assert.True(_globalSettings.IsVirtualEditOneMore);
        }

        /// <summary>
        /// Ensure the IsBackspaceStart properly parsers start from the option
        /// </summary>
        [Fact]
        public void IsBackspaceStart()
        {
            Assert.False(_globalSettings.IsBackspaceStart);
            _globalSettings.Backspace = "eol,start";
            Assert.True(_globalSettings.IsBackspaceStart);
        }

        /// <summary>
        /// Ensure the IsBackspaceEol properly parsers eol from the option
        /// </summary>
        [Fact]
        public void IsBackspaceEol()
        {
            Assert.False(_globalSettings.IsBackspaceEol);
            _globalSettings.Backspace = "eol,Eol";
            Assert.True(_globalSettings.IsBackspaceEol);
        }

        /// <summary>
        /// Ensure the IsBackspaceIndent properly parsers start from the option
        /// </summary>
        [Fact]
        public void IsBackspaceIndent()
        {
            Assert.False(_globalSettings.IsBackspaceIndent);
            _globalSettings.Backspace = "indent,start";
            Assert.True(_globalSettings.IsBackspaceIndent);
        }

        /// <summary>
        /// Setting a setting should raise the event even if the values are the same.  This is 
        /// depended on by the :noh feature
        /// </summary>
        [Fact]
        public void SetShouldRaise()
        {
            var seen = false;
            _globalSettings.HighlightSearch = true;
            _globalSettings.SettingChanged += delegate { seen = true; };
            _globalSettings.HighlightSearch = true;
            Assert.True(seen);
        }

        [Fact]
        public void Clipboard_SetUnnamed()
        {
            _globalSettings.Clipboard = "unnamed";
            Assert.Equal(ClipboardOptions.Unnamed, _globalSettings.ClipboardOptions);
            Assert.Equal("unnamed", _globalSettings.Clipboard);
        }

        [Fact]
        public void Clipboard_Multiple()
        {
            _globalSettings.Clipboard = "unnamed,autoselect";
            Assert.Equal(ClipboardOptions.Unnamed | ClipboardOptions.AutoSelect, _globalSettings.ClipboardOptions);
            Assert.Equal("unnamed,autoselect", _globalSettings.Clipboard);
        }

        /// <summary>
        /// Make sure the get / set logic for parsing out the options is complete
        /// </summary>
        [Fact]
        public void SelectModeOptions_Simple()
        {
            _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard | SelectModeOptions.Mouse;
            Assert.Equal("mouse,key", _globalSettings.SelectMode);
            _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
            Assert.Equal("key", _globalSettings.SelectMode);
        }

        [Fact]
        public void SelectModeOptions_Default()
        {
            Assert.Equal("", _globalSettings.SelectMode);
            Assert.Equal(SelectModeOptions.None, _globalSettings.SelectModeOptions);
            var setting = _globalSettings.GetSetting(GlobalSettingNames.SelectModeName).Value;
            Assert.Equal("", setting.DefaultValue.AsStringValue().Item);
        }
    }
}
