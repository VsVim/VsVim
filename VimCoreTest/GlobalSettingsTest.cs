using System;
using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class GlobalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return GlobalSettingNames.IgnoreCaseName; } }
        protected override IVimSettings Create()
        {
            return CreateGlobal();
        }

        private IVimGlobalSettings CreateGlobal()
        {
            return new GlobalSettings();
        }

        [Fact]
        public void Sanity1()
        {
            var global = CreateGlobal();
            var all = global.AllSettings;
            Assert.True(all.Any(x => x.Name == GlobalSettingNames.IgnoreCaseName));
            Assert.True(all.Any(x => x.Name == GlobalSettingNames.ShiftWidthName));
        }

        [Fact]
        public void SetByAbbreviation1()
        {
            var global = CreateGlobal();
            Assert.True(global.TrySetValueFromString("sw", "2"));
            Assert.Equal(2, global.ShiftWidth);
        }

        [Fact]
        public void SetByAbbreviation2()
        {
            var global = CreateGlobal();
            Assert.False(global.IgnoreCase);
            Assert.True(global.TrySetValueFromString("ic", "true"));
            Assert.True(global.IgnoreCase);
        }

        [Fact]
        public void IsVirtualEditOneMore1()
        {
            var global = CreateGlobal();
            global.VirtualEdit = String.Empty;
            Assert.False(global.IsVirtualEditOneMore);
        }

        [Fact]
        public void IsVirtualEditOneMore2()
        {
            var global = CreateGlobal();
            global.VirtualEdit = "onemore";
            Assert.True(global.IsVirtualEditOneMore);
        }

        [Fact]
        public void IsVirtualEditOneMore3()
        {
            var global = CreateGlobal();
            global.VirtualEdit = "onemore,blah";
            Assert.True(global.IsVirtualEditOneMore);
        }

        /// <summary>
        /// Ensure the IsBackspaceStart properly parsers start from the option
        /// </summary>
        [Fact]
        public void IsBackspaceStart()
        {
            var globalSettings = CreateGlobal();
            Assert.False(globalSettings.IsBackspaceStart);
            globalSettings.Backspace = "eol,start";
            Assert.True(globalSettings.IsBackspaceStart);
        }

        /// <summary>
        /// Ensure the IsBackspaceEol properly parsers eol from the option
        /// </summary>
        [Fact]
        public void IsBackspaceEol()
        {
            var globalSettings = CreateGlobal();
            Assert.False(globalSettings.IsBackspaceEol);
            globalSettings.Backspace = "eol,Eol";
            Assert.True(globalSettings.IsBackspaceEol);
        }

        /// <summary>
        /// Ensure the IsBackspaceIndent properly parsers start from the option
        /// </summary>
        [Fact]
        public void IsBackspaceIndent()
        {
            var globalSettings = CreateGlobal();
            Assert.False(globalSettings.IsBackspaceIndent);
            globalSettings.Backspace = "indent,start";
            Assert.True(globalSettings.IsBackspaceIndent);
        }

        /// <summary>
        /// Setting a setting should raise the event even if the values are the same.  This is 
        /// depended on by the :noh feature
        /// </summary>
        [Fact]
        public void SetShouldRaise()
        {
            var global = CreateGlobal();
            var seen = false;
            global.HighlightSearch = true;
            global.SettingChanged += delegate { seen = true; };
            global.HighlightSearch = true;
            Assert.True(seen);
        }

        [Fact]
        public void Clipboard_SetUnnamed()
        {
            var global = CreateGlobal();
            global.Clipboard = "unnamed";
            Assert.Equal(ClipboardOptions.Unnamed, global.ClipboardOptions);
            Assert.Equal("unnamed", global.Clipboard);
        }

        [Fact]
        public void Clipboard_Multiple()
        {
            var global = CreateGlobal();
            global.Clipboard = "unnamed,autoselect";
            Assert.Equal(ClipboardOptions.Unnamed | ClipboardOptions.AutoSelect, global.ClipboardOptions);
            Assert.Equal("unnamed,autoselect", global.Clipboard);
        }

        /// <summary>
        /// Make sure the get / set logic for parsing out the options is complete
        /// </summary>
        [Fact]
        public void SelectModeOptions_Simple()
        {
            var global = CreateGlobal();
            global.SelectModeOptions = SelectModeOptions.Keyboard | SelectModeOptions.Mouse;
            Assert.Equal("mouse,key", global.SelectMode);
            global.SelectModeOptions = SelectModeOptions.Keyboard;
            Assert.Equal("key", global.SelectMode);
        }
    }
}
