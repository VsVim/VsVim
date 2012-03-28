using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using System.Collections.ObjectModel;
using EditorUtils;

namespace VsVim.Settings
{
    [Export(typeof(ILegacySettings))]
    internal sealed class LegacySettings : ILegacySettings
    {
        private readonly Settings _settings = Settings.Default;

        #region ILegacySettings

        bool ILegacySettings.HaveUpdatedKeyBindings
        {
            get { return _settings.HaveUpdatedKeyBindings; }
            set { _settings.HaveUpdatedKeyBindings = value; }
        }

        bool ILegacySettings.IgnoredConflictingKeyBinding
        {
            get { return _settings.IgnoredConflictingKeyBinding; }
            set { _settings.IgnoredConflictingKeyBinding = value; }
        }

        ReadOnlyCollection<VsVim.CommandBindingSetting> ILegacySettings.RemovedBindings
        {
            get
            {
                return _settings.RemovedBindings
                    .Select(x => new VsVim.CommandBindingSetting(x.Name, x.CommandString))
                    .ToReadOnlyCollection();
            }
            set
            {
                _settings.RemovedBindings = value
                    .Select(x => new CommandBindingSetting() { Name = x.Name, CommandString = x.CommandString })
                    .ToArray();
            }
        }

        void ILegacySettings.Save()
        {
            _settings.Save();
        }

        #endregion
    }
}
