using System;
using System.Collections.Generic;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest.Mock
{
    public class MockVimGlobalSettings : IVimGlobalSettings
    {
        public KeyInput DisableCommandImpl;
        public int CaretOpacity { get; set; }

        public KeyInput DisableCommand
        {
            get { return DisableCommandImpl; }
        }

        public bool DoubleEscape { get; set; }
        public bool HighlightSearch { get; set; }
        public bool IgnoreCase { get; set; }

        public bool IsVirtualEditOneMore
        {
            get { throw new NotImplementedException(); }
        }

        public int ScrollOffset { get; set; }
        public int ShiftWidth { get; set; }
        public bool SmartCase { get; set; }
        public bool StartOfLine { get; set; }
        public bool TildeOp { get; set; }
        public string VimRc { get; set; }
        public string VimRcPaths { get; set; }
        public string VirtualEdit { get; set; }
        public bool VisualBell { get; set; }

        public IEnumerable<Setting> AllSettings
        {
            get { throw new NotImplementedException(); }
        }

        public FSharpOption<Setting> GetSetting(string settingName)
        {
            throw new NotImplementedException();
        }

        public event FSharpHandler<Setting> SettingChanged;

        public void RaiseSettingChanged(Setting setting)
        {
            var e = SettingChanged;
            if (e != null)
            {
                e(this, setting);
            }
        }

        public bool TrySetValue(string settingName, SettingValue value)
        {
            throw new NotImplementedException();
        }

        public bool TrySetValueFromString(string settingName, string strValue)
        {
            throw new NotImplementedException();
        }
    }
}
