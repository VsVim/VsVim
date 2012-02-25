using System;
using System.Linq;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
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

        [Test]
        public void Sanity1()
        {
            var global = CreateGlobal();
            var all = global.AllSettings;
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettingNames.IgnoreCaseName));
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettingNames.ShiftWidthName));
        }

        [Test]
        public void SetByAbbreviation1()
        {
            var global = CreateGlobal();
            Assert.IsTrue(global.TrySetValueFromString("sw", "2"));
            Assert.AreEqual(2, global.ShiftWidth);
        }

        [Test]
        public void SetByAbbreviation2()
        {
            var global = CreateGlobal();
            Assert.IsFalse(global.IgnoreCase);
            Assert.IsTrue(global.TrySetValueFromString("ic", "true"));
            Assert.IsTrue(global.IgnoreCase);
        }

        [Test]
        public void IsVirtualEditOneMore1()
        {
            var global = CreateGlobal();
            global.VirtualEdit = String.Empty;
            Assert.IsFalse(global.IsVirtualEditOneMore);
        }

        [Test]
        public void IsVirtualEditOneMore2()
        {
            var global = CreateGlobal();
            global.VirtualEdit = "onemore";
            Assert.IsTrue(global.IsVirtualEditOneMore);
        }

        [Test]
        public void IsVirtualEditOneMore3()
        {
            var global = CreateGlobal();
            global.VirtualEdit = "onemore,blah";
            Assert.IsTrue(global.IsVirtualEditOneMore);
        }

        /// <summary>
        /// Ensure the IsBackspaceStart properly parsers start from the option
        /// </summary>
        [Test]
        public void IsBackspaceStart()
        {
            var globalSettings = CreateGlobal();
            Assert.IsFalse(globalSettings.IsBackspaceStart);
            globalSettings.Backspace = "eol,start";
            Assert.IsTrue(globalSettings.IsBackspaceStart);
        }

        /// <summary>
        /// Ensure the IsBackspaceEol properly parsers eol from the option
        /// </summary>
        [Test]
        public void IsBackspaceEol()
        {
            var globalSettings = CreateGlobal();
            Assert.IsFalse(globalSettings.IsBackspaceEol);
            globalSettings.Backspace = "eol,Eol";
            Assert.IsTrue(globalSettings.IsBackspaceEol);
        }

        /// <summary>
        /// Ensure the IsBackspaceIndent properly parsers start from the option
        /// </summary>
        [Test]
        public void IsBackspaceIndent()
        {
            var globalSettings = CreateGlobal();
            Assert.IsFalse(globalSettings.IsBackspaceIndent);
            globalSettings.Backspace = "indent,start";
            Assert.IsTrue(globalSettings.IsBackspaceIndent);
        }

        /// <summary>
        /// Setting a setting should raise the event even if the values are the same.  This is 
        /// depended on by the :noh feature
        /// </summary>
        [Test]
        public void SetShouldRaise()
        {
            var global = CreateGlobal();
            var seen = false;
            global.HighlightSearch = true;
            global.SettingChanged += delegate { seen = true; };
            global.HighlightSearch = true;
            Assert.IsTrue(seen);
        }

        [Test]
        public void Clipboard_SetUnnamed()
        {
            var global = CreateGlobal();
            global.Clipboard = "unnamed";
            Assert.AreEqual(ClipboardOptions.Unnamed, global.ClipboardOptions);
            Assert.AreEqual("unnamed", global.Clipboard);
        }

        [Test]
        public void Clipboard_Multiple()
        {
            var global = CreateGlobal();
            global.Clipboard = "unnamed,autoselect";
            Assert.AreEqual(ClipboardOptions.Unnamed | ClipboardOptions.AutoSelect, global.ClipboardOptions);
            Assert.AreEqual("unnamed,autoselect", global.Clipboard);
        }
    }
}
