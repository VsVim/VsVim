using System;
using System.Collections.Generic;

namespace Vim.UnitTest.Mock
{
    public class MockVimLocalSettings : IVimLocalSettings
    {
        public IVimGlobalSettings GlobalSettingsImpl;
        public bool CursorLine { get; set; }
        public IVimGlobalSettings GlobalSettings
        {
            get { return GlobalSettingsImpl; }
        }

        public int Scroll { get; set; }
        public IEnumerable<Setting> AllSettings
        {
            get { throw new NotImplementedException(); }
        }

        public Microsoft.FSharp.Core.FSharpOption<Setting> GetSetting(string settingName)
        {
            throw new NotImplementedException();
        }

        public event Microsoft.FSharp.Control.FSharpHandler<Setting> SettingChanged;

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
